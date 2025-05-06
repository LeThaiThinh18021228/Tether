using FishNet;
using Framework.ADS;
using Framework.FishNet;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
namespace Framework.HSPDIMAlgo
{
    public enum Strategy
    {
        SEQUENTIAL,
        PARALEL_MUTUAL_REF,
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
        public List<int> RemovedEntities = new();
        public NativeParallelHashSet<int3>[] Result = new NativeParallelHashSet<int3>[dimension];
        public bool IsDynamicMapping;
        public bool IsDynamicMatching;
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
                var uplist = IsDynamicMatching ? modifiedUpRanges.ToArray() : upRanges.ToArray();
                var sublist = IsDynamicMatching ? modifiedSubRanges.ToArray() : subRanges.ToArray();
                MappingRangeDynamic(uplist, upTree, ref flattenUpTree);
                MappingRangeDynamic(sublist, subTree, ref flattenSubTree);
                if (Strategy == Strategy.PARALEL_ID_OUTPUT)
                {
                    ConvertFlattenedSortListTreeId(upRanges.Count, uplist.Length, upTree, ref flattenUpTree);
                    ConvertFlattenedSortListTreeId(subRanges.Count, sublist.Length, subTree, ref flattenSubTree);
                    SortRange(flattenUpTree, upTree);
                    SortRange(flattenSubTree, subTree);
                }
                //LogTree(upTree, subTree);
                //PDebug.Log(flattenUpTree);
                //PDebug.Log(flattenSubTree);
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
                    var up = uplist[i];
                    up.entity.Modified = Vector3Bool.@false;
                    if (IsDynamicMatching)
                        for (int j = 0; j < dimension; j++)
                        {
                            up.Boundss[j, 0].Modified = false;
                            up.Boundss[j, 1].Modified = false;
                            up.Boundss[j, 2].Modified = false;

                        }
                }
                    for (int i = 0; i < sublist.Length; i++)
                    {
                        var sub = sublist[i];
                        sub.entity.Modified = Vector3Bool.@false;
                        if (IsDynamicMatching)
                            for (int j = 0; j < dimension; j++)
                            {
                                sub.Boundss[j, 0].Modified = false;
                                sub.Boundss[j, 1].Modified = false;
                                sub.Boundss[j, 2].Modified = false;
                            }
                    }
                    if (IsDynamicMatching)
                    {
                        for (int i = 0; i < dimension; i++)
                        {
                            foreach (var node in upTree[i])
                            {
                                ref var lower = ref node.Data.Lowers;
                                for (int k = 0; k < lower.Length; k++)
                                {
                                    var b = lower[k];
                                    b.Modified = false;
                                    lower[k] = b;
                                }

                                ref var upper = ref node.Data.Uppers;
                                for (int k = 0; k < upper.Length; k++)
                                {
                                    var b = upper[k];
                                    b.Modified = false;
                                    upper[k] = b;
                                }

                                ref var inside = ref node.Data.Insides;
                                for (int k = 0; k < inside.Length; k++)
                                {
                                    var b = inside[k];
                                    b.Modified = false;
                                    inside[k] = b;
                                }

                                ref var cover = ref node.Data.Covers;
                                for (int k = 0; k < cover.Length; k++)
                                {
                                    var b = cover[k];
                                    b.Modified = false;
                                    cover[k] = b;
                                }
                            }
                            foreach (var node in subTree[i])
                            {
                                ref var lower = ref node.Data.Lowers;
                                for (int k = 0; k < lower.Length; k++)
                                {
                                    var b = lower[k];
                                    b.Modified = false;
                                    lower[k] = b;
                                }

                                ref var upper = ref node.Data.Uppers;
                                for (int k = 0; k < upper.Length; k++)
                                {
                                    var b = upper[k];
                                    b.Modified = false;
                                    upper[k] = b;
                                }

                                ref var inside = ref node.Data.Insides;
                                for (int k = 0; k < inside.Length; k++)
                                {
                                    var b = inside[k];
                                    b.Modified = false;
                                    inside[k] = b;
                                }

                                ref var cover = ref node.Data.Covers;
                                for (int k = 0; k < cover.Length; k++)
                                {
                                    var b = cover[k];
                                    b.Modified = false;
                                    cover[k] = b;
                                }
                            }
                        }

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
            var uplist = upRanges.ToArray();
            var sublist = subRanges.ToArray();
            MappingRangeDynamic(uplist, upTree, ref flattenUpTree);
            MappingRangeDynamic(sublist, subTree, ref flattenSubTree);
            if (Strategy == Strategy.PARALEL_ID_OUTPUT)
            {
                InitFlatteningTreeId(uplist.Length, uplist.Length, upTree, ref flattenUpTree);
                InitFlatteningTreeId(sublist.Length, sublist.Length, subTree, ref flattenSubTree);
                SortRange(flattenUpTree, upTree);
                SortRange(flattenSubTree, subTree);

                ulong overlapMaxSize = (ulong)(flattenUpTree.Lowers.Length + flattenUpTree.Uppers.Length + flattenUpTree.Insides.Length) * (ulong)(flattenSubTree.Lowers.Length + flattenSubTree.Uppers.Length + flattenSubTree.Insides.Length) / 8;
                overlapMaxSize = Math.Min(overlapMaxSize, (ulong)(2147483646 * 0.15f / UnsafeUtility.SizeOf<int3>()));
                for (int i = 0; i < dimension; i++)
                {
                    Result[i] = new NativeParallelHashSet<int3>((int)overlapMaxSize, Allocator.Persistent);
                }
            }
            //LogTree(upTree, sortListUpTree);
            //LogTree(subTree, sortListSubTree);
            //PDebug.Log(flattenUpTree);
            //PDebug.Log(flattenSubTree);
            MatchingTreeToTreeParallelId(flattenUpTree, flattenSubTree);
            flattenUpTree.Dispose();
            flattenSubTree.Dispose();
            if (debugId)
            {
                for (int j = 0; j < subRanges.Count; j++)
                {
                    subRanges[j].UpdateIntersectionId();
                }
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
        void InitFlatteningTreeId(int length, int modifiedLength, BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenedTree)
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

            int startLower = 0;
            int startUpper = 0;
            int startCover = 0;
            int startInside = 0;
            int startLowerDimension = 0;
            int startUpperDimension = 0;
            int startCoverDimension = 0;
            int startInsideDimension = 0;
            flattenedTree.Lowers = new NativeArray<NativeBound>(flattenedTree.lowerCount, Allocator.TempJob);
            flattenedTree.Uppers = new NativeArray<NativeBound>(flattenedTree.lowerCount, Allocator.TempJob);
            flattenedTree.Covers = new NativeArray<NativeBound>(flattenedTree.coverCount, Allocator.TempJob);
            flattenedTree.Insides = new NativeArray<NativeBound>(flattenedTree.insideCount, Allocator.TempJob);
            for (short i = 0; i < dimension; i++)
            {
                int totalNodeDimI = (1 << (tree[i].depth + 1)) - 1;
                foreach (var node in tree[i])
                {
                    int index = (1 << node.depth) + node.index - 1 + startInsideDimension;

                    var lowers = node.Data.Lowers;
                    int lowersCount = lowers.Length;
                    if (lowersCount > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(i, node.depth, node.index, startLower, lowersCount);
                        NativeArray<NativeBound>.Copy(lowers.AsArray(), 0, flattenedTree.Lowers, startLower, lowersCount);
                        startLower += lowersCount;
                    }
                    else
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(i, node.depth, node.index, startLower, 0);
                    }

                    var uppers = node.Data.Uppers;
                    int uppersCount = uppers.Length;
                    if (uppersCount > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(i, node.depth, node.index, startUpper, uppersCount);
                        NativeArray<NativeBound>.Copy(uppers.AsArray(), 0, flattenedTree.Uppers, startUpper, uppersCount);
                        startUpper += uppersCount;
                    }
                    else
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(i, node.depth, node.index, startUpper, 0);
                    }

                    var covers = node.Data.Covers;
                    int coversCount = covers.Length;
                    if (coversCount > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(i, node.depth, node.index, startCover, coversCount);
                        NativeArray<NativeBound>.Copy(covers.AsArray(), 0, flattenedTree.Covers, startCover, coversCount);
                        startCover += coversCount;
                    }
                    else
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(i, node.depth, node.index, startCover, 0);
                    }

                    var insides = node.Data.Insides;
                    int insidesCount = insides.Length;
                    if (insidesCount > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(i, node.depth, node.index, startInside, insidesCount);
                        NativeArray<NativeBound>.Copy(insides.AsArray(), 0, flattenedTree.Insides, startInside, insidesCount);
                        startInside += insidesCount;
                    }
                    else
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(i, node.depth, node.index, startInside, 0);
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

        }
        void ConvertFlattenedSortListTreeId(int length, int modifiedLength, BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenedTree)
        {
            stopwatchInput.Start();
            int startLower = 0;
            int startUpper = 0;
            int startCover = 0;
            int startInside = 0;
            int startDimension = 0;
            flattenedTree.Lowers = new NativeArray<NativeBound>(flattenedTree.lowerCount, Allocator.TempJob);
            flattenedTree.Uppers = new NativeArray<NativeBound>(flattenedTree.lowerCount, Allocator.TempJob);
            flattenedTree.Covers = new NativeArray<NativeBound>(flattenedTree.coverCount, Allocator.TempJob);
            flattenedTree.Insides = new NativeArray<NativeBound>(flattenedTree.insideCount, Allocator.TempJob);
            for (short i = 0; i < dimension; i++)
            {
                int totalNodeDimI = (1 << (tree[i].depth + 1)) - 1;
                foreach (var node in tree[i])
                {
                    int index = (1 << node.depth) + node.index - 1 + startDimension;
                    var lowers = node.Data.Lowers;
                    int lowersCount = lowers.Length;
                    if (lowersCount > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(i, node.depth, node.index, startLower, lowersCount);
                        NativeArray<NativeBound>.Copy(lowers.AsArray(), 0, flattenedTree.Lowers, startLower, lowersCount);
                        startLower += lowersCount;
                    }
                    else
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(i, node.depth, node.index, startLower, 0);
                    }

                    var uppers = node.Data.Uppers;
                    int uppersCount = uppers.Length;
                    if (uppersCount > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(i, node.depth, node.index, startUpper, uppersCount);
                        NativeArray<NativeBound>.Copy(uppers.AsArray(), 0, flattenedTree.Uppers, startUpper, uppersCount);
                        startUpper += uppersCount;
                    }
                    else
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(i, node.depth, node.index, startUpper, 0);
                    }

                    var covers = node.Data.Covers;
                    int coversCount = covers.Length;
                    if (coversCount > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(i, node.depth, node.index, startCover, coversCount);
                        NativeArray<NativeBound>.Copy(covers.AsArray(), 0, flattenedTree.Covers, startCover, coversCount);
                        startCover += coversCount;
                    }
                    else
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(i, node.depth, node.index, startCover, 0);
                    }

