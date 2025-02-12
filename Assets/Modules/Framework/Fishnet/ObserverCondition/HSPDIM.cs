using Framework.ADS;
using Framework.FishNet;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
namespace Framework.HSPDIMAlgo
{
    public class HSPDIM : SingletonNetwork<HSPDIM>, IDisposable
    {
        public static readonly float mapSizeEstimate = 100;
        public static float minEntitySubRegSize = 10;
        public static float minEntityUpRegSize = 3;

        public static short subTreeDepth;
        public static short upTreeDepth;
        public static readonly short dimension = 2;

        //public Dictionary<int, IHSPDIMEntity> HSPDIMEntities = new();
        public List<HSPDIMRange> upRanges = new();
        public List<HSPDIMRange> subRanges = new();
        public HashSet<HSPDIMRange> modifiedUpRanges = new();
        public HashSet<HSPDIMRange> modifiedSubRanges = new();
        public BinaryTree<HSPDIMTreeNodeData>[] upTree;
        public BinaryTree<HSPDIMTreeNodeData>[] subTree;
        public BinaryTree<HSPDIMSortListTreeNodeData>[] sortListUpTree;
        public BinaryTree<HSPDIMSortListTreeNodeData>[] sortListSubTree;
        NativeHSPDIMFlattenedTree flattenUpTree = new()
        {
            LowerDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
            UpperDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
            CoverDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
            InsideDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
        };
        NativeHSPDIMFlattenedTree flattenSubTree = new()
        {
            LowerDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
            UpperDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
            CoverDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent),
            InsideDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
        };
        NativeHSPDIMFlattenedSortListTree flattenedSortListUpTree = new()
        {
            ElementDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
        };
        NativeHSPDIMFlattenedSortListTree flattenedSortListSubTree = new()
        {
            ElementDimensions = new NativeArray<NativeListElement>(dimension, Allocator.Persistent)
        };
        NativeList<OverlapID> overlapSet;
        public bool isRunning;

        #region Measure
        Stopwatch stopwatchAlgo = new Stopwatch();
        Stopwatch stopwatchOutput = new Stopwatch();
        int exeCount = 0;
        double exeTotalTimeAlgo = 0;
        double exeTotalTimeOutput = 0;
        double totalMemAlgo = 0;
        double totalMemOutput = 0;
        #endregion

