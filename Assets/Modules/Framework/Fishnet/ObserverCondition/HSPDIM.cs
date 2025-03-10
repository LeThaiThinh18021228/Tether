using FishNet;
using Framework.ADS;
using Framework.FishNet;
using Sirenix.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UIElements;
using static MongoDB.Bson.Serialization.Serializers.SerializerHelper;
namespace Framework.HSPDIMAlgo
{
    public enum Strategy
    {
        SEQUENTIAL,
        PARALEL_MUTUAL_REF,
        PARALEL_REF,
        PARALEL_ID_OUTPUT,
    }
    // error may occur: wrong minEntitySize => wrong tree height calculated, entity go outside space
    public class HSPDIM : SingletonNetwork<HSPDIM>, IDisposable
    {
        public static readonly float mapSizeEstimate = 100;
        public static int entityCountEstimate = 500;
        public static float minEntitySubRegSize = 10;
        public static float minEntityUpRegSize = 3;

        public static short subTreeDepth;
        public static short upTreeDepth;
        public static readonly short dimension = 2;
        public bool isRunning;

        public Dictionary<int, IHSPDIMEntity> HSPDIMEntities = new(entityCountEstimate);
        public List<HSPDIMRange> upRanges =new(entityCountEstimate);
        public List<HSPDIMRange> subRanges = new(entityCountEstimate);
        public HashSet<HSPDIMRange> modifiedUpRanges = new(entityCountEstimate);
        public HashSet<HSPDIMRange> modifiedSubRanges = new(entityCountEstimate);
        public BinaryTree<HSPDIMTreeNodeData>[] upTree;
        public BinaryTree<HSPDIMTreeNodeData>[] subTree;
        NativeHSPDIMFlattenedTree flattenUpTree;
        NativeHSPDIMFlattenedTree flattenSubTree;
        NativeHSPDIMFlattenedSortListTree flattenedSortListUpTree;
        NativeHSPDIMFlattenedSortListTree flattenedSortListSubTree;
        List<NativeBound> lowerBounds = new();
        List<NativeBound> upperBounds = new();
        List<NativeBound> coverBounds = new();
        List<NativeBound> insideBounds = new();
        NativeList<OverlapID> overlapSet;
        public List<int> RemovedEntities = new();
        public NativeParallelHashSet<int4> Result = new(0, Allocator.Persistent);
        public bool IsDynamic;
        public Strategy Strategy;

        Dictionary<int, HashSet<(int, int)>> pairOverlaps = new();
        #region Measure
        Stopwatch stopwatchTotal = new Stopwatch();
        Stopwatch stopwatchMapping = new Stopwatch();
        Stopwatch stopwatchInput = new Stopwatch();
        Stopwatch stopwatchRecalculateModifyOverlap = new Stopwatch();
        Stopwatch stopwatchMatching = new Stopwatch();
        Stopwatch stopwatchOutput = new Stopwatch();
        Stopwatch stopwatchMergeOverlap = new Stopwatch();
        public Stopwatch stopwatchLookupResult = new Stopwatch();
        int exeCount = 0;
        public int maxExeCount = 0;
        public int exclude = 0;
        double exeTotalTime = 0;
        double exeTotalTimeMapping = 0;
        double exeTotalTimeInput = 0;
        double exeTotalTimeRecalculateModifyOverlap = 0;
        double exeTotalTimeMatching = 0;
        double exeTotalTimeOutput = 0;
        double exeTotalTimeMergeOverlap = 0;
        public double exeTotalTimeLookupResult = 0;

        double totalMemAlgo = 0;
        double totalMemOutput = 0;
        float overlapTotal = 0;
        float overlapCurrent = 0;
        float intersectTotal = 0;

        public bool debugId;
        #endregion

        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short DepthCal(float subjectSize)
        {
            short depth = 0;
            while ((1<<( depth + 1)) <= mapSizeEstimate / subjectSize)
            {
                depth++;
            }
            return depth;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexCal(float subjectPos, int depth)
        {
            return Mathf.FloorToInt(subjectPos / mapSizeEstimate * (1<< depth));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatText(double value)
        {
            return value < 100 ? value.ToString("0.00") : ((value/1000).ToString("0.00") + "k");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int retrieveBit(int n, int pos)
        {
            return (n >> pos) & 1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int set1Bit(int n, int pos)
        {
            return n |= (1 << pos);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int set0Bit(int n, int pos)
        {
            return n &= ~(1 << pos);
        }
        public static bool UpdateInterval(float frequency = 1)
        {
            float time = Time.time * frequency;
            return Mathf.FloorToInt(time - Time.deltaTime * frequency) < Mathf.FloorToInt(time);
        }
        private void Update()
        {
            if (UpdateInterval(5) && isRunning)
            {
                exeCount++;
                stopwatchTotal.Reset();
                stopwatchMapping.Reset();
                stopwatchInput.Reset();
                stopwatchRecalculateModifyOverlap.Reset();
                stopwatchMatching.Reset();
                stopwatchOutput.Reset();
                stopwatchMergeOverlap.Reset();
                stopwatchLookupResult.Reset();

                stopwatchTotal.Start();

                var uplist = IsDynamic? modifiedUpRanges.ToArray(): upRanges.ToArray();
                var sublist = IsDynamic ? modifiedSubRanges.ToArray(): subRanges.ToArray();
                MappingRangeDynamic(uplist, upTree);
                MappingRangeDynamic(sublist, subTree);
                if (Strategy == Strategy.PARALEL_ID_OUTPUT || Strategy == Strategy.PARALEL_REF)
                {
                    ConvertFlattenedSortListTree(upRanges.Count ,uplist.Length, upTree,ref flattenUpTree,ref flattenedSortListUpTree);
                    ConvertFlattenedSortListTree(subRanges.Count, sublist.Length, subTree,ref flattenSubTree,ref flattenedSortListSubTree);
                }
                //LogTree(upTree, subTree);
                //PDebug.Log(flattenUpTree);
                //PDebug.Log(flattenSubTree);
                //PDebug.Log(flattenedSortListUpTree);
                //PDebug.Log(flattenedSortListSubTree);
                if (Strategy == Strategy.PARALEL_ID_OUTPUT)
                {
                    RecalculateModifiedOverlapId(uplist, sublist);
                }
                else
                {
                    RecalculateModifiedOverlapRef(uplist, sublist);
                }
                DynamicMatching();
                for (int i = 0; i < uplist.Length; i++)
                {
                    uplist[i].entity.Modified = Vector3Bool.@false;
                }
                for (int i = 0; i < sublist.Length; i++)
                {
                    sublist[i].entity.Modified = Vector3Bool.@false;
                }
                stopwatchTotal.Stop();
                if (exeCount > exclude)
                {
                    exeTotalTime += stopwatchTotal.Elapsed.TotalMilliseconds;
                    exeTotalTimeMapping += stopwatchMapping.Elapsed.TotalMilliseconds;
                    exeTotalTimeRecalculateModifyOverlap += stopwatchRecalculateModifyOverlap.Elapsed.TotalMilliseconds;
                    exeTotalTimeInput += stopwatchInput.Elapsed.TotalMilliseconds;
                    exeTotalTimeMatching += stopwatchMatching.Elapsed.TotalMilliseconds;
                    exeTotalTimeOutput += stopwatchOutput.Elapsed.TotalMilliseconds;
                    exeTotalTimeMergeOverlap += stopwatchMergeOverlap.Elapsed.TotalMilliseconds;
                    exeTotalTimeLookupResult += stopwatchLookupResult.Elapsed.TotalMilliseconds;
                    var total = exeTotalTime / (exeCount - exclude);
                    var mapping = exeTotalTimeMapping / (exeCount - exclude);
                    var input = exeTotalTimeInput / (exeCount - exclude);
                    var recal = exeTotalTimeRecalculateModifyOverlap / (exeCount - exclude);
                    var matching = exeTotalTimeMatching / (exeCount - exclude);
                    var output = exeTotalTimeOutput / (exeCount - exclude);
                    var merge = exeTotalTimeMergeOverlap / (exeCount - exclude);
                    var lookup = exeTotalTimeLookupResult / (exeCount - exclude);

                    PDebug.LogWarning($"Thread Count {JobsUtility.JobWorkerCount}, Sub Mod Count {modifiedSubRanges.Count}, Up Mod Count {modifiedUpRanges.Count} over {exeCount - exclude} " +
                        $"exeTotalTime : {total} _ {stopwatchTotal.Elapsed.TotalMilliseconds}" +
                        $"\nexeTotalTimeMapping : {mapping} _ {stopwatchMapping.Elapsed.TotalMilliseconds}" +
                        $"\nexeTotalTimeRecalculateModifyOverlap : {recal} _ {stopwatchRecalculateModifyOverlap.Elapsed.TotalMilliseconds}" +
                        $"\nexeTotalTimeInput : {input} _ {stopwatchInput.Elapsed.TotalMilliseconds}" +
                        $"\nexeTotalTimeMatching {matching}  _ {stopwatchMatching.Elapsed.TotalMilliseconds}" +
                        $"\nexeTimeOutPut : {output}  _ {stopwatchOutput.Elapsed.TotalMilliseconds}" +
                        $"\nexeTotalTimeMergeOverlap: {merge} _ {stopwatchMergeOverlap.Elapsed.TotalMilliseconds}" +
                        $"\nexeTotalTimeLookupResult: {lookup}  _ {stopwatchLookupResult.Elapsed.TotalMilliseconds}" +
                        $"\nMem {totalMemAlgo / 1024f / (exeCount - exclude)} " +
                        $"\n time with average overlap {intersectTotal / (exeCount - exclude)} {overlapTotal / (exeCount - exclude)}: {overlapCurrent}" +
                        $"\n{FormatText(total)}" +
                        $"\n{FormatText(mapping)}" +
                        $"\n{FormatText(recal)}" +
                        $"\n{FormatText(input)}" +
                        $"\n{FormatText(matching)}" +
                        $"\n{FormatText(output)}" +
                        $"\n{FormatText(merge)}" +
                        $"\n{FormatText(lookup)}");

                    if (exeCount - exclude >= maxExeCount)
                    {
                        Application.Quit();
                        Time.timeScale = 0;
                    }
                }

                modifiedUpRanges.Clear();
                modifiedSubRanges.Clear();
                //LogTree(upTree);
                //LogTree(subTree);
            }
        }
        public void OnGameStart(GameState prev, GameState next, bool asServer)
        {
            if (!asServer) return;
            if (next == GameState.STARTED)
            {
                InitMappingAndMatching();
            }
        }
        public override void OnStopServer()
        {
            base.OnStopServer();
            Dispose();
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Dispose();
        }
        public void InitMappingAndMatching()
        {
            HSPDIMEntities.EnsureCapacity(entityCountEstimate);
            upRanges.Capacity = entityCountEstimate;
            subRanges.Capacity = entityCountEstimate;
            subTreeDepth = DepthCal(minEntitySubRegSize);
            upTreeDepth = DepthCal(minEntityUpRegSize);
            PDebug.Log(subTreeDepth);
            upTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMTreeNodeData>(upTreeDepth)).ToArray();
            subTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMTreeNodeData>(subTreeDepth)).ToArray();
            upTree.ForEach(tree => tree.ForEach(node =>
            {
                node.Data.lowerBound = mapSizeEstimate * node.index / (1<< node.depth) ;
                node.Data.upperBound = mapSizeEstimate *(node.index + 1) / (1<< node.depth);
            }));
            subTree.ForEach(tree => tree.ForEach(node =>
            {
                node.Data.lowerBound = mapSizeEstimate * node.index / (1 << node.depth);
                node.Data.upperBound = mapSizeEstimate * (node.index + 1) / (1 << node.depth);
            }));
            flattenUpTree = new()
            {
                LowerDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
                UpperDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
                CoverDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
                InsideDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
            };
            flattenSubTree = new()
            {
                LowerDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
                UpperDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
                CoverDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
                InsideDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
            };
            flattenedSortListUpTree = new()
            {
                ElementDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
            };
            flattenedSortListSubTree = new()
            {
                ElementDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
            };
            var uplist = upRanges.ToArray();
            var sublist = subRanges.ToArray();
            MappingRanges(uplist, upTree);
            MappingRanges(sublist, subTree);
            InitFlatteningTree(uplist.Length, uplist.Length, upTree, ref flattenUpTree,ref flattenedSortListUpTree);
            InitFlatteningTree(sublist.Length, sublist.Length, subTree,ref flattenSubTree,ref flattenedSortListSubTree);
            if (Strategy == Strategy.PARALEL_ID_OUTPUT)
            {
                ulong overlapMaxSize = (ulong)dimension * (ulong)(flattenUpTree.Lowers.Length + flattenUpTree.Uppers.Length + flattenUpTree.Insides.Length) * (ulong)(flattenSubTree.Lowers.Length + flattenSubTree.Uppers.Length + flattenSubTree.Insides.Length) / 4;
                overlapMaxSize = Math.Min(overlapMaxSize, (ulong)(2147483646 * 0.4 / UnsafeUtility.SizeOf<int4>()));
                Result = new NativeParallelHashSet<int4>((int)overlapMaxSize, Allocator.Persistent);
            }
            //LogTree(upTree, sortListUpTree);
            //LogTree(subTree, sortListSubTree);
            //PDebug.Log(flattenUpTree);
            //PDebug.Log(flattenSubTree);
            MatchingTreeToTreeParallelRef(flattenUpTree, flattenSubTree, flattenedSortListSubTree, upTree, subTree, true);
            flattenUpTree.Dispose();
            flattenSubTree.Dispose();
            flattenedSortListSubTree.Dispose();
            flattenedSortListUpTree.Dispose();
            for (int j = 0; j < subRanges.Count; j++)
            {
                subRanges[j].UpdateIntersectionId();
            }
            for (int j = 0; j < subRanges.Count; j++)
            {
                subRanges[j].entity.Modified = Vector3Bool.@false;
            }
            for (int j = 0; j < upRanges.Count; j++)
            {
                upRanges[j].entity.Modified = Vector3Bool.@false;
            }
            modifiedUpRanges.Clear();
            modifiedSubRanges.Clear();
            isRunning = true;
        }
        void InitFlatteningTree(int length, int modifiedLength, BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenedTree, ref NativeHSPDIMFlattenedSortListTree flattenedSortListTree)
        {
            //flattenedTree
            int totalNode = 0;
            for (int i = 0; i < dimension; i++)
            {
                totalNode += (1 << (tree[i].depth + 1)) - 1;
            }
            flattenedTree.depth = new NativeArray<short>(new short[] { tree[0].depth, tree[1].depth }, Allocator.Persistent);
            flattenedTree.LowerNodes = new(totalNode, Allocator.Persistent);
            flattenedTree.UpperNodes = new(totalNode, Allocator.Persistent);
            flattenedTree.CoverNodes = new(totalNode, Allocator.Persistent);
            flattenedTree.InsideNodes = new(totalNode, Allocator.Persistent);

            int capacity = (length * dimension / 2);
            capacity = (capacity > lowerBounds.Capacity) ? capacity : lowerBounds.Capacity;
            lowerBounds.Capacity = upperBounds.Capacity = insideBounds.Capacity = capacity;
            int startLower = 0;
            int startUpper = 0;
            int startCover = 0;
            int startInside = 0;
            int startLowerDimension = 0;
            int startUpperDimension = 0;
            int startCoverDimension = 0;
            int startInsideDimension = 0;
            for (int i = 0; i < dimension; i++)
            {
                int totalNodeDimI = (1 << (tree[i].depth + 1)) - 1;
                foreach (var node in tree[i])
                //Parallel.ForEach(tree[i], (node) =>
                {
                    int index = (1 << node.depth) + node.index - 1 + startInsideDimension;
                    if (node.Data.lowers.Count > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, node.Data.lowers.Count);
                        startLower += node.Data.lowers.Count;
                        for (int j = 0; j < node.Data.lowers.Count; j++)
                        {
                            var b = node.Data.lowers[j];
                            lowerBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                        }
                    }
                    else
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, 0);
                    }


                    if (node.Data.uppers.Count > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, node.Data.uppers.Count);
                        startUpper += node.Data.uppers.Count;
                        for (int j = 0; j < node.Data.uppers.Count; j++)
                        {
                            var b = node.Data.uppers[j];
                            upperBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                        }
                    }
                    else
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, 0);
                    }