                    var insides = node.Data.Insides;
                    int insidesCount = insides.Length;
                    if (insidesCount > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(i, node.depth, node.index, startInside, insidesCount);
                        NativeArray<NativeBound>.Copy(insides.AsArray(), 0, flattenedTree.Insides, startInside, insidesCount);
                        startInside += insidesCount;
                    }
                    else
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(i, node.depth, node.index, startInside, 0);
                    }
                }
                ;
                flattenedTree.LowerDimensions[i] = flattenedTree.UpperDimensions[i] = flattenedTree.CoverDimensions[i] = flattenedTree.InsideDimensions[i] = new NativeListElement(startDimension, totalNodeDimI);
                startDimension += totalNodeDimI;
            }
            stopwatchInput.Stop();
        }
        private void SortRange(NativeHSPDIMFlattenedTree flattenTree, BinaryTree<HSPDIMTreeNodeData>[] tree)
        {
            stopwatchInput.Start();
            SortParallelJob job = new()
            {
                FlattenedSortListTree = flattenTree,
                isDynamic = IsDynamicMapping
            };
            JobHandle jobHandle = job.Schedule(flattenTree.LowerNodes.Length, 1);
            jobHandle.Complete();

            if (IsDynamicMapping)
                for (int i = 0; i < dimension; i++)
                {
                    int lowersStart = flattenTree.LowerDimensions[i].Start;
                    // Enumerate every node in tree[dim]
                    foreach (var node in tree[i])
                    {
                        int idxTree = ((1 << node.depth) + node.index - 1);
                        int idx = idxTree + lowersStart;
                        var ln = flattenTree.LowerNodes[idx];
                        if (ln.Count > 0)
                            NativeArray<NativeBound>.Copy(flattenTree.Lowers, ln.Start,node.Data.Lowers.AsArray(), 0,ln.Count);
                        var un = flattenTree.UpperNodes[idx];
                        if (un.Count > 0)
                            NativeArray<NativeBound>.Copy(flattenTree.Uppers, un.Start, node.Data.Uppers.AsArray(), 0, un.Count);

                        var inn = flattenTree.InsideNodes[idx];
                        if (inn.Count > 0)
                            NativeArray<NativeBound>.Copy(flattenTree.Insides, inn.Start, node.Data.Insides.AsArray(), 0, inn.Count);
                    }
                }
            stopwatchInput.Stop();
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
                    if (IsDynamicMatching)
                        MatchingTreeToTree(subTree, upTree);
                    break;
                case Strategy.PARALEL_MUTUAL_REF:
                    MatchingTreeToTreeParallel(upTree, subTree);
                    if (IsDynamicMatching)
                        MatchingTreeToTreeParallel(subTree, upTree);
                    break;
                case Strategy.PARALEL_ID_OUTPUT:
                    {
                        MatchingTreeToTreeParallelId(flattenUpTree, flattenSubTree);
                        if (IsDynamicMatching)
                            MatchingTreeToTreeParallelId(flattenSubTree, flattenUpTree);

                        if (HSPDIM.Instance.debugId)
                        {
                            HSPDIMEntities.ForEach(e =>
                            {
                                for (int i = 0; i < dimension; i++)
                                {
                                    e.Value.UpRange?.overlapSetsId[i].Clear();
                                    e.Value.SubRange?.overlapSetsId[i].Clear();
                                }
                            });
                            for (int i = 0; i < dimension; i++)
                            {
                                var set = Result[i].ToNativeArray(Allocator.Temp);
                                for (int idx = 0; idx < set.Length; idx++)
                                {
                                    int3 key = set[idx];
                                    if (key.z == 1)
                                    {
                                        HSPDIMEntities[key.x].SubRange.overlapSetsId[i].Add(key.y);
                                        HSPDIMEntities[key.y].UpRange.overlapSetsId[i].Add(key.x);
                                    }
                                    else
                                    {
                                        HSPDIMEntities[key.x].UpRange.overlapSetsId[i].Add(key.y);
                                        HSPDIMEntities[key.y].SubRange.overlapSetsId[i].Add(key.x);
                                    }
                                }
                                set.Dispose();
                            }
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
            if (debugId)
            {
                foreach (var r in subRanges)
                {
                    if (r.entity.Enable)
                    {
                        r.UpdateIntersectionId();
                        overlapCurrent += r.overlapSetsId[0].Count + r.overlapSetsId[1].Count;
                        intersectTotal += r.intersectionId.Count;
                    }
                }
            }
            overlapTotal += overlapCurrent;
            stopwatchMergeOverlap.Stop();

        }
        private void RecalculateModifiedOverlapRef(HSPDIMRange[] upList, HSPDIMRange[] subList)
        {
            stopwatchRecalculateModifyOverlap.Start();
            if (IsDynamicMatching)
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
            if (!IsDynamicMatching)
            {
                for (int i = 0; i < dimension; i++)
                    Result[i].Clear();
            }
            else
            {
                for (int i = 0; i < dimension; i++)
                {
                    var  resultInput = Result[i].ToNativeArray(Allocator.TempJob);
                    stopwatchOutput.Start();
                    NativeHashSet<int> rangeIdModified = new((modifiedUpRanges.Count + modifiedUpRanges.Count + RemovedEntities.Count), Allocator.TempJob);
                    for (int j = 0; j < upList.Length; j++)
                    {
                        var entity = upList[j].entity;
                        if (entity.Modified[i])
                        {
                            rangeIdModified.Add(entity.Id);
                        }
                    }
                    for (int j = 0; j < subList.Length; j++)
                    {
                        var entity = subList[j].entity;
                        if (entity.Modified[i])
                        {
                            rangeIdModified.Add(entity.Id);
                        }
                    }
                    foreach (var e in RemovedEntities)
                    {
                        rangeIdModified.Add(e);
                    }

                    if (rangeIdModified.Count > 0)
                    {
                        ulong overlapMaxSize = (ulong)(flattenUpTree.Lowers.Length + flattenUpTree.Uppers.Length + flattenUpTree.Insides.Length) * (ulong)(flattenSubTree.Lowers.Length + flattenSubTree.Uppers.Length + flattenSubTree.Insides.Length) / 8;
                        overlapMaxSize = Math.Min(overlapMaxSize, (ulong)(2147483646 * 0.15f / UnsafeUtility.SizeOf<int3>()));
                        NativeParallelHashSet<int3> outputResult = new((int)overlapMaxSize, Allocator.Persistent);
                        RecalculateModifiedOverlapJob job = new()
                        {
                            ResultInput = resultInput,
                            ResultOutput = outputResult.AsParallelWriter(),
                            RangeIDModified = rangeIdModified,
                        };
                        JobHandle jobHandle = job.Schedule(resultInput.Length, 64);
                        jobHandle.Complete();
                        Result[i].Dispose();
                        Result[i] = outputResult;
                    }
                    rangeIdModified.Dispose();
                    resultInput.Dispose();
                    stopwatchOutput.Stop();
                }
            }
            RemovedEntities.Clear();
            stopwatchRecalculateModifyOverlap.Stop();
        }
        private void MatchingTreeToTreeParallelId(NativeHSPDIMFlattenedTree flattenedTree, NativeHSPDIMFlattenedTree flattenedSortListTree)
        {
            stopwatchMatching.Start();
            MatchingRangeToTreeIdJob2 job = new()
            {
                FlattenedSortListTree = flattenedSortListTree,
                FlattenTree = flattenedTree,
                Result0 = Result[0].AsParallelWriter(),
                Result1 = Result[1].AsParallelWriter(),
                dynamic = IsDynamicMatching,
                //Message = logQueue.AsParallelWriter(),
            };
            JobHandle handle = job.Schedule(flattenedSortListTree.LowerNodes.Length, 1);
            handle.Complete();
            stopwatchMatching.Stop();
        }
        private void MatchingTreeToTree(BinaryTree<HSPDIMTreeNodeData>[] tree1, BinaryTree<HSPDIMTreeNodeData>[] tree2)
        {
            stopwatchMatching.Start();
            if (IsDynamicMatching)
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
            if (IsDynamicMatching)
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
        public void MappingRangeDynamic(HSPDIMRange[] ranges, BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenedTree)
        {
            stopwatchMapping.Start();
            if (Strategy == Strategy.PARALEL_ID_OUTPUT)
            {
                if (IsDynamicMapping)
                {
                    for (int j = 0; j < ranges.Length; j++)
                    {
                        var r = ranges[j];
                        for (short i = 0; i < dimension; i++)
                        {
                            if (r.entity.Modified[i])
                            {
                                RemoveRangeFromTreeId(i, r, tree, ref flattenedTree);
                            }
                        }
                    }
                    for (int j = 0; j < ranges.Length; j++)
                    {
                        var r = ranges[j];
                        r.UpdateRangeId(tree[0].depth);
                        for (short i = 0; i < dimension; i++)
                        {
                            if (r.entity.Modified[i])
                            {
                                AddRangeToTreeId(i, r, tree, ref flattenedTree);
                            }
                        }
                    }
                    //for (int i = 0; i < dimension; i++)
                    //{
                    //    Parallel.ForEach(tree[i], node =>
                    //    {
                    //        node.Data.Uppers.Sort();
                    //        node.Data.Lowers.Sort();
                    //        node.Data.Insides.Sort();
                    //    });
                    //}

                }
                else
                {
                    ClearRangeTreeId(tree, flattenedTree);
                    for (int j = 0; j < ranges.Length; j++)
                    {
                        var r = ranges[j];
                        r.UpdateRangeId(tree[0].depth);
                        for (short i = 0; i < dimension; i++)
                        {
                            AddRangeToTreeId(i, r, tree, ref flattenedTree);
                        }
                    }
                }
            }
            else
            {
                if (IsDynamicMapping)
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
                                AddRangeToTree(i, r, tree);
                            }
                        }
                    }
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
        public static void ClearRangeTreeId(BinaryTree<HSPDIMTreeNodeData>[] tree, NativeHSPDIMFlattenedTree flattenedTree)
        {
            flattenedTree.lowerCount = 0;
            flattenedTree.insideCount = 0;
            flattenedTree.coverCount = 0;
            for (short i = 0; i < dimension; i++)
            {
                foreach (var node in tree[i])
                {
                    node.Data.Lowers.Clear();
                    node.Data.Uppers.Clear();
                    node.Data.Covers.Clear();
                    node.Data.Insides.Clear();
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
            }
            else
            {
                tree[depth, lowerBound.index].Data.lowers.Add(lowerBound);
                tree[depth, upperBound.index].Data.uppers.Add(upperBound);
            }
            if (coverBound != null) tree[depth, coverBound.index].Data.covers.Add(coverBound);
        }
        public static void AddBoundToTreeId(NativeBound LowerBound, NativeBound UpperBound, BinaryTree<HSPDIMTreeNodeData> tree, ref NativeHSPDIMFlattenedTree flattenTree, bool inside, NativeBound? CoverBound = null)
        {
            //tree
            short depth = (short)LowerBound.Depth;
            LowerBound.IsInside = UpperBound.IsInside = inside;
            if (inside)
            {
                tree[depth, LowerBound.Index].Data.Insides.Add(LowerBound);
                tree[depth, LowerBound.Index].Data.Insides.Add(UpperBound);
                flattenTree.insideCount+=2;

            }
            else
            {
                tree[depth, LowerBound.Index].Data.Lowers.Add(LowerBound);
                tree[depth, UpperBound.Index].Data.Uppers.Add(UpperBound);
                flattenTree.lowerCount++;
            }
            if (CoverBound != null)
            {
                tree[depth, CoverBound.Value.Index].Data.Covers.Add(CoverBound.Value);
                flattenTree.coverCount++;
            }
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
        public static void RemoveBoundFromTree(HSPDIMBound lowerBound, HSPDIMBound upperBound,BinaryTree<HSPDIMTreeNodeData> tree, bool inside, HSPDIMBound coverBound = null)
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
                        RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1],  tree[i], false, range.Bounds[i, 2]);
                    }
                    else
                    {
                        RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], false);
                    }
                }
            }
        }
        public static void RemoveRangeFromTreeId(short i, HSPDIMRange range, BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenTree)
        {
            ref var lowerBound = ref range.Boundss[i, 0];
            ref var upperBound = ref range.Boundss[i, 1];
            ref var coverBound = ref range.Boundss[i, 2];
            bool modified = range.entity.Modified[i];
            if (lowerBound.Index < 0 || upperBound.Index < 0) return;
            short depth = (short)lowerBound.Depth;
            bool isInside = (upperBound.Index - lowerBound.Index) == 0;

            HSPDIMTreeNodeData lowerNode = tree[i][depth, lowerBound.Index].Data;
            HSPDIMTreeNodeData upperNode = tree[i][depth, upperBound.Index].Data;

            int lowerIndexInContainer, upperIndexInContainer=0;
            if (isInside)
            {
                lowerIndexInContainer = lowerNode.Insides.BinarySearch(lowerBound);
                lowerNode.Insides.RemoveAt(lowerIndexInContainer);
                upperIndexInContainer = upperNode.Insides.BinarySearch(upperBound);
                upperNode.Insides.RemoveAt(upperIndexInContainer);
                flattenTree.insideCount-=2;
            }
            else
            {
                lowerIndexInContainer = lowerNode.Lowers.BinarySearch(lowerBound);
                lowerNode.Lowers.RemoveAt(lowerIndexInContainer);
                upperIndexInContainer = upperNode.Uppers.BinarySearch(upperBound);
                upperNode.Uppers.RemoveAt(upperIndexInContainer);
                flattenTree.lowerCount--;
                if ((upperBound.Index - lowerBound.Index) == 2)
                {
                    HSPDIMTreeNodeData coverNode = tree[i][depth, coverBound.Index].Data;
                    for (int j = 0; j < coverNode.Covers.Length; j++)
                    {
                        if (coverNode.Covers[j].Id == coverBound.Id)
                        {
                            coverNode.Covers.RemoveAt(j);
                        }
                    }
                    flattenTree.coverCount--;
                    coverBound.UpdateRemoveBound(modified);
                }
            }
            lowerBound.UpdateRemoveBound(modified);
            upperBound.UpdateRemoveBound(modified);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeToTreeId(short i,HSPDIMRange range,BinaryTree<HSPDIMTreeNodeData>[] tree, ref NativeHSPDIMFlattenedTree flattenedTree)
        {
            int id = range.entity.Id;
            float pos = range.entity.Position[i] + HSPDIM.mapSizeEstimate / 2;
            float halfRange = range.range[i] / 2;
            int treeDepth = tree[i].depth;
            int depth = range.range[i] < HSPDIM.mapSizeEstimate / (1 << treeDepth) ? treeDepth : HSPDIM.DepthCal(range.range[i]);
            float lowerPos = pos - halfRange;
            float upperPos = pos + halfRange;
            int lowerIndex = range.entity.Enable ? HSPDIM.IndexCal(lowerPos, depth) : -1;
            int upperIndex = range.entity.Enable ? HSPDIM.IndexCal(upperPos, depth) : -1;
            if (lowerIndex < 0 || upperIndex < 0) return;
            bool modified = range.entity.Modified[i];

            ref var lowerBound = ref range.Boundss[i, 0];
            ref var upperBound = ref range.Boundss[i, 1];
            ref var coverBound = ref range.Boundss[i, 2];
            bool isInside = (upperIndex - lowerIndex) == 0;
            lowerBound.UpdateBound(id, lowerPos, lowerIndex, depth, lowerIndex, isInside, modified);
            upperBound.UpdateBound(id, upperPos, upperIndex, depth, lowerIndex, isInside, modified);

            if (isInside)
            {
                var node = tree[i][depth, lowerIndex].Data;
                node.Insides.Add(lowerBound);
                node.Insides.Add(upperBound);
                flattenedTree.insideCount += 2;
            }
            else
            {
                tree[i][depth, lowerIndex].Data.Lowers.Add(lowerBound);
                tree[i][depth, upperIndex].Data.Uppers.Add(upperBound);
                flattenedTree.lowerCount++;
                if (upperIndex - lowerIndex == 2)
                {
                    coverBound.UpdateBound(id, pos, lowerIndex + 1, depth, lowerIndex, isInside, modified);
                    tree[i][depth, lowerIndex + 1].Data.Covers.Add(coverBound);
                    flattenedTree.coverCount++;
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
            flattenSubTree.DisposePersistent();
            flattenUpTree.DisposePersistent();
            for (int i = 0; i < dimension; i++)
            {
                Result[i].Dispose();
                foreach (var node in upTree[i])
                {
                    node.Data.Dispose();
                }
                foreach (var node in subTree[i])
                {
                    node.Data.Dispose();
                }
            }
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


            stringBuilder.Append("Tree:\n");
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
        public struct MatchingRangeToTreeIdJob2 : IJobParallelFor
        {
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenedSortListTree;
            [ReadOnly] public NativeHSPDIMFlattenedTree FlattenTree;
            [ReadOnly] public bool dynamic;
            public NativeParallelHashSet<int3>.ParallelWriter Result0;
            public NativeParallelHashSet<int3>.ParallelWriter Result1;
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
                NativeParallelHashSet<int3>.ParallelWriter result = (dimensionIndex == 0)? Result0 : Result1;
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
                                            result.Add(new int3(upperBound.Id, boundInSortedList.Id, boundInSortedList.IsSub ? 0 : 1));
                                        else
                                            result.Add(new int3(boundInSortedList.Id, upperBound.Id, boundInSortedList.IsSub ? 1 : 0));
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
                                            result.Add(new int3(coverBound.Id, boundInSortedList.Id, boundInSortedList.IsSub ? 0 : 1));
                                        else
                                            result.Add(new int3(boundInSortedList.Id, coverBound.Id, boundInSortedList.IsSub ? 1 : 0));
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
                                            result.Add(new int3(lowerBound.Id, boundInSortedList.Id, boundInSortedList.IsSub ? 0 : 1));
                                        else
                                            result.Add(new int3(boundInSortedList.Id, lowerBound.Id, boundInSortedList.IsSub ? 1 : 0));
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
                                            result.Add(new int3(lowerBound.Id, newInsBound.Id, lowerBound.IsSub ? 1 : 0));
                                        else
                                            result.Add(new int3(newInsBound.Id, lowerBound.Id, lowerBound.IsSub ? 0 : 1));
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
                                                    result.Add(new int3(insideBound.Id, newInsBound.Id, insideBound.IsSub ? 1 : 0));
                                                else
                                                    result.Add(new int3(newInsBound.Id, insideBound.Id, insideBound.IsSub ? 0 : 1));
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
                NativeParallelHashSet<int3>.ParallelWriter result = (dimensionIndex == 0) ? Result0 : Result1;
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
                                    result.Add(new int3(boundInTree.Id, up.Id, boundInTree.IsSub ? 1 : 0));
                                else
                                    result.Add(new int3(up.Id, boundInTree.Id, boundInTree.IsSub ? 0 : 1));
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
                            result.Add(new int3(sub.Id, boundInSortedList.Id, boundInSortedList.IsSub ? 0 : 1));
                        else
                            result.Add(new int3(boundInSortedList.Id, sub.Id, boundInSortedList.IsSub ? 1 : 0));
                    }
                    subsetValues.Dispose();

                }
            }
        }
        [BurstCompile]
        public struct RecalculateModifiedOverlapJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int3> ResultInput;
            public NativeParallelHashSet<int3>.ParallelWriter ResultOutput;
            [ReadOnly] public NativeHashSet<int> RangeIDModified;


            public void Execute(int index)
            {
                int3 overlap = ResultInput[index];

                // If either object is modified for this dimension, skip this overlap.
                if (RangeIDModified.Contains(overlap.x) || RangeIDModified.Contains(overlap.y))
                    return;
                ResultOutput.Add(overlap);
            }
        }
        [BurstCompile]
        public struct SortParallelJob : IJobParallelFor
        {
            public NativeHSPDIMFlattenedTree FlattenedSortListTree;
            [ReadOnly] public bool isDynamic;
            public void Execute(int index)
            {
                NativeNode lowerNode = FlattenedSortListTree.LowerNodes[index];
                NativeNode upperNode = FlattenedSortListTree.UpperNodes[index];
                NativeNode insideNode = FlattenedSortListTree.InsideNodes[index];
                if (lowerNode.Count > 1)
                {
                    NativeArray<NativeBound> subArray = FlattenedSortListTree.Lowers.GetSubArray(lowerNode.Start, lowerNode.Count);
                    if (isDynamic)
                    {
                        PartialSortAndMerge(subArray);
                    }
                    else
                    {
                        subArray.Sort();
                    }
                }
                if (upperNode.Count > 1)
                {
                    NativeArray<NativeBound> subArray = FlattenedSortListTree.Uppers.GetSubArray(upperNode.Start, upperNode.Count);
                    if (isDynamic)
                    {
                        PartialSortAndMerge(subArray);
                    }
                    else
                    {
                        subArray.Sort();
                    }
                }
                if (insideNode.Count > 1)
                {
                    NativeArray<NativeBound> subArray = FlattenedSortListTree.Insides.GetSubArray(insideNode.Start, insideNode.Count);
                    if (isDynamic)
                    {
                        PartialSortAndMerge(subArray);
                    }
                    else
                    {
                        subArray.Sort();
                    }
                }
            }

            private void PartialSortAndMerge(NativeArray<NativeBound> arr)
            {
                int n = arr.Length;
                if (n < 2) return; // 0 or 1 element => nothing to do

                // 1) Find pivot: from the front, stop when order breaks
                int pivot = 1;
                while (pivot < n && arr[pivot].CompareTo(arr[pivot - 1]) >= 0)
                    pivot++;

                // If pivot == n => entire array is already sorted, so do nothing
                if (pivot == n) return;

                NativeArray<NativeBound> unsortedPart = arr.GetSubArray(pivot, n - pivot);
                unsortedPart.Sort();

                NativeArray<NativeBound> merged = new NativeArray<NativeBound>(n, Allocator.Temp);

                NativeArray<NativeBound> left = arr.GetSubArray(0, pivot);
                NativeArray<NativeBound> right = arr.GetSubArray(pivot, n - pivot);

                int li = 0, ri = 0, mi = 0;
                while (li < left.Length && ri < right.Length)
                {
                    if (left[li].CompareTo(right[ri]) <= 0)
                        merged[mi++] = left[li++];
                    else
                        merged[mi++] = right[ri++];
                }
                while (li < left.Length) merged[mi++] = left[li++];
                while (ri < right.Length) merged[mi++] = right[ri++];

                for (int i = 0; i < n; i++)
                    arr[i] = merged[i];

                merged.Dispose();
            }

        }
    }
}