        [BurstDiscard]
        public static short DepthCal(float subjectSize)
        {
            short depth = 0;
            while (Mathf.Pow(2, depth + 1) <= mapSizeEstimate / subjectSize)
            {
                depth++;
            }
            return depth;
        }
        public static int IndexCal(float subjectPos, int depth)
        {
            return Mathf.FloorToInt(subjectPos / mapSizeEstimate * Mathf.Pow(2, depth));
        }
        public static bool UpdateInterval(float ratio = 5f)
        {
            float time = Time.time * ratio;
            return Mathf.FloorToInt(time - Time.deltaTime * ratio) < Mathf.FloorToInt(time);
        }
        private void Update()
        {
            if (UpdateInterval() && isRunning)
            {
                //long memoryBefore = GC.GetTotalMemory(true);
                var uplist = modifiedUpRanges.ToList();
                var sublist = modifiedSubRanges.ToList();
                MappingRangeDynamic(uplist, upTree, sortListUpTree);
                MappingRangeDynamic(sublist, subTree, sortListSubTree);
                ConvertFlattenedSortListTree(true);
                ConvertFlattenedSortListTree(false);
                //LogTree(upTree, sortListUpTree);
                //LogTree(subTree, sortListSubTree);
                //PDebug.Log(flattenUpTree);
                //PDebug.Log(flattenSubTree);
                //PDebug.Log(flattenedSortListUpTree);
                //PDebug.Log(flattenedSortListSubTree);
                DynamicMatching();
                uplist.ForEach(r => r.entity.Modified = Vector3Bool.@false);
                sublist.ForEach(r => r.entity.Modified = Vector3Bool.@false);
                uplist.Clear();
                sublist.Clear();

                //stopwatch.Stop();
                //if (exeCount > 1)
                //{
                //    exeTotalTime += stopwatch.Elapsed.TotalMilliseconds;
                //    //totalMem += GC.GetTotalMemory(true) - memoryBefore;
                //    PDebug.LogWarning($"Thread Count {JobsUtility.JobWorkerCount}, Sub Mod Count {modifiedSubRanges.Count}, Up Mod Count {modifiedUpRanges.Count}, ExeTime {exeTotalTime} : {exeTotalTime / (exeCount - 1)}, Mem {totalMem / 1024f / (exeCount - 1)} over {exeCount - 1} time");
                //    if (exeCount == 100)
                //    {
                //        Time.timeScale = 0;
                //    }
                //}
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
        public void InitMappingAndMatching()
        {
            minEntitySubRegSize = minEntityUpRegSize = HSPDIMTest.rangeValue;
            subTreeDepth = DepthCal(minEntitySubRegSize);
            upTreeDepth = DepthCal(minEntityUpRegSize);
            upTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMTreeNodeData>(upTreeDepth)).ToArray();
            subTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMTreeNodeData>(subTreeDepth)).ToArray();
            sortListUpTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMSortListTreeNodeData>(upTreeDepth)).ToArray();
            sortListSubTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMSortListTreeNodeData>(subTreeDepth)).ToArray();
            upTree.ForEach(tree => tree.ForEach(node =>
            {
                node.Data.lowerBound = node.index / Mathf.Pow(2, node.depth) * mapSizeEstimate;
                node.Data.upperBound = (node.index + 1) / Mathf.Pow(2, node.depth) * mapSizeEstimate;
            }));
            subTree.ForEach(tree => tree.ForEach(node =>
            {
                node.Data.lowerBound = node.index / Mathf.Pow(2, node.depth) * mapSizeEstimate;
                node.Data.upperBound = (node.index + 1) / Mathf.Pow(2, node.depth) * mapSizeEstimate;
            }));
            MappingRanges(modifiedUpRanges.ToList(), upTree, sortListUpTree);
            MappingRanges(modifiedSubRanges.ToList(), subTree, sortListSubTree);
            InitFlatteningTree(true);
            InitFlatteningTree(false);
            //LogTree(upTree, sortListUpTree);
            //LogTree(subTree, sortListSubTree);
            //PDebug.Log(flattenUpTree);
            //PDebug.Log(flattenSubTree);
            //PDebug.Log(flattenedSortListUpTree);
            //PDebug.Log(flattenedSortListSubTree);
            //MatchingTreeToTree(sortListUpTree, subTree);
            //MatchingTreeToTree(sortListSubTree, upTree);
            //MatchingTreeToTreeParallel(flattenUpTree, flattenedSortListSubTree, sortListSubTree, 1);
            //MatchingTreeToTreeParallel(flattenSubTree, flattenedSortListUpTree, sortListUpTree, 0);
            foreach (var range in modifiedSubRanges)
            {
                range.UpdateIntersection();
            }
            modifiedUpRanges.ForEach(r => r.entity.Modified = Vector3Bool.@false);
            modifiedSubRanges.ForEach(r => r.entity.Modified = Vector3Bool.@false);
            modifiedUpRanges.Clear();
            modifiedSubRanges.Clear();
            isRunning = true;
        }
        void InitFlatteningTree(bool isUp)
        {
            BinaryTree<HSPDIMTreeNodeData>[] tree = isUp ? upTree : subTree;
            BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree = isUp ? sortListUpTree : sortListSubTree;
            NativeHSPDIMFlattenedTree flattenedTree = isUp ? flattenUpTree : flattenSubTree;
            NativeHSPDIMFlattenedSortListTree flattenedSortListTree = isUp ? flattenedSortListUpTree : flattenedSortListSubTree;
            //flattenedTree
            int totalNode = 0;
            for (int i = 0; i < dimension; i++)
            {
                totalNode += (int)Mathf.Pow(2, tree[i].depth + 1) - 1;
            }
            flattenedTree.LowerNodes = new(totalNode, Allocator.Persistent);
            flattenedTree.UpperNodes = new(totalNode, Allocator.Persistent);
            flattenedTree.CoverNodes = new(totalNode, Allocator.Persistent);
            flattenedTree.InsideNodes = new(totalNode, Allocator.Persistent);
            List<NativeBound> lowerBounds = new();
            List<NativeBound> upperBounds = new();
            List<NativeBound> coverBounds = new();
            List<NativeBound> insideBounds = new();
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
                int totalNodeDimI = (int)Mathf.Pow(2, tree[i].depth + 1) - 1;
                tree[i].ForEach(node =>
                {
                    int index = (1 << node.depth) + node.index - 1 + startInsideDimension;
                    if (node.Data.lowers.Count > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, node.Data.lowers.Count);
                        startLower += node.Data.lowers.Count;
                        lowerBounds.AddRange(node.Data.lowers.Select((b, j) => b.ToNativeBound(j, false, j)));
                    }
                    else
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, 0);
                    }


                    if (node.Data.uppers.Count > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, node.Data.uppers.Count);
                        startUpper += node.Data.uppers.Count;
                        upperBounds.AddRange(node.Data.uppers.Select((b, j) => b.ToNativeBound(j, false, tree[i][node.depth, b.range.Bounds[i, 0].index].Data.lowers.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    else
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, 0);
                    }


                    if (node.Data.covers.Count > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, node.Data.covers.Count);
                        startCover += node.Data.covers.Count;
                        coverBounds.AddRange(node.Data.covers.Select((b, j) => b.ToNativeBound(j, false, 0)));
                    }
                    else
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, 0);
                    }


                    if (node.Data.insides.Count > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, node.Data.insides.Count);
                        startInside += node.Data.insides.Count;
                        insideBounds.AddRange(node.Data.insides.Select((b, j) => b.ToNativeBound(j, true, node.Data.insides.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    else
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, 0);
                    }
                });
                flattenedTree.LowerDimensions[i] = new NativeListElement(startLowerDimension, totalNodeDimI);
                startLowerDimension += totalNodeDimI;
                flattenedTree.UpperDimensions[i] = new NativeListElement(startUpperDimension, totalNodeDimI);
                startUpperDimension += totalNodeDimI;
                flattenedTree.CoverDimensions[i] = new NativeListElement(startCoverDimension, totalNodeDimI);
                startCoverDimension += totalNodeDimI;
                flattenedTree.InsideDimensions[i] = new NativeListElement(startInsideDimension, totalNodeDimI);
                startInsideDimension += totalNodeDimI;
            }
            flattenedTree.depth = new NativeArray<short>(new short[] { tree[0].depth, tree[1].depth }, Allocator.Persistent);
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
            if (isUp) flattenUpTree = flattenedTree;
            else flattenSubTree = flattenedTree;

            //flattenedSortListTree
            flattenedSortListTree.ElementList = new(totalNode * 2, Allocator.Persistent);
            List<NativeBound> listSortedNativeBounds = new();
            int startSortedBound = 0;
            int startSortedBoundDimension = 0;
            for (int i = 0; i < dimension; i++)
            {
                int totalNodeDimI = (int)Mathf.Pow(2, tree[i].depth + 2) - 2;
                foreach (var node in tree[i])
                {
                    int index = (2 << node.depth) + node.index * 2 - 2 + startSortedBoundDimension;
                    List<HSPDIMBound> crossNodeRanges = sortListTree[i][node.depth, node.index].Data.crosses.FindAll(b => b.range.entity.Modified[i]);
                    List<HSPDIMBound> insideRange = sortListTree[i][node.depth, node.index].Data.insides.FindAll(b => b.range.entity.Modified[i]);

                    flattenedSortListTree.ElementList[index] = new NativeListElement(startSortedBound, crossNodeRanges.Count);
                    if (crossNodeRanges.Count > 0)
                    {
                        listSortedNativeBounds.AddRange(crossNodeRanges.Select((b, j) => b.ToNativeBound(node.depth, j, false, node.index, crossNodeRanges.BinarySearch(b.range.Bounds[i, 0]))));
                        startSortedBound += crossNodeRanges.Count;
                    }

                    flattenedSortListTree.ElementList[index + 1] = new NativeListElement(startSortedBound, insideRange.Count);
                    if (insideRange.Count > 0)
                    {
                        listSortedNativeBounds.AddRange(insideRange.Select((b, j) => b.ToNativeBound(node.depth, j, true, node.index, insideRange.BinarySearch(b.range.Bounds[i, 0]))));
                        startSortedBound += insideRange.Count;
                    }
                }
                flattenedSortListTree.ElementDimensions[i] = new NativeListElement(startSortedBoundDimension, totalNodeDimI);
                startSortedBoundDimension += totalNodeDimI;
            }

            flattenedSortListTree.Bounds = new NativeArray<NativeBound>(listSortedNativeBounds.Count, Allocator.TempJob);
            for (int i = 0; i < listSortedNativeBounds.Count; i++)
            {
                flattenedSortListTree.Bounds[i] = listSortedNativeBounds[i];
            }
            if (isUp) flattenedSortListUpTree = flattenedSortListTree;
            else flattenedSortListSubTree = flattenedSortListTree;
        }
        void ConvertFlattenedSortListTree(bool isUp)
        {
            BinaryTree<HSPDIMTreeNodeData>[] tree = isUp ? upTree : subTree;
            BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree = isUp ? sortListUpTree : sortListSubTree;
            NativeHSPDIMFlattenedTree flattenedTree = isUp ? flattenUpTree : flattenSubTree;
            NativeHSPDIMFlattenedSortListTree flattenedSortListTree = isUp ? flattenedSortListUpTree : flattenedSortListSubTree;

            List<NativeBound> lowerBounds = new();
            List<NativeBound> upperBounds = new();
            List<NativeBound> coverBounds = new();
            List<NativeBound> insideBounds = new();
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
                int totalNodeDimI = (int)Mathf.Pow(2, tree[i].depth + 1) - 1;
                tree[i].ForEach(node =>
                {
                    int index = (1 << node.depth) + node.index - 1 + startInsideDimension;
                    if (node.Data.lowers.Count > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, node.Data.lowers.Count);
                        startLower += node.Data.lowers.Count;
                        lowerBounds.AddRange(node.Data.lowers.Select((b, j) => b.ToNativeBound(j, false, j)));
                    }
                    else
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, 0);
                    }


                    if (node.Data.uppers.Count > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, node.Data.uppers.Count);
                        startUpper += node.Data.uppers.Count;
                        upperBounds.AddRange(node.Data.uppers.Select((b, j) => b.ToNativeBound(j, false, tree[i][node.depth, b.range.Bounds[i, 0].index].Data.lowers.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    else
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, 0);
                    }


                    if (node.Data.covers.Count > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, node.Data.covers.Count);
                        startCover += node.Data.covers.Count;
                        coverBounds.AddRange(node.Data.covers.Select((b, j) => b.ToNativeBound(j, false, 0)));
                    }
                    else
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, 0);
                    }


                    if (node.Data.insides.Count > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, node.Data.insides.Count);
                        startInside += node.Data.insides.Count;
                        insideBounds.AddRange(node.Data.insides.Select((b, j) => b.ToNativeBound(j, true, node.Data.insides.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    else
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, 0);
                    }
                });
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
            if (isUp) flattenUpTree = flattenedTree;
            else flattenSubTree = flattenedTree;

            //flattentree
            List<NativeBound> listSortedNativeBounds = new();
            int startSortedBound = 0;
            int startSortedBoundDimension = 0;
            for (int i = 0; i < dimension; i++)
            {
                int totalNodeDimI = (int)Mathf.Pow(2, tree[i].depth + 2) - 2;
                foreach (var node in tree[i])
                {
                    int index = (2 << node.depth) + node.index * 2 - 2 + startSortedBoundDimension;
                    List<HSPDIMBound> crossNodeRanges = sortListTree[i][node.depth, node.index].Data.crosses.FindAll(b => b.range.entity.Modified[i]);
                    List<HSPDIMBound> insideRange = sortListTree[i][node.depth, node.index].Data.insides.FindAll(b => b.range.entity.Modified[i]);
                    flattenedSortListTree.ElementList[index] = new NativeListElement(startSortedBound, crossNodeRanges.Count);
                    if (crossNodeRanges.Count > 0)
                    {
                        listSortedNativeBounds.AddRange(crossNodeRanges.Select((b, j) => b.ToNativeBound(node.depth, j, false, node.index, crossNodeRanges.BinarySearch(b.range.Bounds[i, 0]))));
                        startSortedBound += crossNodeRanges.Count;
                    }

                    flattenedSortListTree.ElementList[index + 1] = new NativeListElement(startSortedBound, insideRange.Count);
                    if (insideRange.Count > 0)
                    {
                        listSortedNativeBounds.AddRange(insideRange.Select((b, j) => b.ToNativeBound(node.depth, j, true, node.index, insideRange.BinarySearch(b.range.Bounds[i, 0]))));
                        startSortedBound += insideRange.Count;
                    }
                }
                flattenedSortListTree.ElementDimensions[i] = new NativeListElement(startSortedBoundDimension, totalNodeDimI);
                startSortedBoundDimension += totalNodeDimI;
            }
            flattenedSortListTree.Bounds = new NativeArray<NativeBound>(listSortedNativeBounds.Count, Allocator.TempJob);
            for (int i = 0; i < listSortedNativeBounds.Count; i++)
            {
                flattenedSortListTree.Bounds[i] = listSortedNativeBounds[i];
            }
            if (isUp) flattenedSortListUpTree = flattenedSortListTree;
            else flattenedSortListSubTree = flattenedSortListTree;
        }
        private void Matching(List<HSPDIMBound> sortedBounds, BinaryTree<HSPDIMTreeNodeData> tree, int i)
        {
            if (sortedBounds.Count == 0) return;
            //StringBuilder sb = new StringBuilder();
            //sb.Append("StartMatching\n");
            BinaryTree<Vector3Int> indexTree = new(tree.depth);
            indexTree.ForEach(t =>
            {
                t.Data.x = 0;
                t.Data.y = 0;
                t.Data.z = 0;
            });
            List<HSPDIMRange> newIns = new();
            List<HSPDIMRange> subset = new();
            List<HSPDIMRange> upset = new();
            int leftLeaf = IndexCal(sortedBounds.First().boundValue, tree.depth);
            int rightLeaf = IndexCal(sortedBounds.Last().boundValue, tree.depth);
            int j = 0;
            int m2 = leftLeaf, m = leftLeaf;
            HSPDIMBound boundInSortedList;
            HSPDIMBound boundInTree = null;

            //sb.Append($"sortedListCount:{sortedBounds.Count},leftLeaf:{leftLeaf},rightLeaf:{rightLeaf}\n");
            //Debug.Log(sb);
            //sb.Clear();
            while (j < sortedBounds.Count() && m <= rightLeaf)
            {
                boundInSortedList = sortedBounds[j];
                //sb.Append($"bound in SortedList:{boundInSortedList.boundValue},indexLeaf:{m},boundInListIndex:{IndexCal(boundInSortedList.boundValue, tree.depth)}\n");
                TreeNode<HSPDIMTreeNodeData> node;
                TreeNode<Vector3Int> indexNode = null;
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
                                for (int idx = indexNode.Data.y; idx < node.Data.uppers.Count; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.uppers[idx].range;
                                    boundInSortedList.range.overlapSets[i].Add(overlapRange);
                                    overlapRange.overlapSets[i].Add(boundInSortedList.range);
                                }
                                //sb.Append($"add Overlap Upper from{indexNode.Data.y} to {node.Data.uppers.Count}:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))};\n ");
                            }

                            if (node.Data.covers.Count > 0)
                            {
                                for (int idx = 0; idx < node.Data.covers.Count; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.covers[idx].range;
                                    boundInSortedList.range.overlapSets[i].Add(overlapRange);
                                    overlapRange.overlapSets[i].Add(boundInSortedList.range);
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
                                for (int idx = 0; idx < indexNode.Data.x; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.lowers[idx].range;
                                    boundInSortedList.range.overlapSets[i].Add(overlapRange);
                                    overlapRange.overlapSets[i].Add(boundInSortedList.range);
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
                    if (newIns.Count > 0)
                        //sb.Append($"add Overlap Lower to\n{string.Join("\n", newIns.Select(r => r.ToString()))} \n");
                        for ((short l, int k) = (tree.depth, m); l >= 0; l--)
                        {
                            indexNode = indexTree[l, k];
                            node = tree[l, k];
                            //sb.Append($"node:[{i},{l},{k}]");
                            if (node.Data.lowers.Count > 0)
                            {
                                for (int idx = 0; idx < node.Data.lowers.Count; idx++)
                                {
                                    HSPDIMRange overlapRange = node.Data.lowers[idx].range;
                                    boundInSortedList.range.overlapSets[i].Add(overlapRange);
                                    overlapRange.overlapSets[i].Add(boundInSortedList.range);
                                }
                                //sb.Append($"\t overlap {node.Data.lowers.Count} lower:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}");
                            }
                            //sb.Append($"\n");
                            if (l == tree.depth)
                            {
                                newIns.ForEach(b =>
                                {
                                    if (m > IndexCal(b.Bounds[i, 0].boundValue, tree.depth))
                                    {
                                        for (int idx = 0; idx < node.Data.insides.Count; idx++)
                                        {
                                            HSPDIMRange overlapRange = node.Data.insides[idx].range;
                                            boundInSortedList.range.overlapSets[i].Add(overlapRange);
                                            overlapRange.overlapSets[i].Add(boundInSortedList.range);
                                        }
                                        //sb.Append($"\t overlap {b} inside:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}");
                                    }
                                    else
                                    {
                                        //SortMatchInside(b.Bounds[i, 0], tree, indexTree, i, m, subset, upset, sb);
                                    }
                                });
                                if (m == m2)
                                {
                                    SortMatchInside(boundInSortedList, tree, indexTree, i, m, subset, upset, false, null);
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
            TreeNode<HSPDIMTreeNodeData> node = tree[tree.depth, m];
            HSPDIMBound boundInTree;
            //sb.Append($"insideIt: {indexTree[tree.depth, m].Data.z}\n");
            if (node.Data.insides.Count > 0 && indexTree[tree.depth, m].Data.z < node.Data.insides.Count)
            {
                boundInTree = node.Data.insides[indexTree[tree.depth, m].Data.z];
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
                        upset.ForEach(r =>
                        {
                            r.overlapSets[i].Add(boundInTree.range);
                            boundInTree.range.overlapSets[i].Add(r);
                        });

                        if (upset.Count > 0)
                        {
                            //sb.Append($"matching inside range {boundInTree.range} :\n{string.Join("\n", upset.Select(r => r.ToString()))} \n");
                        }
                    }
                    //sb.Append($"subset after:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
                    Vector3Int temp = indexTree[tree.depth, m].Data;
                    temp.z++;
                    indexTree[tree.depth, m].Data = temp;
                    if (indexTree[tree.depth, m].Data.z < node.Data.insides.Count)
                    {
                        boundInTree = node.Data.insides[indexTree[tree.depth, m].Data.z];
                        //sb.Append($"insideIt: {indexTree[tree.depth, m].Data.z}\n");
                    }
                    else
                    {
                        //sb.Append($"\n");
                        break;
                    }
                }
            }

            if (boundInSortedList.isUpper == -1)
            {
                if (headEnd)
                {
                    upset.Add(boundInSortedList.range);
                }
            }
            else if (boundInSortedList.isUpper == 1)
            {
                if (headEnd)
                    upset.Remove(boundInSortedList.range);
                if (subset.Count > 0)
                {
                    boundInSortedList.range.overlapSets[i].AddRange(subset);
                    //sb.Append($"add {subset.Count} Overlap inside {boundInSortedList}:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
                    subset.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                }
            }
        }
        private void DynamicMatching()
        {
            upRanges.ForEach(r =>
            {
                for (int i = 0; i < dimension; i++)
                {
                    if (r.entity.Modified[i])
                    {
                        r.overlapSets[i].ForEach(r2 => r2.overlapSets[i].Remove(r));
                        r.entity.SubRange?.overlapSets[i].ForEach(r2 =>
                        {
                            r2.overlapSets[i].Remove(r);
                        });
                        r.overlapSets[i].Clear();
                        r.entity.SubRange?.overlapSets[i].Clear();
                    }
                }
            });
            exeCount++;
            stopwatchAlgo.Reset();
            stopwatchOutput.Reset();
            //MatchingTreeToTree(sortListUpTree, subTree);
            //MatchingTreeToTree(sortListSubTree, upTree);
            MatchingTreeToTreeParallel(flattenUpTree, flattenedSortListSubTree, sortListSubTree, 1);
            MatchingTreeToTreeParallel(flattenSubTree, flattenedSortListUpTree, sortListUpTree, 0);
            int exclude = 3;
            if (exeCount > exclude)
            {
                exeTotalTimeAlgo += stopwatchAlgo.Elapsed.TotalMilliseconds;
                exeTotalTimeOutput += stopwatchOutput.Elapsed.TotalMilliseconds;

                PDebug.LogWarning($"Thread Count {JobsUtility.JobWorkerCount}, Sub Mod Count {modifiedSubRanges.Count}, Up Mod Count {modifiedUpRanges.Count}, ExeTimeAlgo {exeTotalTimeAlgo} : {exeTotalTimeAlgo / (exeCount - exclude)}, ExeTimeOutPut {exeTotalTimeOutput} : {exeTotalTimeOutput / (exeCount - exclude)}, Mem {totalMemAlgo / 1024f / (exeCount - exclude)} over {exeCount - exclude} time");

                if (exeCount - exclude == 100)
                {
                    Time.timeScale = 0;
                }
            }
            subRanges.ForEach(r => { if (r.entity.Enable) { r.UpdateIntersection(); } });
            //Dispose();
        }
        private void MatchingTreeToTreeParallel(NativeHSPDIMFlattenedTree flattenedTree, NativeHSPDIMFlattenedSortListTree flattenedSortListTree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree, int isUpToSub)
        {
            ulong size = (ulong)dimension * (ulong)flattenedSortListTree.Bounds.Length * (ulong)(flattenedTree.Lowers.Length + flattenedTree.Uppers.Length + flattenedTree.Insides.Length / 2);
            PDebug.Log($"Size Allocation :{size}, with each {UnsafeUtility.SizeOf<OverlapID>()}, min {2147483647 / UnsafeUtility.SizeOf<OverlapID>()}");
            size = (ulong)Math.Min(size, 2147483647f / UnsafeUtility.SizeOf<OverlapID>() * 0.9f);
            NativeList<OverlapID> overlapSet = new((int)size, Allocator.TempJob);
            var tree = isUpToSub == 1 ? upTree : subTree;
            //var logQueue = new NativeQueue<FixedString128Bytes>(Allocator.TempJob);
            stopwatchAlgo.Start();
            MathcingRangeToTreeJob job = new()
            {
                FlattenedSortListTree = flattenedSortListTree,
                FlattenTree = flattenedTree,
                OverlapSet = overlapSet.AsParallelWriter(),
                //Message = logQueue.AsParallelWriter(),
            };
            JobHandle handle = job.Schedule(flattenedSortListTree.ElementList.Length, 1);
            handle.Complete();
            stopwatchAlgo.Stop();

            stopwatchOutput.Start();
            for (int j = 0; j < overlapSet.Length; j++)
            {
                OverlapID overlap = overlapSet[j];
                int i = overlap.rangeIDInTree.Dim;
                var overlapRange = overlap.MapRangeToTree(tree[i]);
                int startIndexElement = flattenedSortListTree.ElementList[overlap.rangeIDInList.Index].Start;
                var boundInList = overlap.rangeIDInList.IsInside
? sortListTree[i][overlap.rangeIDInList.Depth, overlap.rangeIDInList.Index].Data.insides[overlap.rangeIDInList.IndexContainer]
: sortListTree[i][overlap.rangeIDInList.Depth, overlap.rangeIDInList.Index].Data.crosses[overlap.rangeIDInList.IndexContainer];
                boundInList.range.overlapSets[i].AddRange(overlapRange);
                overlapRange.ForEach(r => r.overlapSets[i].Add(boundInList.range));
            }
            stopwatchOutput.Stop();

            overlapSet.Dispose();
            //Debug.Log($"{string.Join("\n", Enumerable.Range(0, logQueue.Count).Select(_ => logQueue.Dequeue()))}");
            //logQueue.Dispose();
            flattenedTree.Dispose();
            flattenedSortListTree.Dispose();
        }
        private void MatchingTreeToTree(BinaryTree<HSPDIMSortListTreeNodeData>[] tree1, BinaryTree<HSPDIMTreeNodeData>[] tree2)
        {
            stopwatchAlgo.Start();
            for (int i = 0; i < dimension; i++)
            {
                tree1[i].PreOrderEnumerator(tree1[i].Root).ForEach(node =>
                {
                    if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                    {
                        Matching(node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], i);
                    }
                    if (node.Data.crosses.Count > 0)
                    {
                        Matching(node.Data.crosses.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], i);
                    }
                });
            }
            stopwatchAlgo.Stop();
        }
        public static void MappingRanges(List<HSPDIMRange> ranges, BinaryTree<HSPDIMTreeNodeData>[] tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree, List<HSPDIMBound>[] bounds = null)
        {
            foreach (HSPDIMRange r in ranges)
            {
                r.UpdateRange(tree[0].depth);
                for (short i = 0; i < dimension; i++)
                {
                    AddRangeToTree(i, r, tree, sortListTree);
                }
            }
            if (bounds != null)
            {
                foreach (HSPDIMRange r in ranges)
                {
                    for (short i = 0; i < dimension; i++)
                    {
                        if (r.entity.Modified[i])
                        {
                            bounds[i].Add(r.Bounds[i, 0]);
                            bounds[i].Add(r.Bounds[i, 1]);
                        }
                    }
                }
                bounds?.ForEach(bs => bs.Sort());
            }
            for (short i = 0; i < dimension; i++)
            {
                foreach (var node in tree[i])
                {
                    if (node.Data.uppers.Count > 0) node.Data.uppers.Sort();
                    if (node.Data.lowers.Count > 0) node.Data.lowers.Sort();
                    if (node.depth == tree[i].depth && node.Data.insides.Count > 0) node.Data.insides.Sort();
                }
            }
        }
        public void MappingRangeDynamic(List<HSPDIMRange> ranges, BinaryTree<HSPDIMTreeNodeData>[] tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree)
        {
            //StringBuilder sb = new StringBuilder();
            //sb.Append($"MappingRangesDynamic {ranges.Count()} range\n");
            foreach (HSPDIMRange r in ranges)
            {
                //sb.Append($"old bound {r} \n ");
                for (short i = 0; i < dimension; i++)
                {
                    if (r.entity.Modified[i])
                    {
                        RemoveRangeFromTree(i, r, tree, sortListTree);
                    }
                }
                r.UpdateRange(tree[0].depth);
                for (short i = 0; i < dimension; i++)
                {
                    if (r.entity.Modified[i])
                    {
                        AddRangeToTree(i, r, tree, sortListTree);
                    }
                }
                //sb.Append($"new bound {r} \n");
            }
            //PDebug.Log(sb);
        }
        public static void AddBoundToTree(HSPDIMBound lowerBound, HSPDIMBound upperBound, BinaryTree<HSPDIMTreeNodeData> tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree, bool inside, HSPDIMBound coverBound = null)
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

            // sortlisttree 
            List<HSPDIMBound> sortListTreeNode;
            if (inside)
            {
                sortListTreeNode = sortListTree[dim][depth, lowerBound.index].Data.insides;
                sortListTreeNode.Insert(lowerIndexInContainer, lowerBound);
                sortListTreeNode.Insert(upperIndexInContainer, upperBound);

            }
            else
            {
                sortListTreeNode = sortListTree[dim][depth, lowerBound.index].Data.crosses;
                sortListTreeNode.Insert(lowerIndexInContainer, lowerBound);
                sortListTreeNode.Insert(upperIndexInContainer + lowerContainer.Count, upperBound);
            }

        }
        public static void RemoveBoundFromTree(HSPDIMBound lowerBound, HSPDIMBound upperBound, BinaryTree<HSPDIMTreeNodeData> tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree, bool inside, HSPDIMBound coverBound = null)
        {
            int dim = lowerBound.dimId;
            short depth = (short)lowerBound.range.depthLevel[dim];
            HSPDIMTreeNodeData lowerNode = tree[depth, lowerBound.index].Data;
            HSPDIMTreeNodeData upperNode = tree[depth, upperBound.index].Data;
            List<HSPDIMBound> lowerContainer, upperContainer;
            int lowerIndexInContainer = -100, upperIndexInContainer = -100;
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
            lowerContainer.RemoveAt(lowerIndexInContainer);
            upperIndexInContainer = upperContainer.BinarySearch(upperBound);
            upperContainer.RemoveAt(upperIndexInContainer);

            if (coverBound != null)
            {
                tree[depth, lowerBound.index].Data.covers.Remove(coverBound);
                coverBound.index = -1;
            }


            // sortlisttree 
            List<HSPDIMBound> sortListTreeNode;
            if (inside)
            {
                sortListTreeNode = sortListTree[dim][depth, lowerBound.index].Data.insides;
                sortListTreeNode.RemoveAt(lowerIndexInContainer);
                sortListTreeNode.RemoveAt(upperIndexInContainer);
            }
            else
            {
                sortListTreeNode = sortListTree[dim][depth, lowerBound.index].Data.crosses;
                sortListTreeNode.RemoveAt(lowerIndexInContainer);
                sortListTreeNode.RemoveAt(upperIndexInContainer + lowerContainer.Count);
            }
            lowerBound.index = -1;
            upperBound.index = -1;

            //PDebug.LogError($"[{bound.dimId},{bound.range.depthLevel[bound.dimId]},{bound.index}] {bound.range} \n {string.Join(",", container.Select(b => b.range))}");
            //Time.timeScale = 0;
        }
        public static void RemoveRangeFromTree(short i, HSPDIMRange range, BinaryTree<HSPDIMTreeNodeData>[] tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree)
        {
            if (range.Bounds[i, 0] == null) return;
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
            {
                if (range.Bounds[i, 1].index - range.Bounds[i, 0].index == 0 && range.depthLevel[i] == tree[i].depth)
                {
                    RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], sortListTree, true);
                }
                else
                {
                    if (range.Bounds[i, 1].index - range.Bounds[i, 0].index == 2)
                    {
                        RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], sortListTree, false, range.Bounds[i, 2]);
                    }
                    else
                    {
                        RemoveBoundFromTree(range.Bounds[i, 0], range.Bounds[i, 1], tree[i], sortListTree, false);
                    }
                }
            }
        }
        public static void AddRangeToTree(short i, HSPDIMRange range, BinaryTree<HSPDIMTreeNodeData>[] tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree)
        {
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
            {
                HSPDIMBound lowerBound = range.Bounds[i, 0] = range.Bounds[i, 0] ?? new HSPDIMBound(i, -1, range);
                HSPDIMBound upperBound = range.Bounds[i, 1] = range.Bounds[i, 1] ?? new HSPDIMBound(i, 1, range);
                HSPDIMBound coverBound = range.Bounds[i, 2] = range.Bounds[i, 2] ?? new HSPDIMBound(i, 0, range);
                if (upperBound.index - lowerBound.index == 0 && range.depthLevel[i] == tree[i].depth)
                {
                    AddBoundToTree(lowerBound, upperBound, tree[i], sortListTree, true);
                }
                else
                {
                    if (upperBound.index - lowerBound.index == 2)
                    {
                        AddBoundToTree(lowerBound, upperBound, tree[i], sortListTree, false, coverBound);
                    }
                    else
                    {
                        AddBoundToTree(lowerBound, upperBound, tree[i], sortListTree, false);
                    }
                }
            }
        }
        public void Dispose()
        {
            //matchingResultUpToSub.ForEach(m => m.Dispose());
            //matchingResultSubToUp.ForEach(m => m.Dispose());
            flattenSubTree.DisposePersistent();
            flattenUpTree.DisposePersistent();
            flattenedSortListSubTree.DisposePersistent();
            flattenedSortListUpTree.DisposePersistent();
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
        private static void LogTree(BinaryTree<HSPDIMTreeNodeData>[] tree, BinaryTree<HSPDIMSortListTreeNodeData>[] sortListTree)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Tree:\n");
            if (tree != null)
            {
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in tree[i])
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
                foreach (var node in sortListTree[i])
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
        public struct MathcingRangeToTreeJob : IJobParallelFor
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
            void Matching(int startBoundIndex, int countBound, int indexSortedListElemenet, int DimensionIndex, short treeDepth)
            {
                int endBoundIndex = startBoundIndex + countBound - 1;
                int totalNodes = (int)Mathf.Pow(2, treeDepth + 1) - 1;
                NativeArray<int3> indexTree = new(totalNodes, Allocator.Temp);
                for (int q = 0; q < totalNodes; q++)
                {
                    indexTree[q] = new int3();
                }
                int leftLeaf = IndexCal(FlattenedSortListTree.Bounds[startBoundIndex].BoundValue, treeDepth);
                int rightLeaf = IndexCal(FlattenedSortListTree.Bounds[endBoundIndex].BoundValue, treeDepth);
                NativeList<int2> newIns = new(Allocator.Temp);
                NativeList<RangeIDInTree> subset = new(Allocator.Temp);
                NativeList<int2> upset = new(Allocator.Temp);
                int j = startBoundIndex;
                int m2 = leftLeaf, m = leftLeaf;
                int i = DimensionIndex;
                int startNodeDimesion = FlattenTree.UpperDimensions[DimensionIndex].Start;

                NativeBound boundInSortedList = FlattenedSortListTree.Bounds[j];
                NativeBound boundInTree = new();
                while (j <= endBoundIndex && m <= rightLeaf)
                {
                    boundInSortedList = FlattenedSortListTree.Bounds[j];
                    NativeNode node = new();
                    int nodeIndex;
                    int nodeIndexInTree;
                    if (IndexCal(boundInSortedList.BoundValue, treeDepth) == m)
                    {
                        for ((short l, int k) = (treeDepth, m); l >= 0; l--, k = k / 2)
                        {
                            nodeIndexInTree = (int)Mathf.Pow(2, l) + k - 1;
                            nodeIndex = nodeIndexInTree + startNodeDimesion;
                            if (boundInSortedList.RangeIdInList.IsUpper == -1)
                            {
                                node = FlattenTree.UpperNodes[nodeIndex];
                                if (node.Count > 0 && indexTree[nodeIndexInTree].y < node.Count)
                                {
                                    boundInTree = FlattenTree.Uppers[indexTree[nodeIndexInTree].y + node.Start];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        int3 indexNode = indexTree[nodeIndexInTree];
                                        indexNode.y++;
                                        indexTree[nodeIndexInTree] = indexNode;
                                        if (indexTree[nodeIndexInTree].y < node.Count)
                                        {
                                            boundInTree = FlattenTree.Uppers[indexTree[nodeIndexInTree].y + node.Start];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (node.Count - indexTree[nodeIndexInTree].y > 0 && node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, 1, false, indexTree[nodeIndexInTree].y, node.Count - indexTree[nodeIndexInTree].y), boundInSortedList.RangeIdInList));
                                }
                                node = FlattenTree.CoverNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, 0, false, 0, node.Count), boundInSortedList.RangeIdInList));
                                }

                                if (l == treeDepth)
                                {
                                    newIns.Add(new(boundInSortedList.RangeIdInList.IndexContainer, boundInSortedList.RangeIdInList.LowerIndexContainer));
                                }
                            }
                            else if (boundInSortedList.RangeIdInList.IsUpper == 1)
                            {
                                node = FlattenTree.LowerNodes[nodeIndex];
                                if (node.Count > 0 && indexTree[nodeIndexInTree].x < node.Count)
                                {
                                    boundInTree = FlattenTree.Lowers[indexTree[nodeIndexInTree].x + node.Start];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        int3 indexNode = indexTree[nodeIndexInTree];
                                        indexNode.x++;
                                        indexTree[nodeIndexInTree] = indexNode;
                                        if (indexTree[nodeIndexInTree].x < node.Count)
                                        {
                                            boundInTree = FlattenTree.Lowers[indexTree[nodeIndexInTree].x + node.Start];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (indexTree[nodeIndexInTree].x > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, -1, false, 0, indexTree[nodeIndexInTree].x), boundInSortedList.RangeIdInList));
                                }
                                if (l == treeDepth)
                                {
                                    for (int idx = 0; idx < newIns.Length; idx++)
                                    {
                                        if (newIns[idx].y == boundInSortedList.RangeIdInList.LowerIndexContainer)
                                        {
                                            newIns.RemoveAt(idx);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        SortMatchInside(boundInSortedList, indexTree, DimensionIndex, m, subset, upset, treeDepth, true);
                        j++;
                        m2 = m;
                    }
                    else
                    {
                        for ((short l, int k) = (treeDepth, m); l >= 0; l--)
                        {
                            nodeIndexInTree = (int)Mathf.Pow(2, l) + k - 1;
                            nodeIndex = nodeIndexInTree + startNodeDimesion;
                            node = FlattenTree.LowerNodes[nodeIndex];
                            if (node.Count > 0)
                            {
                                for (int q = 0; q < newIns.Length; q++)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(
                                        new RangeIDInTree(i, l, k, -1, false, 0, node.Count),
                                        new(i, boundInSortedList.RangeIdInList.Depth, boundInSortedList.RangeIdInList.Index, boundInSortedList.RangeIdInList.IsUpper, boundInSortedList.RangeIdInList.IsInside, newIns[q].x, newIns[q].y)));
                                }
                            }
                            if (l == treeDepth)
                            {
                                node = FlattenTree.InsideNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    for (int q = 0; q < newIns.Length; q++)
                                    {
                                        if (m > IndexCal(FlattenedSortListTree.Bounds[newIns[q].y + startBoundIndex].BoundValue, treeDepth))
                                        {
                                            OverlapSet.AddNoResize(new OverlapID(
                                                new RangeIDInTree(i, l, k, boundInTree.RangeIdInTree.IsUpper, true, 0, node.Count),
                                                new(i, boundInSortedList.RangeIdInList.Depth, boundInSortedList.RangeIdInList.Index, boundInSortedList.RangeIdInList.IsUpper, boundInSortedList.RangeIdInList.IsInside, newIns[q].x, newIns[q].y)));
                                        }
                                    }
                                }
                                if (m == m2)
                                {
                                    SortMatchInside(boundInSortedList, indexTree, DimensionIndex, m, subset, upset, treeDepth, false);
                                }
                            }
                            if ((k + 1) % 2 == 0) k = k / 2;
                            else break;
                        }
                        m++;
                    }
                }
                newIns.Dispose();
                subset.Dispose();
                upset.Dispose();
                indexTree.Dispose();
            }
            private void SortMatchInside(NativeBound boundInSortedList, NativeArray<int3> indexTree, int DimensionIndex, int m, NativeList<RangeIDInTree> subset, NativeList<int2> upset, short treeDepth, bool headEnd)
            {

                NativeBound boundInTree;
                int nodeIndexInTree = (int)Mathf.Pow(2, treeDepth) + m - 1;
                int nodeIndex = nodeIndexInTree + FlattenTree.UpperDimensions[DimensionIndex].Start;
                NativeNode node = FlattenTree.InsideNodes[nodeIndex];
                //Message.Enqueue($"\nSortMatchInside {boundInSortedList.BoundValue} HeadEnd_{headEnd} [{DimensionIndex},{treeDepth},{m}]  node.Count={node.Count}");
                //Message.Enqueue($"SubsetCount={subset.Length} UpsetCount={upset.Length}");
                //Message.Enqueue($"Index {indexTree[nodeIndexInTree].z}\n");
                if (node.Count > 0 && indexTree[nodeIndexInTree].z < node.Count)
                {
                    boundInTree = FlattenTree.Insides[indexTree[nodeIndexInTree].z + node.Start];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        //Message.Enqueue($"Index {indexTree[nodeIndexInTree].z}\n");
                        if (boundInTree.RangeIdInTree.IsUpper == -1)
                        {
                            subset.Add(boundInTree.RangeIdInTree);
                        }
                        else if (boundInTree.RangeIdInTree.IsUpper == 1)
                        {
                            for (int j = 0; j < subset.Length; j++)
                            {
                                if (subset[j].LowerIndexContainer == boundInTree.RangeIdInTree.LowerIndexContainer)
                                {
                                    subset.RemoveAt(j);
                                    break;
                                }
                            }
                            for (int q = 0; q < upset.Length; q++)
                            {
                                //Message.Enqueue($"Overlap {indexTree[nodeIndexInTree].z} & {upset[q].x}\n");
                                OverlapSet.AddNoResize(new OverlapID(boundInTree.RangeIdInTree,
                                    new(DimensionIndex, boundInSortedList.RangeIdInList.Depth, boundInSortedList.RangeIdInList.Index, boundInSortedList.RangeIdInList.IsUpper, boundInSortedList.RangeIdInList.IsInside, upset[q].x, upset[q].y)));
                            }
                        }
                        int3 indexNode = indexTree[nodeIndexInTree];
                        indexNode.z++;
                        indexTree[nodeIndexInTree] = indexNode;
                        if (indexTree[nodeIndexInTree].z < node.Count)
                        {
                            boundInTree = FlattenTree.Insides[indexTree[nodeIndexInTree].z + node.Start];
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                //Message.Enqueue($"SubsetCount={subset.Length} UpsetCount={upset.Length}");
                if (boundInSortedList.RangeIdInTree.IsUpper == -1)
                {
                    if (headEnd)
                    {
                        upset.Add(new int2(boundInSortedList.RangeIdInList.IndexContainer, boundInSortedList.RangeIdInList.LowerIndexContainer));
                    }
                }
                else if (boundInSortedList.RangeIdInTree.IsUpper == 1)
                {
                    if (headEnd)
                        for (int idx = 0; idx < upset.Length; idx++)
                        {
                            if (upset[idx].y == boundInSortedList.RangeIdInList.LowerIndexContainer)
                            {
                                upset.RemoveAt(idx);
                                break;
                            }
                        }
                    for (int q = 0; q < subset.Length; q++)
                    {
                        //Message.Enqueue($"Overlap {subset[q].Start} & {boundInSortedList.RangeIdInList.IndexContainer}\n");
                        OverlapSet.AddNoResize(new OverlapID(subset[q], boundInSortedList.RangeIdInList));
                    }
                }
                //Message.Enqueue($"SubsetCount={subset.Length} UpsetCount={upset.Length}");
            }
        }
    }
}