                    if (node.Data.covers.Count > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, node.Data.covers.Count);
                        startCover += node.Data.covers.Count;
                        for (int j = 0; j < node.Data.covers.Count; j++)
                        {
                            var b = node.Data.covers[j];
                            coverBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                        }
                    }
                    else
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, 0);
                    }


                    if (node.Data.insides.Count > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, node.Data.insides.Count);
                        startInside += node.Data.insides.Count;
                        for (int j = 0; j < node.Data.insides.Count; j++)
                        {
                            var b = node.Data.insides[j];
                            insideBounds.Add(HSPDIMBound.ToNativeBound(b, j, true));
                        }
                    }
                    else
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, 0);
                    }
                }
                ;
                flattenedTree.LowerDimensions[i] = new NativeListElement(startLowerDimension, totalNodeDimI);
                startLowerDimension += totalNodeDimI;
                flattenedTree.UpperDimensions[i] = new NativeListElement(startUpperDimension, totalNodeDimI);
                startUpperDimension += totalNodeDimI;
                flattenedTree.CoverDimensions[i] = new NativeListElement(startCoverDimension, totalNodeDimI);
                startCoverDimension += totalNodeDimI;
                flattenedTree.InsideDimensions[i] = new NativeListElement(startInsideDimension, totalNodeDimI);
                startInsideDimension += totalNodeDimI;
            }
            flattenedTree.Lowers = new NativeArray<NativeBound>(lowerBounds.Count, Allocator.TempJob);
            for (int i = 0; i < lowerBounds.Count; i++)
            {
                flattenedTree.Lowers[i] = lowerBounds[i];
            }
            flattenedTree.Uppers = new NativeArray<NativeBound>(upperBounds.Count, Allocator.TempJob);
            for (int i = 0; i < upperBounds.Count; i++)
            {
                flattenedTree.Uppers[i] = upperBounds[i];
            }
            flattenedTree.Covers = new NativeArray<NativeBound>(coverBounds.Count, Allocator.TempJob);
            for (int i = 0; i < coverBounds.Count; i++)
            {
                flattenedTree.Covers[i] = coverBounds[i];
            }
            flattenedTree.Insides = new NativeArray<NativeBound>(insideBounds.Count, Allocator.TempJob);
            for (int i = 0; i < insideBounds.Count; i++)
            {
                flattenedTree.Insides[i] = insideBounds[i];
            }
            lowerBounds.Clear();
            upperBounds.Clear();
            coverBounds.Clear();
            insideBounds.Clear();

            //flattenedSortListTree
            stopwatchTotal.Stop();
            for (int i = 0; i < dimension; i++)
            {
                flattenedSortListTree.ElementDimensions[i] = new NativeListElement(totalNode * i * 2 / dimension, totalNode * 2 / dimension);
            }
            flattenedSortListTree.ElementList = new(totalNode * 2, Allocator.Persistent);
            flattenedSortListTree.Bounds = new NativeArray<NativeBound>(modifiedLength * 2 * dimension, Allocator.TempJob);
            List<NativeBound> listSortedNativeBounds = new();
            int startSortedBound = 0;
            int startSortedBoundDimension = 0;
            for (int i = 0; i < dimension; i++)
            {
                int dimTreeDepth = tree[i].depth;
                int totalNodeDimI = (1 << (dimTreeDepth + 2)) - 2;

                // Process each node in the current dimension.
                foreach (var node in tree[i])
                {
                    int nodeDepth = node.depth;
                    int nodeIndex = node.index;
                    int index = (1 << (nodeDepth + 1)) + nodeIndex * 2 - 2 + startSortedBoundDimension;

                    List<NativeBound> crossNativeBounds = new List<NativeBound>();
                    for (int k = 0; k < node.Data.lowers.Count; k++)
                    {
                        HSPDIMBound b = node.Data.lowers[k];
                        if (b.entity.Modified[i])
                        {
                            // Use original container index k.
                            crossNativeBounds.Add(HSPDIMBound.ToNativeBound(b, k, false));
                        }
                    }

                    int maxCurrentLevelIndex = 1 << nodeDepth;
                    if (crossNativeBounds.Count > 0)
                    {
                        var siblingUppers = tree[i][nodeDepth, nodeIndex + 1].Data.uppers;
                        for (int j = 0; j < siblingUppers.Count; j++)
                        {
                            HSPDIMBound b = siblingUppers[j];
                            if (b.entity.Modified[i] && b.range.Bounds[i, 0].index == nodeIndex)
                            {
                                crossNativeBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                            }
                        }
                        if (nodeIndex + 2 < maxCurrentLevelIndex)
                        {
                            siblingUppers = tree[i][nodeDepth, nodeIndex + 2].Data.uppers;
                            for (int j = 0; j < siblingUppers.Count; j++)
                            {
                                HSPDIMBound b = siblingUppers[j];
                                if (b.entity.Modified[i] && b.range.Bounds[i, 0].index == nodeIndex)
                                {
                                    crossNativeBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                                }
                            }
                        }
                    }

                    flattenedSortListTree.ElementList[index] = new NativeListElement(startSortedBound, crossNativeBounds.Count);
                    listSortedNativeBounds.AddRange(crossNativeBounds);
                    startSortedBound += crossNativeBounds.Count;

                    List<NativeBound> insideNativeBounds = new();
                    if (nodeDepth == dimTreeDepth)
                    {
                        for (int k = 0; k < node.Data.insides.Count; k++)
                        {
                            HSPDIMBound b = node.Data.insides[k];
                            if (b.entity.Modified[i])
                            {
                                insideNativeBounds.Add(HSPDIMBound.ToNativeBound(b, k, true));
                            }
                        }
                    }
                    flattenedSortListTree.ElementList[index + 1] = new NativeListElement(startSortedBound, insideNativeBounds.Count);
                    listSortedNativeBounds.AddRange(insideNativeBounds);
                    startSortedBound += insideNativeBounds.Count;
                }

                flattenedSortListTree.ElementDimensions[i] = new NativeListElement(startSortedBoundDimension, totalNodeDimI);
                startSortedBoundDimension += totalNodeDimI;
            }


            for (int i = 0; i < listSortedNativeBounds.Count; i++)
            {
                flattenedSortListTree.Bounds[i] = listSortedNativeBounds[i];
            }
            stopwatchTotal.Start();
        }
        void ConvertFlattenedSortListTree(int length, int modifiedLength, BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenedTree, ref NativeHSPDIMFlattenedSortListTree flattenedSortListTree)
        {
            stopwatchInput.Start();
            int capacity = (length * dimension / 2);
            capacity = (capacity > lowerBounds.Capacity) ? capacity : lowerBounds.Capacity;
            lowerBounds.Capacity = upperBounds.Capacity = insideBounds.Capacity = capacity;
            int startLower = 0;
            int startUpper = 0;
            int startCover = 0;
            int startInside = 0;
            int startLowerDimension = 0;
            int startUpperDimension = 0;
            int startCoverDimension = 0;
            int startInsideDimension = 0;

            for (int i = 0; i < dimension; i++)
            {
                int totalNodeDimI = (1 << (tree[i].depth + 1)) - 1;
                foreach (var node in tree[i])
                //Parallel.ForEach(tree[i], (node) =>
                {
                    int index = (1 << node.depth) + node.index - 1 + startInsideDimension;
                    
                    var lowers = node.Data.lowers;
                    int lowersCount = lowers.Count;
                    if (lowersCount > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, lowersCount);
                        startLower += lowersCount;
                        for (int j = 0; j < lowersCount; j++)
                        {
                            var b = lowers[j];
                            lowerBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                        }
                    }
                    else
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, 0);
                    }

                    var uppers = node.Data.uppers;
                    int uppersCount = uppers.Count;
                    if (uppersCount > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, uppersCount);
                        startUpper += uppersCount;
                        for (int j = 0; j < uppersCount; j++)
                        {
                            var b = uppers[j];
                            upperBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                        }
                    }
                    else
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, 0);
                    }

                    var covers = node.Data.covers;
                    int coversCount = covers.Count;
                    if (coversCount > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, coversCount);
                        startCover += coversCount;
                        for (int j = 0; j < coversCount; j++)
                        {
                            var b = covers[j];
                            coverBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                        }
                    }
                    else
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, 0);
                    }

                    var insides = node.Data.insides;
                    int insidesCount = insides.Count;
                    if (insidesCount > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, insidesCount);
                        startInside += insidesCount;
                        for (int j = 0; j < insidesCount; j++)
                        {
                            var b = insides[j];
                            insideBounds.Add(HSPDIMBound.ToNativeBound(b, j, true));
                        }
                    }
                    else
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, 0);
                    }
                };
                flattenedTree.LowerDimensions[i] = new NativeListElement(startLowerDimension, totalNodeDimI);
                startLowerDimension += totalNodeDimI;
                flattenedTree.UpperDimensions[i] = new NativeListElement(startUpperDimension, totalNodeDimI);
                startUpperDimension += totalNodeDimI;
                flattenedTree.CoverDimensions[i] = new NativeListElement(startCoverDimension, totalNodeDimI);
                startCoverDimension += totalNodeDimI;
                flattenedTree.InsideDimensions[i] = new NativeListElement(startInsideDimension, totalNodeDimI);
                startInsideDimension += totalNodeDimI;
            }

            flattenedTree.Lowers = new NativeArray<NativeBound>(lowerBounds.Count, Allocator.TempJob);
            for (int i = 0; i < lowerBounds.Count; i++)
            {
                flattenedTree.Lowers[i] = lowerBounds[i];
            }
            flattenedTree.Uppers = new NativeArray<NativeBound>(upperBounds.Count, Allocator.TempJob);
            for (int i = 0; i < upperBounds.Count; i++)
            {
                flattenedTree.Uppers[i] = upperBounds[i];
            }
            flattenedTree.Covers = new NativeArray<NativeBound>(coverBounds.Count, Allocator.TempJob);
            for (int i = 0; i < coverBounds.Count; i++)
            {
                flattenedTree.Covers[i] = coverBounds[i];
            }
            flattenedTree.Insides = new NativeArray<NativeBound>(insideBounds.Count, Allocator.TempJob);
            for (int i = 0; i < insideBounds.Count; i++)
            {
                flattenedTree.Insides[i] = insideBounds[i];
            }
            lowerBounds.Clear();
            upperBounds.Clear();
            coverBounds.Clear();
            insideBounds.Clear();
            stopwatchInput.Stop();

            //
            stopwatchTotal.Stop();
            flattenedSortListTree.Bounds = new NativeArray<NativeBound>(modifiedLength * 2 * dimension, Allocator.TempJob);
            List<NativeBound> listSortedNativeBounds = new();
            int startSortedBound = 0;
            int startSortedBoundDimension = 0;
            for (int i = 0; i < dimension; i++)
            {
                int dimTreeDepth = tree[i].depth;
                int totalNodeDimI = (1 << (dimTreeDepth + 2)) - 2;

                // Process each node in the current dimension.
                foreach (var node in tree[i])
                {
                    int nodeDepth = node.depth;
                    int nodeIndex = node.index;
                    int index = (1 << (nodeDepth + 1)) + nodeIndex * 2 - 2 + startSortedBoundDimension;

                    List<NativeBound> crossNativeBounds = new List<NativeBound>();
                    for (int k = 0; k < node.Data.lowers.Count; k++)
                    {
                        HSPDIMBound b = node.Data.lowers[k];
                        if (b.entity.Modified[i] || !IsDynamic)
                        {
                            crossNativeBounds.Add(HSPDIMBound.ToNativeBound(b, k, false));
                        }
                    }

                    int maxCurrentLevelIndex = 1 << nodeDepth;
                    if (crossNativeBounds.Count > 0)
                    {
                        var siblingUppers = tree[i][nodeDepth, nodeIndex + 1].Data.uppers;
                        for (int j = 0; j < siblingUppers.Count; j++)
                        {
                            HSPDIMBound b = siblingUppers[j];
                            if ((b.entity.Modified[i] || !IsDynamic) && b.range.Bounds[i, 0].index == nodeIndex)
                            {
                                crossNativeBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                            }
                        }
                        if (nodeIndex + 2 < maxCurrentLevelIndex)
                        {
                            siblingUppers = tree[i][nodeDepth, nodeIndex + 2].Data.uppers;
                            for (int j = 0; j < siblingUppers.Count; j++)
                            {
                                HSPDIMBound b = siblingUppers[j];
                                if ((b.entity.Modified[i] || !IsDynamic) && b.range.Bounds[i, 0].index == nodeIndex)
                                {
                                    crossNativeBounds.Add(HSPDIMBound.ToNativeBound(b, j, false));
                                }
                            }
                        }
                    }

                    flattenedSortListTree.ElementList[index] = new NativeListElement(startSortedBound, crossNativeBounds.Count);
                    listSortedNativeBounds.AddRange(crossNativeBounds);
                    startSortedBound += crossNativeBounds.Count;

                    List<NativeBound> insideNativeBounds = new();
                    if (nodeDepth == dimTreeDepth)
                    {
                        for (int k = 0, count = node.Data.insides.Count; k < count; k++)
                        {
                            HSPDIMBound b = node.Data.insides[k];
                            if (b.entity.Modified[i] || !IsDynamic)
                            {
                                insideNativeBounds.Add(HSPDIMBound.ToNativeBound(b, k, true));
                            }
                        }
                    }
                    flattenedSortListTree.ElementList[index + 1] = new NativeListElement(startSortedBound, insideNativeBounds.Count);
                    listSortedNativeBounds.AddRange(insideNativeBounds);
                    startSortedBound += insideNativeBounds.Count;
                }

                flattenedSortListTree.ElementDimensions[i] = new NativeListElement(startSortedBoundDimension, totalNodeDimI);
                startSortedBoundDimension += totalNodeDimI;
            }


            for (int i = 0; i < listSortedNativeBounds.Count; i++)
            {
                flattenedSortListTree.Bounds[i] = listSortedNativeBounds[i];
            }
            //BuildFlattenedSortListTreeJob job = new()
            //{
            //    dynamic = IsDynamic,
            //    FlattenedTree = flattenedTree,
            //    OutputSortListTree = flattenedSortListTree
            //};
            //JobHandle jobHandle = job.Schedule(flattenedTree.LowerNodes.Length, 1);
            //jobHandle.Complete();
            stopwatchTotal.Start();
        }
        private void Matching(List<HSPDIMBound> sortedBounds, BinaryTree<HSPDIMTreeNodeData> tree, int i)
        {
            int boundsCount = sortedBounds.Count;
            if (boundsCount == 0) return;
            //StringBuilder sb = new StringBuilder();
            //sb.Append("StartMatching\n");
            BinaryTree<Vector3Int> indexTree = new(tree.depth);
            List<HSPDIMRange> newIns = new();
            List<HSPDIMRange> subset = new();
            List<HSPDIMRange> upset = new();
            int leftLeaf = IndexCal(sortedBounds.First().boundValue, tree.depth);
            int rightLeaf = IndexCal(sortedBounds.Last().boundValue, tree.depth);
            int j = 0;
            int m2 = leftLeaf, m = leftLeaf;
            HSPDIMBound boundInSortedList;
            HSPDIMBound boundInTree;

            //sb.Append($"sortedListCount:{sortedBounds.Count},leftLeaf:{leftLeaf},rightLeaf:{rightLeaf}\n");
            //Debug.Log(sb);
            //sb.Clear();
            while (j < boundsCount && m <= rightLeaf)
            {
                boundInSortedList = sortedBounds[j];
                //sb.Append($"bound in SortedList:{boundInSortedList.boundValue},indexLeaf:{m},boundInListIndex:{IndexCal(boundInSortedList.boundValue, tree.depth)}\n");
                TreeNode<HSPDIMTreeNodeData> node;
                TreeNode<Vector3Int> indexNode;
                if (IndexCal(boundInSortedList.boundValue, tree.depth) == m)
                {
                    //sb.Append("go at leaf");
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--, k = k / 2)
                    {
                        //sb.Append($"node:[{l},{k}] -> ");
                        node = tree[l, k];
                        indexNode = indexTree[l, k];
                        if (boundInSortedList.isUpper == -1)
                        {
                            if (node.Data.uppers.Count > 0 && indexNode.Data.y < node.Data.uppers.Count)
                            {
                                boundInTree = node.Data.uppers[indexNode.Data.y];
                                while (boundInTree.boundValue <= boundInSortedList.boundValue)
                                {
                                    indexNode.Data.y++;
                                    if (indexNode.Data.y < node.Data.uppers.Count)
                                    {
                                        boundInTree = node.Data.uppers[indexNode.Data.y];
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            if (node.Data.uppers.Count - indexNode.Data.y > 0)
                            {
                                for (int idx = indexNode.Data.y; idx < node.Data.uppers.Count; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.uppers[idx].range;
                                    boundInSortedList.range.overlapSetsId[i].Add(overlapRange.entity.Id);
                                    overlapRange.overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                }
                                //sb.Append($"add Overlap Upper from{indexNode.Data.y} to {node.Data.uppers.Count}:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))};\n ");
                            }

                            if (node.Data.covers.Count > 0)
                            {
                                for (int idx = 0; idx < node.Data.covers.Count; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.covers[idx].range;
                                    boundInSortedList.range.overlapSetsId[i].Add(overlapRange.entity.Id);
                                    overlapRange.overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                }
                                //sb.Append($"add {node.Data.covers.Count} Overlap Cover:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}; \n");
                            }

                            if (l == tree.depth)
                            {
                                newIns.Add(boundInSortedList.range);
                                //sb.Append($"add {boundInSortedList.range} to newIns; \n");
                            }
                        }
                        else if (boundInSortedList.isUpper == 1)
                        {
                            if (node.Data.lowers.Count > 0 && indexNode.Data.x < node.Data.lowers.Count)
                            {
                                boundInTree = node.Data.lowers[indexNode.Data.x];
                                while (boundInTree.boundValue < boundInSortedList.boundValue)
                                {
                                    indexNode.Data.x++;
                                    if (indexNode.Data.x < node.Data.lowers.Count)
                                    {
                                        boundInTree = node.Data.lowers[indexNode.Data.x];
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            if (indexNode.Data.x > 0)
                            {
                                for (int idx = 0; idx < indexNode.Data.x; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.lowers[idx].range;
                                    boundInSortedList.range.overlapSetsId[i].Add(overlapRange.entity.Id);
                                    overlapRange.overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                }
                                //sb.Append($"add Overlap Lower from {0} to {indexNode.Data.x}:\n{string.Join(",", overlapRange.Select(r => r.ToString()))}; \n");
                            }
                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                                //sb.Append($"remove {boundInSortedList.range} from newIns; \n");
                            }
                        }
                        //sb.Append($"\n");
                    }
                    SortMatchInside(boundInSortedList, tree, indexTree, i, m, subset, upset, true, null);
                    j++;
                    m2 = m;
                }
                else
                {
                    //sb.Append("leave leaf\n");
                    //sb.Append($"add Overlap Lower to\n{string.Join("\n", newIns.Select(r => r.ToString()))} \n");
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--)
                    {
                        node = tree[l, k];
                        //sb.Append($"node:[{i},{l},{k}]");
                        if (node.Data.lowers.Count > 0)
                        {
                            foreach (HSPDIMRange b in newIns)
                            {
                                for (int idx = 0; idx < node.Data.lowers.Count; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.lowers[idx].range;
                                    b.overlapSetsId[i].Add(overlapRange.entity.Id);
                                    overlapRange.overlapSetsId[i].Add(b.entity.Id);
                                }
                            }

                            //sb.Append($"\t overlap {node.Data.lowers.Count} lower:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}");
                        }
                        //sb.Append($"\n");
                        if (l == tree.depth)
                        {
                            if (node.Data.insides.Count > 0)
                                foreach (HSPDIMRange b in newIns)
                                {
                                    if (m > IndexCal(b.Bounds[i, 0].boundValue, tree.depth))
                                    {
                                        for (int idx = 0; idx < node.Data.insides.Count; idx++)
                                        {
                                            HSPDIMRange overlapRange = node.Data.insides[idx].range;
                                            b.overlapSetsId[i].Add(overlapRange.entity.Id);
                                            overlapRange.overlapSetsId[i].Add(b.entity.Id);
                                        }
                                        //sb.Append($"\t overlap {b} inside:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}");
                                    }
                                };
                            if (m == m2)
                            {
                                //SortMatchInside(boundInSortedList, tree, indexTree, i, m, subset, upset, false, null);
                            }
                        }
                        if ((k + 1) % 2 == 0) k = k / 2;
                        else break;
                    }
                    m++;
                }
                //Debug.Log(sb);
                //sb.Clear();
            }
        }
        private void SortMatchInside(HSPDIMBound boundInSortedList, BinaryTree<HSPDIMTreeNodeData> tree, BinaryTree<Vector3Int> indexTree, int i, int m, List<HSPDIMRange> subset, List<HSPDIMRange> upset, bool headEnd, StringBuilder sb)
        {
            //sb.Append($"matching inside range at leaf {m} ");
            List<HSPDIMBound> insides = tree[tree.depth, m].Data.insides;
            int insidesCount = insides.Count;
            if (insidesCount > 0)
            {
                TreeNode<Vector3Int> indexNode = indexTree[tree.depth, m];
                HSPDIMBound boundInTree;
                //sb.Append($"insideIt: {indexTree[tree.depth, m].Data.z}\n");
                if (indexNode.Data.z < insidesCount)
                {
                    boundInTree = insides[indexNode.Data.z];
                    while (boundInTree.boundValue <= boundInSortedList.boundValue)
                    {
                        //sb.Append($"subset before:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
                        if (boundInTree.isUpper == -1)
                        {
                            subset.Add(boundInTree.range);
                        }
                        else if (boundInTree.isUpper == 1)
                        {
                            subset.Remove(boundInTree.range);
                            for (int j = 0, upsetCount = upset.Count; j < upsetCount; j++)
                            {
                                upset[j].overlapSetsId[i].Add(boundInTree.range.entity.Id);
                                boundInTree.range.overlapSetsId[i].Add(upset[j].entity.Id);
                            }
                            //if (upset.Count > 0)
                            //{
                            //    sb.Append($"matching inside range {boundInTree.range} :\n{string.Join("\n", upset.Select(r => r.ToString()))} \n");
                            //}
                        }
                        //sb.Append($"subset after:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
                        indexNode.Data.z++;
                        if (indexNode.Data.z < insidesCount)
                        {
                            boundInTree = insides[indexNode.Data.z];
                        }
                        else
                        {
                            //sb.Append($"\n");
                            break;
                        }
                    }
                }
            }
            if (boundInSortedList.isUpper == -1)
            {
                if (headEnd) upset.Add(boundInSortedList.range);
            }
            else if (boundInSortedList.isUpper == 1)
            {
                if (headEnd) upset.Remove(boundInSortedList.range);
                //boundInSortedList.range.overlapSets[i].UnionWith(subset);
                //subset.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                //sb.Append($"add {subset.Count} Overlap inside {boundInSortedList}:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
                for (int j = 0, subsetCount = subset.Count; j < subsetCount; j++)
                {
                    boundInSortedList.range.overlapSetsId[i].Add(subset[j].entity.Id);
                    subset[j].overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                }
            }
        }
        private void MatchingParallel(List<HSPDIMBound> sortedBounds, BinaryTree<HSPDIMTreeNodeData> tree, int i)
        {
            int boundsCount = sortedBounds.Count;
            if (boundsCount == 0) return;
            BinaryTree<Vector3Int> indexTree = new(tree.depth);
            foreach (var node in indexTree)
            {
                node.Data = new Vector3Int(0, 0, 0);
            }
            List<HSPDIMRange> newIns = new();
            List<HSPDIMRange> subset = new();
            List<HSPDIMRange> upset = new();
            int leftLeaf = IndexCal(sortedBounds.First().boundValue, tree.depth);
            int rightLeaf = IndexCal(sortedBounds.Last().boundValue, tree.depth);
            int j = 0;
            int m2 = leftLeaf, m = leftLeaf;
            HSPDIMBound boundInSortedList;
            HSPDIMBound boundInTree = null;
            HSPDIMRange overlapRange;
            while (j < boundsCount && m <= rightLeaf)
            {
                boundInSortedList = sortedBounds[j];
                TreeNode<HSPDIMTreeNodeData> node;
                TreeNode<Vector3Int> indexNode = null;
                if (IndexCal(boundInSortedList.boundValue, tree.depth) == m)
                {
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--, k = k / 2)
                    {
                        node = tree[l, k];
                        indexNode = indexTree[l, k];
                        if (boundInSortedList.isUpper == -1)
                        {
                            if (node.Data.uppers.Count > 0 && indexNode.Data.y < node.Data.uppers.Count)
                            {
                                boundInTree = node.Data.uppers[indexNode.Data.y];
                                while (boundInTree.boundValue <= boundInSortedList.boundValue)
                                {
                                    Vector3Int temp = indexNode.Data;
                                    temp.y++;
                                    indexNode.Data = temp;
                                    if (indexNode.Data.y < node.Data.uppers.Count)
                                    {
                                        boundInTree = node.Data.uppers[indexNode.Data.y];
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            if (node.Data.uppers.Count - indexNode.Data.y > 0)
                            {
                                lock (boundInSortedList.range.overlapSetsId[i])

                                    for (int idx = indexNode.Data.y; idx < node.Data.uppers.Count; idx++)
                                    {
                                        overlapRange = node.Data.uppers[idx].range;
                                        lock (overlapRange.overlapSetsId[i])
                                        {
                                                overlapRange.overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                                boundInSortedList.range.overlapSetsId[i].Add(overlapRange.entity.Id);
                                        }

                                    }
                            }


                            if (node.Data.covers.Count > 0)
                            {
                                lock (boundInSortedList.range.overlapSetsId[i])

                                    for (int idx = 0; idx < node.Data.covers.Count; idx++)
                                    {
                                        overlapRange = node.Data.covers[idx].range;
                                        lock (overlapRange.overlapSetsId[i])
                                        {
                                                boundInSortedList.range.overlapSetsId[i].Add(overlapRange.entity.Id);
                                                overlapRange.overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                        }
                                    }
                            }

                            if (l == tree.depth)
                            {
                                newIns.Add(boundInSortedList.range);
                            }
                        }
                        else if (boundInSortedList.isUpper == 1)
                        {
                            if (node.Data.lowers.Count > 0 && indexNode.Data.x < node.Data.lowers.Count)
                            {
                                boundInTree = node.Data.lowers[indexNode.Data.x];
                                while (boundInTree.boundValue < boundInSortedList.boundValue)
                                {
                                    Vector3Int temp = indexNode.Data;
                                    temp.x++;
                                    indexNode.Data = temp;
                                    if (indexNode.Data.x < node.Data.lowers.Count)
                                    {
                                        boundInTree = node.Data.lowers[indexNode.Data.x];
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            if (indexNode.Data.x > 0)
                            {
                                lock (boundInSortedList.range.overlapSetsId[i])

                                    for (int idx = 0; idx < indexNode.Data.x; idx++)
                                    {
                                        overlapRange = node.Data.lowers[idx].range;
                                        lock (overlapRange.overlapSetsId[i])
                                        {
                                                boundInSortedList.range.overlapSetsId[i].Add(overlapRange.entity.Id);
                                                overlapRange.overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                        }
                                    }
                            }
                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                            }
                        }
                    }
                    SortMatchInsideParallel(boundInSortedList, tree, indexTree, i, m, subset, upset, true, null);
                    j++;
                    m2 = m;
                }
                else
                {
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--)
                    {
                        indexNode = indexTree[l, k];
                        node = tree[l, k];
                        if (node.Data.lowers.Count > 0)
                        {
                            foreach (HSPDIMRange b in newIns)
                            {
                                lock (b.overlapSetsId[i])
                                {
                                    for (int idx = 0; idx < node.Data.lowers.Count; idx++)
                                    {
                                        overlapRange = node.Data.lowers[idx].range;
                                        lock (overlapRange.overlapSetsId[i])
                                        {
                                            b.overlapSetsId[i].Add(overlapRange.entity.Id);
                                            overlapRange.overlapSetsId[i].Add(b.entity.Id);
                                        }
                                    }
                                }

                            }
                        }
                        if (l == tree.depth)
                        {
                            foreach (HSPDIMRange b in newIns)
                            {
                                lock (b.overlapSetsId[i])
                                {
                                    if (m > IndexCal(b.Bounds[i, 0].boundValue, tree.depth))
                                    {
                                            for (int idx = 0; idx < node.Data.insides.Count; idx++)
                                            {
                                                overlapRange = node.Data.insides[idx].range;
                                                lock (overlapRange.overlapSetsId[i])
                                                {
                                                    b.overlapSetsId[i].Add(overlapRange.entity.Id);
                                                    overlapRange.overlapSetsId[i].Add(b.entity.Id);

                                                }
                                            }
                                    }
                                }
                            };
                            if (m == m2)
                            {
                                SortMatchInsideParallel(boundInSortedList, tree, indexTree, i, m, subset, upset, false, null);
                            }
                        }
                        if ((k + 1) % 2 == 0) k = k / 2; else break;
                    }
                    m++;
                }
            }
        }
        private void SortMatchInsideParallel(HSPDIMBound boundInSortedList, BinaryTree<HSPDIMTreeNodeData> tree, BinaryTree<Vector3Int> indexTree,
            int i, int m, List<HSPDIMRange> subset, List<HSPDIMRange> upset, bool headEnd, StringBuilder sb)
        {
            //sb.Append($"matching inside range at leaf {m} ");
            List<HSPDIMBound> insides = tree[tree.depth, m].Data.insides;
            int insidesCount = insides.Count;
            if (insidesCount > 0)
            {
                TreeNode<Vector3Int> indexNode = indexTree[tree.depth, m];
                int z = indexNode.Data.z;
                HSPDIMBound boundInTree;
                if (z < insidesCount)
                {
                    boundInTree = insides[z];
                    while (boundInTree.boundValue <= boundInSortedList.boundValue)
                    {
                        if (boundInTree.isUpper == -1)
                        {
                            subset.Add(boundInTree.range);
                        }
                        else if (boundInTree.isUpper == 1)
                        {
                            subset.Remove(boundInTree.range);
                            lock (boundInTree.range.overlapSetsId[i])

                                for (int j = 0, upsetCount = upset.Count; j < upsetCount; j++)
                                {
                                    {
                                        lock (upset[j].overlapSetsId[i])
                                        {

                                            upset[j].overlapSetsId[i].Add(boundInTree.range.entity.Id);
                                            boundInTree.range.overlapSetsId[i].Add(upset[j].entity.Id);
                                        }
                                    }
                                }
                        }
                        z++;
                        if (z < insidesCount)
                        {
                            boundInTree = insides[z];
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                indexNode.Data.z = z;
            }
            if (boundInSortedList.isUpper == -1)
            {
                if (headEnd) upset.Add(boundInSortedList.range);
            }
            else if (boundInSortedList.isUpper == 1)
            {
                if (headEnd) upset.Remove(boundInSortedList.range);
                if (subset.Count > 0)
                {
                    lock (boundInSortedList.range.overlapSetsId[i])

                        for (int j = 0, subsetCount = subset.Count; j < subsetCount; j++)
                        {
                            lock (subset[j].overlapSetsId[i])
                            {
                                {
                                    boundInSortedList.range.overlapSetsId[i].Add(subset[j].entity.Id);
                                    subset[j].overlapSetsId[i].Add(boundInSortedList.range.entity.Id);
                                }
                            }
                        }
                }
            }
        }
        private void DynamicMatching()
        {
            switch (Strategy)
            {
                case Strategy.SEQUENTIAL:
                    MatchingTreeToTree(upTree, subTree);
                    MatchingTreeToTree(subTree, upTree);
                    break;
                case Strategy.PARALEL_MUTUAL_REF:
                    MatchingTreeToTreeParallel(upTree, subTree);
                    MatchingTreeToTreeParallel(subTree, upTree);
                    break;
                case Strategy.PARALEL_REF:
                    MatchingTreeToTreeParallelRef(flattenUpTree, flattenSubTree, flattenedSortListSubTree, upTree, subTree, true);
                    MatchingTreeToTreeParallelRef(flattenSubTree, flattenUpTree, flattenedSortListUpTree, subTree, upTree, false);
                    flattenUpTree.Dispose();
                    flattenSubTree.Dispose();
                    flattenedSortListSubTree.Dispose();
                    flattenedSortListUpTree.Dispose();
                    break;
                case Strategy.PARALEL_ID_OUTPUT:
                    {
                        MatchingTreeToTreeParallelId(flattenUpTree, flattenSubTree, subTree, upTree);
                        MatchingTreeToTreeParallelId(flattenSubTree, flattenUpTree, upTree, subTree);

                        if (HSPDIM.Instance.debugId)
                        {
                            NativeArray<int4> sets = Result.ToNativeArray(Allocator.Temp);
                            HSPDIMEntities.ForEach(e =>
                            {
                                for (int i = 0; i < dimension; i++)
                                {
                                    e.Value.UpRange?.overlapSetsId[i].Clear();
                                    e.Value.SubRange?.overlapSetsId[i].Clear();
                                }
                            });
                            for (int idx = 0; idx < sets.Length; idx++)
                            {
                                int4 key = sets[idx];
                                if (key.w == 1)
                                {
                                    HSPDIMEntities[key.x].SubRange.overlapSetsId[key.z].Add(key.y);
                                    HSPDIMEntities[key.y].UpRange.overlapSetsId[key.z].Add(key.x);
                                }
                                else
                                {
                                    HSPDIMEntities[key.x].UpRange.overlapSetsId[key.z].Add(key.y);
                                    HSPDIMEntities[key.y].SubRange.overlapSetsId[key.z].Add(key.x);
                                }
                            }
                            sets.Dispose();
                        }
                        flattenUpTree.Dispose();
                        flattenSubTree.Dispose();

                        break;
                    }
                default:
                    break;
            }
            stopwatchMergeOverlap.Start();
            overlapCurrent = 0;
            foreach (var r in subRanges)
            {
                if (r.entity.Enable)
                {
                    r.UpdateIntersectionId();
                    overlapCurrent += r.overlapSetsId[0].Count + r.overlapSetsId[1].Count;
                    intersectTotal += r.intersectionId.Count;
                }
            }
            overlapTotal += overlapCurrent;
            stopwatchMergeOverlap.Stop();

        }
        private void RecalculateModifiedOverlapRef(HSPDIMRange[] upList, HSPDIMRange[] subList)
        {
            stopwatchRecalculateModifyOverlap.Start();
            if (IsDynamic)
            {
                foreach (var e in RemovedEntities)
                {
                    var entity = HSPDIMEntities[e];
                    var up = entity.UpRange;
                    var sub = entity.SubRange;
                    for (int i = 0; i < dimension; i++)
                    {
                        if (up !=null)
                        {
                            foreach (var r2 in up.overlapSetsId[i])
                            {
                                HSPDIMEntities[r2].SubRange.overlapSetsId[i].Remove(e);
                            }
                        }
                        if (sub != null)
                        {
                            foreach (var r2 in sub.overlapSetsId[i])
                            {
                                HSPDIMEntities[r2].UpRange.overlapSetsId[i].Remove(e);
                            }
                            sub?.overlapSetsId[i].Clear();
                        }
                        up?.overlapSetsId[i].Clear();
                    }
                    HSPDIMEntities.Remove(e);
                }
                foreach (var r in upList)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        if (r.entity.Modified[i])
                        {
                            foreach (var r2 in r.overlapSetsId[i])
                            {
                                HSPDIMEntities[r2].SubRange.overlapSetsId[i].Remove(r.entity.Id);
                            }
                        }
                    }
                }
                foreach (var r in subList)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        if (r.entity.Modified[i])
                        {
                            foreach (var r2 in r.overlapSetsId[i])
                            {
                               HSPDIMEntities[r2].UpRange.overlapSetsId[i].Remove(r.entity.Id);
                            }
                            r.overlapSetsId[i].Clear();
                        }
                    }
                }
                foreach (var r in upList)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        if (r.entity.Modified[i])
                        {
                            r.overlapSetsId[i].Clear();
                        }
                    }
                }
            }
            else
            {
                foreach (var r in upList)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        r.overlapSetsId[i].Clear();
                    }
                }
                foreach (var r in subList)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        r.overlapSetsId[i].Clear();
                    }
                }
            }
            RemovedEntities.Clear();
            stopwatchRecalculateModifyOverlap.Stop();
        }
        private void RecalculateModifiedOverlapId(HSPDIMRange[] upList, HSPDIMRange[] subList)
        {
            stopwatchRecalculateModifyOverlap.Start();
            if (!IsDynamic)
            {
                Result.Clear();
            }
            else
            {
                var resultInput = Result.ToNativeArray(Allocator.TempJob);
                NativeHashSet<int2> rangeIdModified = new(dimension * (modifiedUpRanges.Count + modifiedUpRanges.Count + RemovedEntities.Count), Allocator.TempJob);
                for (int j = 0; j < upList.Length; j++)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        var entity = upList[j].entity;
                        if (entity.Modified[i])
                        {
                            rangeIdModified.Add(new int2(entity.Id, i));
                        }
                    }
                }
                for (int j = 0; j < subList.Length; j++)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        var entity = subList[j].entity;
                        if (entity.Modified[i])
                        {
                            rangeIdModified.Add(new int2(entity.Id, i));
                        }
                    }
                }
                foreach (var e in RemovedEntities)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        rangeIdModified.Add(new int2(e, i));
                    }
                }

                if (rangeIdModified.Count > 0)
                {
                    ulong overlapMaxSize = (ulong)dimension * (ulong)(flattenUpTree.Lowers.Length + flattenUpTree.Uppers.Length + flattenUpTree.Insides.Length) * (ulong)(flattenSubTree.Lowers.Length + flattenSubTree.Uppers.Length + flattenSubTree.Insides.Length) / 4;
                    overlapMaxSize = Math.Min(overlapMaxSize, (ulong)(2147483646 * 0.4 / UnsafeUtility.SizeOf<int4>()));
                    //PDebug.Log($"rangeIdModified size = {rangeIdModified.Count}, Input size = {resultInput.Length},Size Allocation :{overlapMaxSize}, with each {UnsafeUtility.SizeOf<int4>()}, min {2147483647 / UnsafeUtility.SizeOf<int4>()}");
                    NativeParallelHashSet<int4> outputResult = new((int)overlapMaxSize, Allocator.Persistent);
                    RecalculateModifiedOverlapJob job = new()
                    {
                        ResultInput = resultInput,
                        ResultOutput = outputResult.AsParallelWriter(),
                        RangeIDModified = rangeIdModified,
                        threadCount = JobsUtility.JobWorkerCount
                    };
                    JobHandle jobHandle = job.Schedule(resultInput.Length, 16);
                    jobHandle.Complete();
                    Result.Dispose();
                    Result = outputResult;
                }
                rangeIdModified.Dispose();
                resultInput.Dispose();
            }
            RemovedEntities.Clear();
            stopwatchRecalculateModifyOverlap.Stop();
        }
        private void MatchingTreeToTreeParallelId(NativeHSPDIMFlattenedTree flattenedTree, NativeHSPDIMFlattenedTree flattenedSortListTree, BinaryTree<HSPDIMTreeNodeData>[] sortListTree, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            stopwatchMatching.Start();
            MatchingRangeToTreeIdJob2 job = new()
            {
                FlattenedSortListTree = flattenedSortListTree,
                FlattenTree = flattenedTree,
                Result = Result.AsParallelWriter(),
                dynamic = IsDynamic,
                //Message = logQueue.AsParallelWriter(),
            };
            JobHandle handle = job.Schedule(flattenedSortListTree.LowerNodes.Length, 1);
            handle.Complete();
            stopwatchMatching.Stop();
        }
        private void MatchingTreeToTreeParallelRef(NativeHSPDIMFlattenedTree flattenedTree, NativeHSPDIMFlattenedTree flattennedTree2, NativeHSPDIMFlattenedSortListTree flattenedSortListTree, BinaryTree<HSPDIMTreeNodeData>[] tree, BinaryTree<HSPDIMTreeNodeData>[] tree2, bool isUp)
        {
            ulong overlapMaxSize = (ulong)dimension * (ulong)(flattennedTree2.Lowers.Length + flattennedTree2.Uppers.Length + flattennedTree2.Insides.Length) * (ulong)(flattenedTree.Lowers.Length + flattenedTree.Uppers.Length + flattenedTree.Insides.Length) / 4 / 2;
            //PDebug.Log($"Size Allocation :{overlapMaxSize}, with each {UnsafeUtility.SizeOf<OverlapID>()}, min {2147483647 / UnsafeUtility.SizeOf<OverlapID>()}");
            overlapMaxSize = (ulong)Math.Min(overlapMaxSize, 2147483647f * 0.4f / UnsafeUtility.SizeOf<OverlapID>());
            overlapSet = new((int)overlapMaxSize, Allocator.TempJob);
            //var logQueue = new NativeQueue<FixedString128Bytes>(Allocator.TempJob);
            stopwatchMatching.Start();
            //MatchingRangeToTreeRefJob2 job = new()
            //{
            //    FlattenedSortListTree = flattennedTree2,
            //    FlattenTree = flattenedTree,
            //    OverlapSet = overlapSet.AsParallelWriter(),
            //    dynamic = IsDynamic,
            //};
            //JobHandle handle = job.Schedule(flattennedTree2.LowerNodes.Length, 1);
            MathcingRangeToTreeRefJob job = new()
            {
                FlattenedSortListTree = flattenedSortListTree,
                FlattenTree = flattenedTree,
                OverlapSet = overlapSet.AsParallelWriter(),
            };
            JobHandle handle = job.Schedule(flattenedSortListTree.ElementList.Length, 1);
            handle.Complete();
            stopwatchMatching.Stop();

            stopwatchOutput.Start();
            var array = overlapSet.AsArray();
            int length = array.Length;
            HSPDIMRange[] ranges = new HSPDIMRange[upRanges.Count];
            for (int j = 0; j < length; j++)
            {
                OverlapID overlap = array[j];
                int i = overlap.rangeIDInTree.Dim;
                overlap.MapRangeToTreeArray(tree[i], ranges);
                int id = overlap.rangeIDInSortedListTree;
                for (int x = 0; x < overlap.rangeIDInTree.Count; x++)
                {
                    var r = ranges[x];
                    if (isUp)
                    {
                        HSPDIMEntities[id].SubRange.overlapSetsId[i].Add(r.entity.Id);
                    }
                    else
                    {
                        HSPDIMEntities[id].UpRange.overlapSetsId[i].Add(r.entity.Id);

                    }
                    r.overlapSetsId[i].Add(id);
                }
            }
            stopwatchOutput.Stop();

            overlapSet.Dispose();
            //Debug.Log($"{string.Join("\n", Enumerable.Range(0, logQueue.Count).Select(_ => logQueue.Dequeue()))}");
            //logQueue.Dispose();

        }
        private void MatchingTreeToTree(BinaryTree<HSPDIMTreeNodeData>[] tree1, BinaryTree<HSPDIMTreeNodeData>[] tree2)
        {
            stopwatchMatching.Start();
            if (IsDynamic)
            {
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in tree1[i])
                    {
                        List<HSPDIMBound> crossNodeRanges = node.Data.lowers.FindAll(b => b.entity.Modified[i]);
                        int maxCurrentLevelIndex = (1<< node.depth);
                        if (crossNodeRanges.Count > 0 && node.index + 1 < maxCurrentLevelIndex)
                        {
                            var nodeRange = tree1[i][node.depth, node.index + 1].Data.uppers;
                            for (int j = 0; j < nodeRange.Count; j++)
                            {
                                if (nodeRange[j].range.entity.Modified[i] && nodeRange[j].range.Bounds[i, 0].index == node.index)
                                {
                                    crossNodeRanges.Add(nodeRange[j]);
                                }
                            }
                            if (node.index + 2 < maxCurrentLevelIndex)
                            {
                                nodeRange = tree1[i][node.depth, node.index + 2].Data.uppers;
                                for (int j = 0; j < nodeRange.Count; j++)
                                {
                                    if (nodeRange[j].range.entity.Modified[i] && nodeRange[j].range.Bounds[i, 0].index == node.index)
                                    {
                                        crossNodeRanges.Add(nodeRange[j]);
                                    }
                                }
                            }
                        }

                        if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                        {
                            Matching(node.Data.insides.FindAll(b => b.entity.Modified[i]), tree2[i], i);
                        }
                        if (crossNodeRanges.Count > 0)
                        {
                            Matching(crossNodeRanges, tree2[i], i);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in tree1[i])
                    {
                        List<HSPDIMBound> crossNodeRanges = new(node.Data.lowers);
                        int maxCurrentLevelIndex = (1<< node.depth);
                        if (crossNodeRanges.Count>0 && node.index + 1 < maxCurrentLevelIndex)
                        {
                            var nodeRange = tree1[i][node.depth, node.index + 1].Data.uppers;
                            for (int j = 0; j < nodeRange.Count; j++)
                            {
                                if (nodeRange[j].range.Bounds[i, 0].index == node.index)
                                {
                                    crossNodeRanges.Add(nodeRange[j]);
                                }
                            }
                            if (node.index + 2 < maxCurrentLevelIndex)
                            {
                                nodeRange = tree1[i][node.depth, node.index + 2].Data.uppers;
                                for (int j = 0; j < nodeRange.Count; j++)
                                {
                                    if (nodeRange[j].range.Bounds[i, 0].index == node.index)
                                    {
                                        crossNodeRanges.Add(nodeRange[j]);
                                    }
                                }
                            }
                        }

                        if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                        {
                            Matching(node.Data.insides, tree2[i], i);
                        }
                        if (crossNodeRanges != null)
                        {
                            Matching(crossNodeRanges, tree2[i], i);
                        }
                    }
                }
            }
            
            stopwatchMatching.Stop();
        }
        private void MatchingTreeToTreeParallel(BinaryTree<HSPDIMTreeNodeData>[] tree1, BinaryTree<HSPDIMTreeNodeData>[] tree2)
        {
            stopwatchMatching.Start();
            if (IsDynamic)
            {
                var nodePairs = Enumerable.Range(0, dimension).SelectMany(i => tree1[i].Select(node => (dim: i, node)));

                // Step 2: Process each (dimension, node) pair in parallel.
                Parallel.ForEach(nodePairs, pair =>
                {
                    int i = pair.dim;
                    var node = pair.node;

                    // Compute the cross-node ranges as before.
                    List<HSPDIMBound> crossNodeRanges = node.Data.lowers.FindAll(b => b.entity.Modified[i]);
                    int maxCurrentLevelIndex = (1 << node.depth);

                    if (crossNodeRanges.Count > 0 && node.index + 1 < maxCurrentLevelIndex)
                    {
                        var nodeRange = tree1[i][node.depth, node.index + 1].Data.uppers;
                        for (int j = 0; j < nodeRange.Count; j++)
                        {
                            if (nodeRange[j].range.entity.Modified[i] && nodeRange[j].range.Bounds[i, 0].index == node.index)
                            {
                                crossNodeRanges.Add(nodeRange[j]);
                            }
                        }
                        if (node.index + 2 < maxCurrentLevelIndex)
                        {
                            nodeRange = tree1[i][node.depth, node.index + 2].Data.uppers;
                            for (int j = 0; j < nodeRange.Count; j++)
                            {
                                if (nodeRange[j].range.entity.Modified[i] && nodeRange[j].range.Bounds[i, 0].index == node.index)
                                {
                                    crossNodeRanges.Add(nodeRange[j]);
                                }
                            }
                        }
                    }

                    if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                    {
                        Parallel.Invoke(
                            // Process the "insides" matching if applicable.
                            () =>
                            {

                                var insidesModified = node.Data.insides.FindAll(b => b.entity.Modified[i]);
                                MatchingParallel(insidesModified, tree2[i], i);
                            });
                    }
                    if (crossNodeRanges.Count > 0)
                    {
                        Parallel.Invoke(
                        () =>
                        {
                            {
                                MatchingParallel(crossNodeRanges, tree2[i], i);
                            }
                        });
                    }
                    ;
                });
            }
            else
            {
                var nodePairs = Enumerable.Range(0, dimension).SelectMany(i => tree1[i].Select(node => (dim: i, node)));
                Parallel.ForEach(nodePairs, pair =>
                {
                    int i = pair.dim;
                    var node = pair.node;
                    {
                        List<HSPDIMBound> crossNodeRanges = new(node.Data.lowers);
                        int maxCurrentLevelIndex = (1 << node.depth);
                        if (crossNodeRanges.Count > 0 && node.index + 1 < maxCurrentLevelIndex)
                        {
                            var nodeRange = tree1[i][node.depth, node.index + 1].Data.uppers;
                            for (int j = 0; j < nodeRange.Count; j++)
                            {
                                if (nodeRange[j].range.Bounds[i, 0].index == node.index)
                                {
                                    crossNodeRanges.Add(nodeRange[j]);
                                }
                            }
                            if (node.index + 2 < maxCurrentLevelIndex)
                            {
                                nodeRange = tree1[i][node.depth, node.index + 2].Data.uppers;
                                for (int j = 0; j < nodeRange.Count; j++)
                                {
                                    if (nodeRange[j].range.Bounds[i, 0].index == node.index)
                                    {
                                        crossNodeRanges.Add(nodeRange[j]);
                                    }
                                }
                            }
                        }
                        if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                        {
                            MatchingParallel(node.Data.insides, tree2[i], i);
                        }
                        if (crossNodeRanges.Count > 0)
                        {
                            MatchingParallel(crossNodeRanges, tree2[i], i);
                        }
                    }
                });
            
            }

            stopwatchMatching.Stop();
        }
        public static void MappingRanges(HSPDIMRange[] ranges, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            for (int j = 0; j < ranges.Length; j++)
            {
                var r = ranges[j];
                r.UpdateRange(tree[0].depth);
                for (short i = 0; i < dimension; i++)
                {
                    AddRangeToTree(i, r, tree);
                }
            }
            for (short i = 0; i < dimension; i++)
            {
                foreach (var node in tree[i])
                {
                    node.Data.lowers.Sort();
                    node.Data.uppers.Sort();
                    node.Data.insides.Sort();
                }
            }
        }
        public void MappingRangeDynamic(HSPDIMRange[] ranges, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            stopwatchMapping.Start();
            if (IsDynamic)
            {
                for (int j = 0; j < ranges.Length; j++)
                {
                    var r = ranges[j];
                    for (short i = 0; i < dimension; i++)
                    {
                        if (r.entity.Modified[i])
                        {
                            RemoveRangeFromTree(i, r, tree);
                        }
                    }
                    r.UpdateRange(tree[0].depth);
                    for (short i = 0; i < dimension; i++)
                    {
                        if (r.entity.Modified[i])
                        {
                            AddRangeToTreeInsertionBinary(i, r, tree);
                        }
                    }
                }
                //foreach (HSPDIMRange r in ranges)
                //{
                //    for (short i = 0; i < dimension; i++)
                //    {
                //        if (r.entity.Modified[i])
                //        {
                //            AddRangeToTree(i, r, tree);
                //        }
                //    }
                //}

                //for (short i = 0; i < dimension; i++)
                //{
                //    foreach (var node in tree[i])
                //    {
                //        if (node.Data.IsEmpty()) continue;
                //        node.Data.lowers.Sort();
                //        node.Data.uppers.Sort();
                //        node.Data.insides.Sort();
                //    }
                //}
            }
            else
            {
                ClearRangeTree(tree);
                for (int j = 0; j < ranges.Length; j++)
                {
                    var r = ranges[j];
                    r.UpdateRange(tree[0].depth);
                    for (short i = 0; i < dimension; i++)
                    {
                        AddRangeToTree(i, r, tree);
                    }
                }
                for (short i = 0; i < dimension; i++)
                {
                    foreach (var node in tree[i])
                    {
                        node.Data.lowers.Sort();
                        node.Data.uppers.Sort();
                        node.Data.insides.Sort();
                    }
                }
            }

            stopwatchMapping.Stop();
        }
        public static void ClearRangeTree(BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            for (short i = 0; i < dimension; i++)
            {
                foreach (var node in tree[i])
                {
                    node.Data.lowers.Clear();
                    node.Data.uppers.Clear();
                    node.Data.covers.Clear();
                    node.Data.insides.Clear();
                }
            }
        }
        public static void AddBoundToTree(HSPDIMBound lowerBound, HSPDIMBound upperBound, BinaryTree<HSPDIMTreeNodeData> tree, bool inside, HSPDIMBound coverBound = null)
        {
            //tree
            int dim = lowerBound.dimId;
            short depth = (short)lowerBound.range.depthLevel[dim];
            if (inside)
            {
                tree[depth, lowerBound.index].Data.insides.Add(lowerBound);
                tree[depth, lowerBound.index].Data.insides.Add(upperBound);
                tree[depth, lowerBound.index].Data.Insides.Add(lowerBound);
                tree[depth, lowerBound.index].Data.Insides.Add(upperBound);
            }
            else
            {
                tree[depth, lowerBound.index].Data.lowers.Add(lowerBound);
                tree[depth, upperBound.index].Data.uppers.Add(upperBound);
            }
            if (coverBound != null) tree[depth, coverBound.index].Data.covers.Add(coverBound);
        }
        public static void AddBoundToTreeInsertionBinary(HSPDIMBound lowerBound, HSPDIMBound upperBound, BinaryTree<HSPDIMTreeNodeData> tree, bool inside, HSPDIMBound coverBound = null)
        {
            //tree
            int dim = lowerBound.dimId;
            short depth = (short)lowerBound.range.depthLevel[dim];

            HSPDIMTreeNodeData lowerNode = tree[depth, lowerBound.index].Data;
            HSPDIMTreeNodeData upperNode = tree[depth, upperBound.index].Data;
            List<HSPDIMBound> lowerContainer, upperContainer;
            int lowerIndexInContainer, upperIndexInContainer;
            if (inside)
            {
                upperContainer = lowerContainer = lowerNode.insides;
            }
            else
            {
                lowerContainer = lowerNode.lowers;
                upperContainer = upperNode.uppers;
            }

            lowerIndexInContainer = lowerContainer.BinarySearch(lowerBound);
            if (lowerIndexInContainer < 0) lowerIndexInContainer = ~lowerIndexInContainer;
            lowerContainer.Insert(lowerIndexInContainer, lowerBound);
            upperIndexInContainer = upperContainer.BinarySearch(upperBound);
            if (upperIndexInContainer < 0) upperIndexInContainer = ~upperIndexInContainer;
            upperContainer.Insert(upperIndexInContainer, upperBound);
            if (coverBound != null) tree[depth, lowerBound.index].Data.covers.Add(coverBound);
        }
        public static void RemoveBoundFromTree(HSPDIMBound lowerBound, HSPDIMBound upperBound, BinaryTree<HSPDIMTreeNodeData> tree, bool inside, HSPDIMBound coverBound = null)
        {
            int dim = lowerBound.dimId;
            short depth = (short)lowerBound.range.depthLevel[dim];
            HSPDIMTreeNodeData lowerNode = tree[depth, lowerBound.index].Data;
            HSPDIMTreeNodeData upperNode = tree[depth, upperBound.index].Data;
            List<HSPDIMBound> lowerContainer, upperContainer;
            if (inside)
            {
                upperContainer = lowerContainer = lowerNode.insides;
            }
            else
            {
                lowerContainer = lowerNode.lowers;
                upperContainer = upperNode.uppers;
            }
            int lowerIndexInContainer = lowerContainer.BinarySearch(lowerBound);
            lowerContainer.RemoveAt(lowerIndexInContainer);
            int upperIndexInContainer = upperContainer.BinarySearch(upperBound);
            upperContainer.RemoveAt(upperIndexInContainer);

            if (coverBound != null)
            {
                tree[depth, lowerBound.index].Data.covers.Remove(coverBound);
                coverBound.index = -1;
            }
            lowerBound.index = -1;
            upperBound.index = -1;

        }
        public static void RemoveRangeFromTree(short i, HSPDIMRange range, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            if (range.Bounds[i, 0] == null) return;
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
            {
                if (range.Bounds[i, 1].index - range.Bounds[i, 0].index == 0 && range.depthLevel[i] == tree[i].depth)
                {
                    RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], true);
                }
                else
                {
                    if (range.Bounds[i, 1].index - range.Bounds[i, 0].index == 2)
                    {
                        RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], false, range.Bounds[i, 2]);
                    }
                    else
                    {
                        RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], false);
                    }
                }
            }
        }
        public static void AddRangeToTree(short i, HSPDIMRange range, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
            {
                HSPDIMBound lowerBound = range.Bounds[i, 0];
                HSPDIMBound upperBound = range.Bounds[i, 1];
                HSPDIMBound coverBound = range.Bounds[i, 2];
                if (upperBound.index - lowerBound.index == 0 && range.depthLevel[i] == tree[i].depth)
                {
                    AddBoundToTree(lowerBound, upperBound, tree[i], true);
                }
                else
                {
                    if (upperBound.index - lowerBound.index == 2)
                    {
                        AddBoundToTree(lowerBound, upperBound, tree[i], false, coverBound);
                    }
                    else
                    {
                        AddBoundToTree(lowerBound, upperBound, tree[i], false);
                    }
                }
            }
        }
        public static void AddRangeToTreeInsertionBinary(short i, HSPDIMRange range, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
            {
                HSPDIMBound lowerBound = range.Bounds[i, 0];
                HSPDIMBound upperBound = range.Bounds[i, 1];
                HSPDIMBound coverBound = range.Bounds[i, 2];
                if (upperBound.index - lowerBound.index == 0 && range.depthLevel[i] == tree[i].depth)
                {
                    AddBoundToTreeInsertionBinary(lowerBound, upperBound, tree[i], true);
                }
                else
                {
                    if (upperBound.index - lowerBound.index == 2)
                    {
                        AddBoundToTreeInsertionBinary(lowerBound, upperBound, tree[i], false, coverBound);
                    }
                    else
                    {
                        AddBoundToTreeInsertionBinary(lowerBound, upperBound, tree[i], false);
                    }
                }
            }
        }
        public void Dispose()
        {
            PDebug.Log("Dispose");
            Result.Dispose();
            flattenSubTree.DisposePersistent();
            flattenUpTree.DisposePersistent();
            flattenedSortListUpTree.DisposePersistent();
            flattenedSortListSubTree.DisposePersistent();
        }
        public static bool IsSorted<T>(List<T> list) where T : IComparable<T>
        {
            if (list == null || list.Count <= 1)
                return true; // A null or single-element list is considered sorted.

            for (int i = 1; i < list.Count; i++)
            {
                if (list[i - 1].CompareTo(list[i]) > 0)
                    return false; // If the previous element is greater, the list is not sorted.
            }

            return true; // All elements are in ascending order.
        }
        private static void LogTree(BinaryTree<HSPDIMTreeNodeData>[] tree1, BinaryTree<HSPDIMTreeNodeData>[] tree2)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Tree:\n");
            if (tree1 != null)
            {
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in tree1[i])
                    {
                        if (!node.Data.IsEmpty())
                        {
                            stringBuilder.AppendLine($"{node.Data} [{i},{node.depth},{node.index}]");
                        }
                    }
                }
            }


            stringBuilder.Append("SortListTree:\n");
            for (int i = 0; i < dimension; i++)
            {
                foreach (var node in tree2[i])
                {
                    if (!node.Data.IsEmpty())
                    {
                        stringBuilder.AppendLine($"{node.Data} [{i},{node.depth},{node.index}]");
                    }
                }
            }
            PDebug.Log(stringBuilder);
        }

        [BurstCompile]
        public struct MatchingRangeToTreeRefJob2 : IJobParallelFor
        {
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenedSortListTree;
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenTree;
            [ReadOnly] public bool dynamic;
            public NativeList<OverlapID>.ParallelWriter OverlapSet;
            public void Execute(int idx)
            {
                NativeNode lowerNode = FlattenedSortListTree.LowerNodes[idx];
                NativeNode insideNode = FlattenedSortListTree.InsideNodes[idx];
                int lowerCount = lowerNode.Count;
                int lowerStart = lowerNode.Start;
                int insideCount = insideNode.Count;
                int insideStart = insideNode.Start;
                if (lowerCount == 0 && insideCount == 0)
                {
                    return;
                }

                int d = -1;
                int p = idx;
                while (p >= 0)
                {
                    d++;
                    p -= FlattenedSortListTree.LowerDimensions[d].Count;
                }


                if (lowerCount > 0)
                {
                    var lowers = FlattenedSortListTree.Lowers;
                    NativeList<NativeBound> crossNodeRange = new(lowerStart * 2, Allocator.Temp);
                    for (int i = 0; i < lowerNode.Count; i++)
                    {
                        var range = lowers[lowerStart + i];
                        if (!dynamic || range.Modified)
                        {
                            crossNodeRange.Add(range);
                        }
                    }
                    int index = lowers[lowerStart].Index;
                    NativeNode uppernode1 = FlattenedSortListTree.UpperNodes[idx + 1];
                    var uppers = FlattenedSortListTree.Uppers;
                    var upper1Start = uppernode1.Start;
                    for (int i = 0; i < uppernode1.Count; i++)
                    {
                        var range = uppers[upper1Start + i];
                        if ((!dynamic || range.Modified) && range.LowerIndex == index)
                        {
                            crossNodeRange.Add(range);
                        }
                    }
                    if (idx + 2 < 1 << lowerNode.Depth)
                    {
                        NativeNode uppernode2 = FlattenedSortListTree.UpperNodes[idx + 2];
                        var upper2Start = uppernode2.Start;
                        for (int i = 0; i < uppernode2.Count; i++)
                        {
                            var range = uppers[i + upper2Start];
                            if ((!dynamic || range.Modified) && range.LowerIndex == index)
                            {
                                crossNodeRange.Add(range);
                            }
                        }
                    }
                    
                    if (crossNodeRange.Length > 0)
                    {
                        Matching(crossNodeRange, idx, d, FlattenTree.depth[d]);
                    }
                    crossNodeRange.Dispose();
                }

                if (insideNode.Count > 0)
                {
                    NativeList<NativeBound> insideNodeRange = new(insideNode.Count * 2, Allocator.Temp);
                    var insides = FlattenedSortListTree.Insides;
                    for (int i = 0; i < insideNode.Count; i++)
                    {
                        var range = insides[insideStart + i];
                        if (!dynamic || range.Modified)
                        {
                            insideNodeRange.Add(range);
                        }
                    }
                    if (insideNodeRange.Length > 0)
                    {
                        Matching(insideNodeRange, idx, d, FlattenTree.depth[d]);
                    }
                    insideNodeRange.Dispose();
                }
            }
            void Matching(NativeList<NativeBound> sortedListRange, int indexSortedListElemenet, int dimensionIndex, short treeDepth)
            {
                // Cache references to FlattenTree arrays
                var lowerNodes = FlattenTree.LowerNodes;
                var upperNodes = FlattenTree.UpperNodes;
                var coverNodes = FlattenTree.CoverNodes;
                var insideNodes = FlattenTree.InsideNodes;
                var lowers = FlattenTree.Lowers;
                var uppers = FlattenTree.Uppers;
                var covers = FlattenTree.Covers;
                var insides = FlattenTree.Insides;
                var upperDims = FlattenTree.UpperDimensions;

                int sortedCount = sortedListRange.Length;
                int endBoundIndex = sortedCount - 1;

                int totalNodes = (1 << (treeDepth + 1)) - 1;
                NativeArray<int3> indexTree = new(totalNodes, Allocator.Temp, NativeArrayOptions.ClearMemory);

                // Compute leaves
                int leftLeaf = IndexCal(sortedListRange[0].BoundValue, treeDepth);
                int rightLeaf = IndexCal(sortedListRange[endBoundIndex].BoundValue, treeDepth);

                // Temp lists
                NativeList<NativeBound> newIns = new(Allocator.Temp);
                FlatRedBlackTree<NativeBound> subset = new(Allocator.Temp);
                FlatRedBlackTree<NativeBound> upset = new(Allocator.Temp);

                int j = 0;
                int m = leftLeaf;
                int m2 = leftLeaf;
                int i = dimensionIndex;

                // Start node dimension
                int startNodeDimension = upperDims[dimensionIndex].Start;

                while (j <= endBoundIndex && m <= rightLeaf)
                {
                    NativeBound boundInSortedList = sortedListRange[j];
                    int leafIndex = IndexCal(boundInSortedList.BoundValue, treeDepth);

                    if (leafIndex == m)
                    {
                        for (short l = treeDepth, k = (short)m; l >= 0; l--, k = (short)(k / 2))
                        {
                            int nodeIndexInTree = (1 << l) + k - 1;
                            int nodeIndex = nodeIndexInTree + startNodeDimension;

                            if (boundInSortedList.IsUpper == -1)
                            {
                                NativeNode node = upperNodes[nodeIndex];
                                int nodeCount = node.Count;
                                int nodeStart = node.Start;
                                var idx3 = indexTree[nodeIndexInTree];
                                int y = idx3.y;

                                if (nodeCount > 0 && y < nodeCount)
                                {
                                    var boundInTree = uppers[nodeStart + y];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        y++;
                                        if (y < nodeCount)
                                            boundInTree = uppers[nodeStart + y];
                                        else
                                            break;
                                    }
                                    idx3.y = y;
                                    indexTree[nodeIndexInTree] = idx3;
                                }

                                if (nodeCount - indexTree[nodeIndexInTree].y > 0 && nodeCount > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, 0, 1, false, indexTree[nodeIndexInTree].y,
                                                        nodeCount - indexTree[nodeIndexInTree].y, true),
                                        boundInSortedList.Id
                                    ));
                                }

                                node = coverNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, 0, 0, false, 0, node.Count, true),
                                        boundInSortedList.Id
                                    ));
                                }

                                if (l == treeDepth)
                                {
                                    newIns.Add(boundInSortedList);
                                }
                            }
                            else if (boundInSortedList.IsUpper == 1)
                            {
                                NativeNode node = lowerNodes[nodeIndex];
                                int nodeCount = node.Count;
                                int nodeStart = node.Start;

                                var idx3 = indexTree[nodeIndexInTree];
                                int x = idx3.x;

                                if (nodeCount > 0 && x < nodeCount)
                                {
                                    var boundInTree = lowers[nodeStart + x];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        x++;
                                        if (x < nodeCount)
                                            boundInTree = lowers[nodeStart + x];
                                        else
                                            break;
                                    }
                                    idx3.x = x;
                                    indexTree[nodeIndexInTree] = idx3;
                                }

                                if (x > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, 0, -1, false, 0, x, true),
                                        boundInSortedList.Id
                                    ));
                                }

                                if (l == treeDepth)
                                {
                                    // Remove from newIns
                                    for (int idx = 0; idx < newIns.Length; idx++)
                                    {
                                        if (newIns[idx].Id == boundInSortedList.Id)
                                        {
                                            newIns.RemoveAt(idx);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        SortMatchInside(boundInSortedList, indexTree, dimensionIndex, m, startNodeDimension, subset, upset, insideNodes, insides, treeDepth, true);
                        j++;
                        m2 = m;
                    }
                    else
                    {
                        int newsLength = newIns.Length;

                        for (short l = treeDepth, k = (short)m; l >= 0; l--)
                        {
                            int nodeIndexInTree = (1 << l) + k - 1;
                            int nodeIndex = nodeIndexInTree + startNodeDimension;

                            NativeNode node = lowerNodes[nodeIndex];
                            int nodeCount = node.Count;
                            if (nodeCount > 0)
                            {
                                for (int q = 0; q < newsLength; q++)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, k, -1, false, 0, nodeCount, true),
                                        newIns[q].Id
                                    ));
                                }
                            }

                            if (l == treeDepth)
                            {
                                node = insideNodes[nodeIndex];
                                int inCount = node.Count;
                                if (inCount > 0)
                                {
                                    for (int q = 0; q < newsLength; q++)
                                    {
                                        if (m > IndexCal(newIns[q].BoundValue, treeDepth))
                                        {
                                            OverlapSet.AddNoResize(new OverlapID(
                                                new NativeBound(0, 0, false, i, l, k, k, 0, true, 0, inCount, true),
                                                newIns[q].Id
                                            ));
                                        }
                                    }
                                }

                                if (m == m2)
                                {
                                    SortMatchInside(boundInSortedList, indexTree, dimensionIndex, m, startNodeDimension, subset, upset, insideNodes, insides, treeDepth, false);
                                }
                            }
                            if (newsLength == 0) break;

                            if ((k + 1) % 2 == 0)
                                k = (short)(k / 2);
                            else
                                break;
                        }

                        m++;
                    }
                }

                newIns.Dispose();
                subset.Dispose();
                upset.Dispose();
                indexTree.Dispose();
            }
            private void SortMatchInside(NativeBound boundInSortedList, NativeArray<int3> indexTree, int DimensionIndex, int m, int startNodeDimension, FlatRedBlackTree<NativeBound> subset, FlatRedBlackTree<NativeBound> upset, NativeArray<NativeNode> insideNode, NativeArray<NativeBound> insides, short treeDepth, bool headEnd)
            {
                int nodeIndexInTree = (1 << treeDepth) + m - 1;
                int nodeIndex = nodeIndexInTree + startNodeDimension;

                NativeNode node = insideNode[nodeIndex];
                int nodeCount = node.Count;
                int nodeStart = node.Start;

                var idxVal = indexTree[nodeIndexInTree];
                int z = idxVal.z;

                if (nodeCount > 0 && z < nodeCount)
                {
                    NativeBound boundInTree = insides[nodeStart + z];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        if (boundInTree.IsUpper == -1)
                        {
                            subset.Insert(boundInTree);
                        }
                        else if (boundInTree.IsUpper == 1)
                        {
                            subset.Delete(boundInTree);
                            var upsetValues = new NativeList<NativeBound>(Allocator.Temp);
                            upset.InOrderTraversal(ref upsetValues);
                            for (int q = 0; q < upsetValues.Length; q++)
                            {
                                OverlapSet.AddNoResize(new OverlapID(boundInTree, upsetValues[q].Id));
                            }
                            upsetValues.Dispose();
                        }

                        z++;
                        if (z < nodeCount)
                            boundInTree = insides[nodeStart + z];
                        else
                            break;
                    }
                    idxVal.z = z;
                    indexTree[nodeIndexInTree] = idxVal;
                }

                // If the current bound is a lower bound
                if (boundInSortedList.IsUpper == -1)
                {
                    if (headEnd)
                    {
                        upset.Insert(boundInSortedList);
                    }
                }
                // If the current bound is an upper bound
                else if (boundInSortedList.IsUpper == 1)
                {
                    if (headEnd)
                    {
                        upset.Delete(boundInSortedList);
                    }
                    var subsetValues = new NativeList<NativeBound>(Allocator.Temp);
                    subset.InOrderTraversal(ref subsetValues);
                    for (int q = 0; q < subsetValues.Length; q++)
                    {
                        OverlapSet.AddNoResize(new OverlapID(subsetValues[q], boundInSortedList.Id));
                    }
                    subsetValues.Dispose();
                }
            }
        }
        [BurstCompile]
        public struct MathcingRangeToTreeRefJob : IJobParallelFor
        {
            [ReadOnly] public NativeHSPDIMFlattenedSortListTree FlattenedSortListTree;
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenTree;
            public NativeList<OverlapID>.ParallelWriter OverlapSet;
            //public NativeQueue<FixedString128Bytes>.ParallelWriter Message;

            public void Execute(int index)
            {
                NativeListElement element = FlattenedSortListTree.ElementList[index];
                if (element.Count == 0)
                {
                    return;
                }
                int d = -1;
                int p = index;
                while (p >= 0)
                {
                    d++;
                    p -= FlattenedSortListTree.ElementDimensions[d].Count;
                }
                if (d < 0 || d >= dimension) throw new Exception();
                Matching(element.Start, element.Count, index, d, FlattenTree.depth[d]);
            }
            void Matching(int startSortList, int countSortList, int indexSortedListElemenet, int dimensionIndex, short treeDepth)
            {
                // Cache references to FlattenTree arrays
                var lowerNodes = FlattenTree.LowerNodes;
                var upperNodes = FlattenTree.UpperNodes;
                var coverNodes = FlattenTree.CoverNodes;
                var insideNodes = FlattenTree.InsideNodes;
                var lowers = FlattenTree.Lowers;
                var uppers = FlattenTree.Uppers;
                var covers = FlattenTree.Covers;
                var insides = FlattenTree.Insides;
                var upperDims = FlattenTree.UpperDimensions;

                int endBoundIndex = startSortList + countSortList - 1;

                int totalNodes = (1 << (treeDepth + 1)) - 1;
                NativeArray<int3> indexTree = new NativeArray<int3>(totalNodes, Allocator.Temp, NativeArrayOptions.ClearMemory);

                // Compute leaves
                int leftLeaf = IndexCal(FlattenedSortListTree.Bounds[startSortList].BoundValue, treeDepth);
                int rightLeaf = IndexCal(FlattenedSortListTree.Bounds[endBoundIndex].BoundValue, treeDepth);

                // Temp lists
                NativeList<NativeBound> newIns = new(Allocator.Temp);
                FlatRedBlackTree<NativeBound> subset = new(Allocator.Temp);
                FlatRedBlackTree<NativeBound> upset = new(Allocator.Temp);

                int j = startSortList;
                int m = leftLeaf;
                int m2 = leftLeaf;
                int i = dimensionIndex;

                // Start node dimension
                int startNodeDimension = upperDims[dimensionIndex].Start;

                while (j <= endBoundIndex && m <= rightLeaf)
                {
                    NativeBound boundInSortedList = FlattenedSortListTree.Bounds[j];
                    int leafIndex = IndexCal(boundInSortedList.BoundValue, treeDepth);

                    if (leafIndex == m)
                    {
                        for (short l = treeDepth, k = (short)m; l >= 0; l--, k = (short)(k / 2))
                        {
                            int nodeIndexInTree = (1 << l) + k - 1;
                            int nodeIndex = nodeIndexInTree + startNodeDimension;

                            if (boundInSortedList.IsUpper == -1)
                            {
                                NativeNode node = upperNodes[nodeIndex];
                                int nodeCount = node.Count;
                                int nodeStart = node.Start;
                                var idx3 = indexTree[nodeIndexInTree];
                                int y = idx3.y;

                                if (nodeCount > 0 && y < nodeCount)
                                {
                                    var boundInTree = uppers[nodeStart + y];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        y++;
                                        if (y < nodeCount)
                                            boundInTree = uppers[nodeStart + y];
                                        else
                                            break;
                                    }
                                    idx3.y = y;
                                    indexTree[nodeIndexInTree] = idx3;
                                }

                                if (nodeCount - indexTree[nodeIndexInTree].y > 0 && nodeCount > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, 0, 1, false, indexTree[nodeIndexInTree].y,
                                                        nodeCount - indexTree[nodeIndexInTree].y, true),
                                        boundInSortedList.Id
                                    ));
                                }

                                node = coverNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, 0, 0, false, 0, node.Count, true),
                                        boundInSortedList.Id
                                    ));
                                }

                                if (l == treeDepth)
                                {
                                    newIns.Add(boundInSortedList);
                                }
                            }
                            else if (boundInSortedList.IsUpper == 1)
                            {
                                NativeNode node = lowerNodes[nodeIndex];
                                int nodeCount = node.Count;
                                int nodeStart = node.Start;

                                var idx3 = indexTree[nodeIndexInTree];
                                int x = idx3.x;

                                if (nodeCount > 0 && x < nodeCount)
                                {
                                    var boundInTree = lowers[nodeStart + x];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        x++;
                                        if (x < nodeCount)
                                            boundInTree = lowers[nodeStart + x];
                                        else
                                            break;
                                    }
                                    idx3.x = x;
                                    indexTree[nodeIndexInTree] = idx3;
                                }

                                if (x > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new NativeBound(0, 0, false, i, l, k, 0, -1, false, 0, x, true),
                                        boundInSortedList.Id
                                    ));
                                }

                                if (l == treeDepth)
                                {
                                    // Remove from newIns
                                    for (int idx = 0; idx < newIns.Length; idx++)
                                    {
                                        if (newIns[idx].Id == boundInSortedList.Id)
                                        {
                                            newIns.RemoveAt(idx);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        SortMatchInside(boundInSortedList, indexTree, i, m, startNodeDimension, subset, upset, insideNodes, insides, treeDepth, true);
                        j++;
                        m2 = m;
                    }
                    else
                    {
                        int newsLength = newIns.Length;

                        for (short l = treeDepth, k = (short)m; l >= 0; l--)
                        {
                            int nodeIndexInTree = (1 << l) + k - 1;
                            int nodeIndex = nodeIndexInTree + startNodeDimension;

                            NativeNode node = lowerNodes[nodeIndex];
                            int nodeCount = node.Count;
                            if (nodeCount > 0)
                            {
                                //for (int q = 0; q < newsLength; q++)
                                //{
                                //    OverlapSet.AddNoResize(new OverlapID(
                                //        new NativeBound(0, 0, false, i, l, k, 0, -1, false, 0, nodeCount, true),
                                //        newIns[q].Id
                                //    ));
                                //}
                            }

                            if (l == treeDepth)
                            {
                                node = insideNodes[nodeIndex];
                                int inCount = node.Count;
                                if (inCount > 0)
                                {
                                    //for (int q = 0; q < newsLength; q++)
                                    //{
                                    //    if (m > IndexCal(newIns[q].BoundValue, treeDepth))
                                    //    {
                                    //        OverlapSet.AddNoResize(new OverlapID(
                                    //            new NativeBound(0, 0, false, i, l, k, 0, 0, true, 0, inCount, true),
                                    //            newIns[q].Id
                                    //        ));
                                    //    }
                                    //}
                                }

                                //if (m == m2)
                                //{
                                //    SortMatchInside(boundInSortedList, indexTree, i, m, startNodeDimension, subset, upset, insideNodes, insides, treeDepth, false);
                                //}
                            }
                            if (newsLength == 0) break;

                            if ((k + 1) % 2 == 0)
                                k = (short)(k / 2);
                            else
                                break;
                        }

                        m++;
                    }
                }

                newIns.Dispose();
                subset.Dispose();
                upset.Dispose();
                indexTree.Dispose();
            }
            private void SortMatchInside(NativeBound boundInSortedList, NativeArray<int3> indexTree, int DimensionIndex, int m, int startNodeDimension, FlatRedBlackTree<NativeBound> subset, FlatRedBlackTree<NativeBound> upset, NativeArray<NativeNode> insideNode, NativeArray<NativeBound> insides, short treeDepth, bool headEnd)
            {
                int nodeIndexInTree = (1 << treeDepth) + m - 1;
                int nodeIndex = nodeIndexInTree + startNodeDimension;

                NativeNode node = insideNode[nodeIndex];
                int nodeCount = node.Count;
                int nodeStart = node.Start;

                var idxVal = indexTree[nodeIndexInTree];
                int z = idxVal.z;

                if (nodeCount > 0 && z < nodeCount)
                {
                    NativeBound boundInTree = insides[nodeStart + z];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        if (boundInTree.IsUpper == -1)
                        {
                            subset.Insert(boundInTree);
                        }
                        else if (boundInTree.IsUpper == 1)
                        {
                            subset.Delete(boundInTree);
                            var upsetValues = new NativeList<NativeBound>(Allocator.Temp);
                            //upset.InOrderTraversal(ref upsetValues);
                            //for (int q = 0; q < upsetValues.Length; q++)
                            //{
                            //    OverlapSet.AddNoResize(new OverlapID(boundInTree, upsetValues[q].Id));
                            //}
                            upsetValues.Dispose();
                        }

                        z++;
                        if (z < nodeCount)
                            boundInTree = insides[nodeStart + z];
                        else
                            break;
                    }
                    idxVal.z = z;
                    indexTree[nodeIndexInTree] = idxVal;
                }

                // If the current bound is a lower bound
                if (boundInSortedList.IsUpper == -1)
                {
                    if (headEnd)
                    {
                        upset.Insert(boundInSortedList);
                    }
                }
                // If the current bound is an upper bound
                else if (boundInSortedList.IsUpper == 1)
                {
                    if (headEnd)
                    {
                        upset.Delete(boundInSortedList);
                    }
                    var subsetValues = new NativeList<NativeBound>(Allocator.Temp);
                    //subset.InOrderTraversal(ref subsetValues);
                    //for (int q = 0; q < subsetValues.Length; q++)
                    //{
                    //    OverlapSet.AddNoResize(new OverlapID(subsetValues[q], boundInSortedList.Id));
                    //}
                    subsetValues.Dispose();
                }
            }
        }
        [BurstCompile]
        public struct MatchingRangeToTreeIdJob2 : IJobParallelFor
        {
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenedSortListTree;
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenTree;
            [ReadOnly] public bool dynamic;
            public NativeParallelHashSet<int4>.ParallelWriter Result;
            public void Execute(int idx)
            {
                NativeNode lowerNode = FlattenedSortListTree.LowerNodes[idx];
                NativeNode insideNode = FlattenedSortListTree.InsideNodes[idx];
                int lowerCount = lowerNode.Count;
                int lowerStart = lowerNode.Start;
                int insideCount = insideNode.Count;
                int insideStart = insideNode.Start;
                if (lowerCount == 0 && insideCount == 0)
                {
                    return;
                }

                int d = -1;
                int p = idx;
                while (p >= 0)
                {
                    d++;
                    p -= FlattenedSortListTree.LowerDimensions[d].Count;
                }


                if (lowerCount > 0)
                {
                    var lowers = FlattenedSortListTree.Lowers;
                    NativeList<NativeBound> crossNodeRange = new(lowerStart * 2, Allocator.Temp);
                    for (int i = 0; i < lowerNode.Count; i++)
                    {
                        var range = lowers[lowerStart + i];
                        if (!dynamic || range.Modified)
                        {
                            crossNodeRange.Add(range);
                        }
                    }
                    int index = lowers[lowerStart].Index;
                    NativeNode uppernode1 = FlattenedSortListTree.UpperNodes[idx + 1];
                    var uppers = FlattenedSortListTree.Uppers;
                    var upper1Start = uppernode1.Start;
                    for (int i = 0; i < uppernode1.Count; i++)
                    {
                        var range = uppers[upper1Start + i];
                        if ((!dynamic || range.Modified) && range.LowerIndex == index)
                        {
                            crossNodeRange.Add(range);
                        }
                    }
                    if (idx + 2 < 1 << lowerNode.Depth)
                    {
                        NativeNode uppernode2 = FlattenedSortListTree.UpperNodes[idx + 2];
                        var upper2Start = uppernode2.Start;
                        for (int i = 0; i < uppernode2.Count; i++)
                        {
                            var range = uppers[i + upper2Start];
                            if ((!dynamic || range.Modified) && range.LowerIndex == index)
                            {
                                crossNodeRange.Add(range);
                            }
                        }
                    }
                    if (crossNodeRange.Length > 0)
                    {
                        Matching(crossNodeRange, idx, d, FlattenTree.depth[d]);
                    }
                    crossNodeRange.Dispose();
                }

                if (insideNode.Count > 0)
                {
                    NativeList<NativeBound> insideNodeRange = new(insideNode.Count * 2, Allocator.Temp);
                    var insides = FlattenedSortListTree.Insides;
                    for (int i = 0; i < insideNode.Count; i++)
                    {
                        var range = insides[insideStart + i];
                        if (!dynamic || range.Modified)
                        {
                            insideNodeRange.Add(range);
                        }
                    }
                    if (insideNodeRange.Length > 0)
                    {
                        Matching(insideNodeRange, idx, d, FlattenTree.depth[d]);
                    }
                    insideNodeRange.Dispose();
                }
            }
            private void Matching(NativeList<NativeBound> sortedListRange, int indexSortedListElement,int dimensionIndex, short treeDepth )
            {
                var lowerNodes = FlattenTree.LowerNodes;
                var upperNodes = FlattenTree.UpperNodes;
                var coverNodes = FlattenTree.CoverNodes;
                var insideNodes = FlattenTree.InsideNodes;
                var lowers = FlattenTree.Lowers;
                var uppers = FlattenTree.Uppers;
                var covers = FlattenTree.Covers;
                var insides = FlattenTree.Insides;

                int endBoundIndex = sortedListRange.Length - 1;

                int totalNodes = (1 << (treeDepth + 1)) - 1;
                NativeArray<int3> indexTree = new(totalNodes, Allocator.Temp, NativeArrayOptions.ClearMemory);

                int leftLeaf = IndexCal(sortedListRange[0].BoundValue, treeDepth);
                int rightLeaf = IndexCal(sortedListRange[endBoundIndex].BoundValue, treeDepth);

                NativeList<NativeBound> newIns = new NativeList<NativeBound>(Allocator.Temp);
                FlatRedBlackTree<NativeBound> subset = new(Allocator.Temp);
                FlatRedBlackTree<NativeBound> upset = new(Allocator.Temp);

                int j = 0;
                int m2 = leftLeaf;
                int m = leftLeaf;
                int i = dimensionIndex;

                int startNodeDimension = FlattenTree.UpperDimensions[dimensionIndex].Start;

                while (j <= endBoundIndex && m <= rightLeaf)
                {
                    var boundInSortedList = sortedListRange[j];
                    int boundLeaf = IndexCal(boundInSortedList.BoundValue, treeDepth);

                    if (boundLeaf == m)
                    {
                        for (short l = treeDepth, k = (short)m; l >= 0; l--, k = (short)(k / 2))
                        {
                            int nodeIndexInTree = (1 << l) + k - 1;
                            int nodeIndex = nodeIndexInTree + startNodeDimension;

                            if (boundInSortedList.IsUpper == -1)
                            {
                                var node = upperNodes[nodeIndex];
                                int nodeCount = node.Count;
                                int nodeStart = node.Start;
                                var indexVal = indexTree[nodeIndexInTree];
                                int y = indexVal.y;
                                if (nodeCount > 0 && y < nodeCount)
                                {
                                    var boundInTree = uppers[nodeStart + y];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        y++;
                                        if (y < nodeCount)
                                            boundInTree = uppers[nodeStart + y];
                                        else
                                            break;
                                    }
                                    indexVal.y = y;
                                    indexTree[nodeIndexInTree] = indexVal;
                                }

                                if (nodeCount - indexTree[nodeIndexInTree].y > 0 && nodeCount > 0)
                                {
                                    for (int idx = nodeStart + indexTree[nodeIndexInTree].y; idx < nodeStart + nodeCount; idx++)
                                    {
                                        var upperBound = uppers[idx];
                                        if (upperBound.Id < boundInSortedList.Id)
                                            Result.Add(new int4(upperBound.Id, boundInSortedList.Id, i, boundInSortedList.IsSub ? 0 : 1));
                                        else
                                            Result.Add(new int4(boundInSortedList.Id, upperBound.Id, i, boundInSortedList.IsSub ? 1 : 0));
                                    }
                                }

                                var coverNode = coverNodes[nodeIndex];
                                int coverCount = coverNode.Count;
                                int coverStart = coverNode.Start;
                                if (coverCount > 0)
                                {
                                    for (int idx = coverStart; idx < coverStart + coverCount; idx++)
                                    {
                                        var coverBound = covers[idx];
                                        if (coverBound.Id < boundInSortedList.Id)
                                            Result.Add(new int4(coverBound.Id, boundInSortedList.Id, i, boundInSortedList.IsSub ? 0 : 1));
                                        else
                                            Result.Add(new int4(boundInSortedList.Id, coverBound.Id, i, boundInSortedList.IsSub ? 1 : 0));
                                    }
                                }

                                // If we’re at the top level, add to newIns
                                if (l == treeDepth)
                                {
                                    newIns.Add(boundInSortedList);
                                }
                            }
                            else if (boundInSortedList.IsUpper == 1)
                            {
                                var node = lowerNodes[nodeIndex];
                                int nodeCount = node.Count;
                                int nodeStart = node.Start;

                                var indexVal = indexTree[nodeIndexInTree];
                                int x = indexVal.x;
                                if (nodeCount > 0 && x < nodeCount)
                                {
                                    var boundInTree = lowers[nodeStart + x];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        x++;
                                        if (x < nodeCount)
                                            boundInTree = lowers[nodeStart + x];
                                        else
                                            break;
                                    }
                                    indexVal.x = x;
                                    indexTree[nodeIndexInTree] = indexVal;
                                }

                                if (x > 0)
                                {
                                    for (int idx = nodeStart; idx < nodeStart + x; idx++)
                                    {
                                        var lowerBound = lowers[idx];
                                        if (lowerBound.Id < boundInSortedList.Id)
                                            Result.Add(new int4(lowerBound.Id, boundInSortedList.Id, i, boundInSortedList.IsSub ? 0 : 1));
                                        else
                                            Result.Add(new int4(boundInSortedList.Id, lowerBound.Id, i, boundInSortedList.IsSub ? 1 : 0));
                                    }
                                }

                                if (l == treeDepth)
                                {
                                    for (int idx = 0; idx < newIns.Length; idx++)
                                    {
                                        if (newIns[idx].Id == boundInSortedList.Id)
                                        {
                                            newIns.RemoveAt(idx);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        SortMatchInside(boundInSortedList, indexTree, dimensionIndex, m, subset, upset, treeDepth, true);

                        j++;
                        m2 = m;
                    }
                    else
                    {
                        int newsLength = newIns.Length;
                        for (short l = treeDepth, k = (short)m; l >= 0; l--)
                        {
                            int nodeIndexInTree = (1 << l) + k - 1;
                            int nodeIndex = nodeIndexInTree + startNodeDimension;

                            var node = lowerNodes[nodeIndex];
                            int nodeCount = node.Count;
                            int nodeStart = node.Start;
                            if (nodeCount > 0)
                            {
                                for (int q = 0; q < newsLength; q++)
                                {
                                    var newInsBound = newIns[q];
                                    for (int idx = nodeStart; idx < nodeStart + nodeCount; idx++)
                                    {
                                        var lowerBound = lowers[idx];
                                        if (lowerBound.Id < newInsBound.Id)
                                            Result.Add(new int4(lowerBound.Id, newInsBound.Id, i, lowerBound.IsSub ? 1 : 0));
                                        else
                                            Result.Add(new int4(newInsBound.Id, lowerBound.Id, i, lowerBound.IsSub ? 0 : 1));
                                    }
                                }
                            }
                            if (l == treeDepth)
                            {
                                var inNode = insideNodes[nodeIndex];
                                int inCount = inNode.Count;
                                int inStart = inNode.Start;

                                if (inCount > 0)
                                {
                                    for (int q = 0; q < newsLength; q++)
                                    {
                                        var newInsBound = newIns[q];
                                        if (m > IndexCal(newInsBound.BoundValue, treeDepth))
                                        {
                                            for (int idx = inStart; idx < inStart + inCount; idx++)
                                            {
                                                var insideBound = insides[idx];
                                                if (insideBound.Id < newInsBound.Id)
                                                    Result.Add(new int4(insideBound.Id, newInsBound.Id, i, insideBound.IsSub ? 1 : 0));
                                                else
                                                    Result.Add(new int4(newInsBound.Id, insideBound.Id, i, insideBound.IsSub ? 0 : 1));
                                            }
                                        }
                                    }
                                }

                                if (m == m2)
                                {
                                    SortMatchInside(sortedListRange[j], indexTree, dimensionIndex, m, subset, upset, treeDepth, false);
                                }
                                if (newsLength == 0) break;

                            }
                            if ((k + 1) % 2 == 0)
                                k = (short)(k / 2);
                            else
                                break;
                        }
                        m++;
                    }
                }

                newIns.Dispose();
                subset.Dispose();
                upset.Dispose();
                indexTree.Dispose();
            }

            private void SortMatchInside(NativeBound boundInSortedList,NativeArray<int3> indexTree, int dimensionIndex, int m, FlatRedBlackTree<NativeBound> subset, FlatRedBlackTree<NativeBound> upset, short treeDepth, bool headEnd)
            {
                var insideNodes = FlattenTree.InsideNodes;
                var insides = FlattenTree.Insides;

                int nodeIndexInTree = (1 << treeDepth) + m - 1;
                int nodeIndex = nodeIndexInTree + FlattenTree.UpperDimensions[dimensionIndex].Start;

                var node = insideNodes[nodeIndex];
                int nodeCount = node.Count;
                int nodeStart = node.Start;

                var idxVal = indexTree[nodeIndexInTree];
                int z = idxVal.z;

                if (nodeCount > 0 && z < nodeCount)
                {
                    var boundInTree = insides[nodeStart + z];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        if (boundInTree.IsUpper == -1)
                        {
                            subset.Insert(boundInTree);
                        }
                        else if (boundInTree.IsUpper == 1)
                        {
                            subset.Delete(boundInTree);
                            var upsetValues = new NativeList<NativeBound>(Allocator.Temp);
                            upset.InOrderTraversal(ref upsetValues);
                            for (int q = 0; q < upsetValues.Length; q++)
                            {
                                var up = upsetValues[q];
                                if (boundInTree.Id < up.Id)
                                    Result.Add(new int4(boundInTree.Id, up.Id, dimensionIndex, boundInTree.IsSub ? 1 : 0));
                                else
                                    Result.Add(new int4(up.Id, boundInTree.Id, dimensionIndex, boundInTree.IsSub ? 0 : 1));
                            }
                            upsetValues.Dispose();
                        }

                        z++;
                        if (z < nodeCount)
                            boundInTree = insides[nodeStart + z];
                        else
                            break;
                    }
                    idxVal.z = z;
                    indexTree[nodeIndexInTree] = idxVal;
                }
                if (boundInSortedList.IsUpper == -1)
                {
                    if (headEnd)
                    {
                        upset.Insert(boundInSortedList);
                    }
                }
                else if (boundInSortedList.IsUpper == 1)
                {
                    if (headEnd)
                    {
                        upset.Delete(boundInSortedList);
                    }
                    var subsetValues = new NativeList<NativeBound>(Allocator.Temp);
                    subset.InOrderTraversal(ref subsetValues);
                    for (int q = 0; q < subsetValues.Length; q++)
                    {
                        var sub = subsetValues[q];
                        if (sub.Id < boundInSortedList.Id)
                            Result.Add(new int4(sub.Id, boundInSortedList.Id, dimensionIndex, boundInSortedList.IsSub ? 0 : 1));
                        else
                            Result.Add(new int4(boundInSortedList.Id, sub.Id, dimensionIndex, boundInSortedList.IsSub ? 1 : 0));
                    }
                    subsetValues.Dispose();

                }
            }
        }
        [BurstCompile]
        public struct RecalculateModifiedOverlapJob : IJobParallelFor
        {
            [ReadOnly] public int threadCount;
            [ReadOnly] public NativeArray<int4> ResultInput;
            public NativeParallelHashSet<int4>.ParallelWriter ResultOutput;
            [ReadOnly] public NativeHashSet<int2> RangeIDModified;

            public void Execute(int index)
            {
                int4 overlap = ResultInput[index];
                // Create keys for both objects: (object id, dimension)
                int2 key1 = new int2(overlap.x, overlap.z);
                int2 key2 = new int2(overlap.y, overlap.z);

                // If either object is modified for this dimension, skip this overlap.
                if (RangeIDModified.Contains(key1) || RangeIDModified.Contains(key2))
                    return;

                ResultOutput.Add(overlap);
            }
        }
        [BurstCompile]
        public struct SortParallel : IJob
        {
            public NativeList<int> data;
            public int left;
            public int right;

            public void Execute()
            {
                int length = right - left + 1;
                NativeArray<int> subArray = data.AsArray().GetSubArray(left, length);
                subArray.Sort();
            }
        }
    }
}