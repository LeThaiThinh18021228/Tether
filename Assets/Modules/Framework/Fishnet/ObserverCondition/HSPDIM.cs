using Framework;
using Framework.ADS;
using Framework.ADS.Paralel;
using Framework.FishNet;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace HSPDIMAlgo
{
    public class HSPDIM : SingletonNetwork<HSPDIM>
    {
        public static readonly float mapSizeEstimate = 100;
        public static readonly float minEntitySubRegSize = 10;
        public static readonly float minEntityUpRegSize = 3;

        public static short subTreeDepth;
        public static short upTreeDepth;
        public static readonly short dimension = 2;

        public List<Range> upRanges = new();
        public List<Range> subRanges = new();
        public List<Range> modifiedUpRanges = new();
        public List<Range> modifiedSubRanges = new();
        public BinaryTree<HSPDIMNodeData>[] upTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMNodeData>(DepthCal(minEntityUpRegSize))).ToArray();
        public BinaryTree<HSPDIMNodeData>[] subTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMNodeData>(DepthCal(minEntitySubRegSize))).ToArray();
        public bool isRunning;

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
        public override void OnStartServer()
        {
            base.OnStartServer();
            subTreeDepth = DepthCal(minEntitySubRegSize);
            upTreeDepth = DepthCal(minEntityUpRegSize);
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
        }
        public static bool UpdateInterval(float ratio = 1f)
        {
            float time = Time.time * ratio;
            return Mathf.FloorToInt(time - Time.deltaTime * ratio) < Mathf.FloorToInt(time);
        }
        private void Update()
        {
            if (!IsServerInitialized) return;
            if (UpdateInterval() && isRunning)
            {
                MappingRangeDynamic(modifiedUpRanges, upTree);
                MappingRangeDynamic(modifiedSubRanges, subTree);
                DynamicMatching();
                LogTree(upTree);
                LogTree(subTree);
            }
        }
        public void GameState_OnChange(GameState prev, GameState next, bool asServer)
        {
            if (!asServer) return;
            if (next == GameState.STARTED)
            {
                MappingRanges(modifiedUpRanges, upTree);
                List<Bound>[] sortedBounds = Enumerable.Range(0, dimension).Select(_ => new List<Bound>(subTreeDepth)).ToArray();
                MappingRanges(modifiedSubRanges, subTree, sortedBounds);
                LogTree(upTree);
                LogTree(subTree);
                for (int i = 0; i < dimension; i++)
                {
                    Matching(sortedBounds[i], upTree[i], i);
                    sortedBounds[i].Clear();
                }
                foreach (var range in modifiedSubRanges)
                {
                    range.UpdateIntersection();
                }
                modifiedUpRanges.Clear();
                modifiedSubRanges.Clear();
                isRunning = true;
            }
        }
        private void Matching(List<Bound> sortedBounds, BinaryTree<HSPDIMNodeData> tree, int i, bool wise = true)
        {
            if (sortedBounds.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("StartMatching\n");
            BinaryTree<Vector3Int> indexTree = new(tree.depth);
            indexTree.PreOrderEnumerator(indexTree.Root).ForEach(t =>
            {
                t.Data.x = 0;
                t.Data.y = 0;
                t.Data.z = 0;
            });
            List<Range> newIns = new();
            List<Range> subset = new();
            List<Range> upset = new();
            int leftLeaf = IndexCal(sortedBounds.First().boundValue, tree.depth);
            int rightLeaf = IndexCal(sortedBounds.Last().boundValue, tree.depth);
            int j = 0;
            int m2 = leftLeaf, m = leftLeaf;
            Bound boundInSortedList;
            Bound boundInTree = null;

            sb.Append($"sortedListCount:{sortedBounds.Count},leftLeaf:{leftLeaf},rightLeaf:{rightLeaf}\n");
            Debug.Log(sb);
            sb.Clear();
            while (j < sortedBounds.Count() && m <= rightLeaf)
            {
                boundInSortedList = sortedBounds[j];
                sb.Append($"bound in SortedList:{boundInSortedList.boundValue},indexLeaf:{m},boundInListIndex:{IndexCal(boundInSortedList.boundValue, tree.depth)}\n");
                TreeNode<HSPDIMNodeData> node;
                TreeNode<Vector3Int> indexNode = null;
                if (IndexCal(boundInSortedList.boundValue, tree.depth) == m)
                {
                    sb.Append("go at leaf");
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--, k = k / 2)
                    {
                        sb.Append($"node:[{l},{k}] -> ");
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

                            IEnumerable<Range> overlapRange = node.Data.uppers.GetRange(indexNode.Data.y, node.Data.uppers.Count - indexNode.Data.y).Select(b => b.range);
                            boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                            if (!wise)
                                overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                            sb.Append($"add Overlap Upper from{indexNode.Data.y} to {node.Data.uppers.Count}: {string.Join(",", overlapRange.Select(r => r.ToString()))};\n ");
                            if (node.Data.covers.Count > 0)
                            {
                                overlapRange = node.Data.covers.Select(b => b.range).ToList();
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add {node.Data.covers.Count} Overlap Cover: {string.Join(",", overlapRange.Select(r => r.ToString()))}; \n");
                            }

                            if (l == tree.depth)
                            {
                                newIns.Add(boundInSortedList.range);
                                sb.Append($"add {boundInSortedList.range} to newIns; \n");
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
                            IEnumerable<Range> overlapRange = node.Data.lowers.GetRange(0, indexNode.Data.x).Select(b => b.range);
                            boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                            if (!wise)
                                overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                            sb.Append($"add Overlap Lower from {0} to {indexNode.Data.x}: {string.Join(",", overlapRange.Select(r => r.ToString()))}; \n");

                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                                sb.Append($"remove {boundInSortedList.range} from newIns; \n");
                            }
                        }
                        sb.Append($"\n");
                    }
                    SortMatchInside(boundInTree, boundInSortedList, tree, indexNode, i, m, subset, upset, sb);
                    j++;
                    m2 = m;
                }
                else
                {
                    sb.Append("leave leaf\n");
                    if (newIns.Count > 0)
                        sb.Append($"add Overlap Lower to  {string.Join(",", newIns.Select(r => r.ToString()))} \n");
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--)
                    {
                        indexNode = indexTree[l, k];
                        sb.Append($"node:[{i},{l},{k}]");
                        if (tree[l, k].Data.lowers.Count > 0)
                        {
                            IEnumerable<Range> overlapRange = tree[l, k].Data.lowers.Select(b => b.range);
                            newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                            if (!wise)
                                overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                            sb.Append($"\t overlap lower: {string.Join(",", overlapRange.Select(r => r.ToString()))}");
                        }
                        sb.Append($"\n");
                        if (l == tree.depth)
                        {
                            if (m > m2)
                            {
                                IEnumerable<Range> overlapRange = tree[l, k].Data.insides.Select(b => b.range);
                                newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                                sb.Append($"\t overlap inside: {string.Join(",", overlapRange.Select(r => r.ToString()))}");
                            }
                            else
                            {
                                SortMatchInside(boundInTree, boundInSortedList, tree, indexNode, i, m, subset, upset, sb);
                            }
                        }
                        if ((k + 1) % 2 == 0) k = k / 2;
                        else break;
                    }
                    m++;
                }
                Debug.Log(sb);
                sb.Clear();
            }
        }
        void MatchingParalel(List<Bound> sortedBounds, BinaryTree<HSPDIMNodeData> tree, FullBinaryTreeNativeDisposable<NativeHSPDIMNodeData> flattenedTree, int i, bool wise = true)
        {
            if (sortedBounds == null || sortedBounds.Count == 0)
                return;

            NativeArray<NativeBound> nativeSortedBounds = new(sortedBounds.Count, Allocator.Temp);
            for (int j = 0; j < sortedBounds.Count; j++)
            {
                nativeSortedBounds[j] = sortedBounds[j].ToNativeBound(j);
            }

            NativeList<OverlapID> overlapSet = new(Allocator.Temp);

            MathcingRangeToTreeJob job = new()
            {
                SortedBounds = nativeSortedBounds,
                Tree = flattenedTree,
                DimensionIndex = i,
                TreeDepth = flattenedTree.Depth,
                OverlapSet = overlapSet.AsParallelWriter(),
            };
            JobHandle handle = job.Schedule(nativeSortedBounds.Length, 64);
            handle.Complete();

            for (int j = 0; j < overlapSet.Length; j++)
            {
                OverlapID overlap = overlapSet[j];
                var overlapRange = overlap.MapRangeToTree(tree);
                sortedBounds[overlap.rangeIDInList].range.overlapSets[i].AddRange(overlapRange);
                overlapRange.ForEach(r => r.overlapSets[i].Add(sortedBounds[overlap.rangeIDInList].range));
            }

            nativeSortedBounds.Dispose();
            overlapSet.Dispose();
        }
        private void SortMatchInside(Bound boundInTree, Bound boundInSortedList, BinaryTree<HSPDIMNodeData> tree, TreeNode<Vector3Int> indexNode, int i, int m, List<Range> subset, List<Range> upset, StringBuilder sb, bool wise = true)
        {
            sb.Append($"matching inside range at leaf {m} ");
            TreeNode<HSPDIMNodeData> node = tree[tree.depth, m];
            sb.Append($"insideIt: {indexNode.Data.z}\n");
            if (node.Data.insides.Count > 0 && indexNode.Data.z < node.Data.insides.Count)
            {
                boundInTree = node.Data.insides[indexNode.Data.z];
                while (boundInTree.boundValue <= boundInSortedList.boundValue)
                {
                    sb.Append($"subset before: {string.Join(",", subset.Select(r => r.ToString()))} \n");
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
                            if (!wise)
                                boundInTree.range.overlapSets[i].Add(r);
                        });

                        sb.Append($"matching inside range {boundInTree.range} : {string.Join(",", upset.Select(r => r.ToString()))} \n");
                    }
                    sb.Append($"subset after: {string.Join(",", subset.Select(r => r.ToString()))} \n");
                    indexNode.Data.z++;
                    if (indexNode.Data.z < node.Data.insides.Count)
                    {
                        boundInTree = node.Data.insides[indexNode.Data.z];
                        sb.Append($"insideIt: {indexNode.Data.z}\n");
                    }
                    else
                    {
                        sb.Append($"\n");
                        break;
                    }
                }
            }

            if (boundInSortedList.isUpper == -1)
            {
                upset.Add(boundInSortedList.range);
            }
            else if (boundInSortedList.isUpper == 1)
            {
                upset.Remove(boundInSortedList.range);
                boundInSortedList.range.overlapSets[i].AddRange(subset);
                sb.Append($"add {subset.Count} Overlap inside {boundInSortedList}: {string.Join(",", subset.Select(r => r.ToString()))} \n");
                if (!wise)
                    subset.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
            }
        }
        private void DynamicMatching()
        {
            PDebug.Log("DynamicMatching");
            upRanges.ForEach(r =>
            {
                for (int i = 0; i < HSPDIM.dimension; i++)
                {
                    if (r.entity.Modified[i])
                    {
                        r.overlapSets[i].ForEach(r2 => r2.overlapSets[i].Remove(r));
                        r.entity.SubRange?.overlapSets[i].ForEach(r2 => r2.overlapSets[i].Remove(r.entity.SubRange));
                        r.overlapSets[i].Clear();
                        r.entity.SubRange?.overlapSets[i].Clear();
                    }
                }
            });
            MatchingTreeToTree(upTree, subTree, false);
            MatchingTreeToTree(subTree, upTree, false);
            subRanges.ForEach(r => { if (r.entity.enabled) { r.UpdateIntersection(); } });
            modifiedUpRanges.ForEach(r => r.entity.Modified = Vector3Bool.@false);
            modifiedSubRanges.ForEach(r => r.entity.Modified = Vector3Bool.@false);
            modifiedUpRanges.Clear();
            modifiedSubRanges.Clear();
        }
        private void MatchingTreeToTree(BinaryTree<HSPDIMNodeData>[] tree1, BinaryTree<HSPDIMNodeData>[] tree2, bool wise = true)
        {
            for (int i = 0; i < dimension; i++)
            {
                FullBinaryTreeNativeDisposable<NativeHSPDIMNodeData> flattenedTree = new(tree2[i].depth, Allocator.Temp);
                tree2[i].ForEach(node =>
                {
                    PDebug.Log("index-" + ((1 << node.depth) + node.index - 1));
                    flattenedTree.Nodes[(int)Mathf.Pow(2, node.depth) + node.index - 1] = new TreeNodeNative<NativeHSPDIMNodeData>(
                        new NativeHSPDIMNodeData()
                        {
                            Lowers = new NativeArray<NativeBound>(node.Data.lowers.Select((b, j) => b.ToNativeBound(j)).ToArray(), Allocator.Temp),
                            Uppers = new NativeArray<NativeBound>(node.Data.uppers.Select((b, j) => b.ToNativeBound(j)).ToArray(), Allocator.Temp),
                            Covers = new NativeArray<NativeBound>(node.Data.covers.Select((b, j) => b.ToNativeBound(j)).ToArray(), Allocator.Temp),
                            Insides = new NativeArray<NativeBound>(node.Data.insides.Select((b, j) => b.ToNativeBound(j)).ToArray(), Allocator.Temp),
                        },
            node.depth,
            node.index
                    );
                    PDebug.Log("index-" + ((int)Mathf.Pow(2, node.depth) + node.index - 1));
                });
                tree1[i].PreOrderEnumerator(tree1[i].Root).ForEach(node =>
                {
                    if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                    {
                        MatchingParalel(node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], flattenedTree, i, wise);
                    }
                    List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.entity.Modified[i]).ToList();
                    if (crossNodeRanges.Count > 0)
                    {
                        var upper1 = tree1[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                        var upper2 = tree1[i][node.depth, node.index + 2].Data.uppers.Where(n =>
                          n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                        crossNodeRanges.AddRange(upper1);
                        crossNodeRanges.AddRange(upper2);
                        MatchingParalel(crossNodeRanges, tree2[i], flattenedTree, i, wise);
                    }

                });
                flattenedTree.Dispose();
            }
        }
        public static void MappingRanges(IEnumerable<Range> ranges, BinaryTree<HSPDIMNodeData>[] tree, List<Bound>[] bounds = null)
        {
            PDebug.Log("MappingRanges");
            foreach (Range r in ranges)
            {
                AddRangeToTree(r, tree, bounds);
                r.UpdateRange(tree[0].depth);
            }
            if (bounds != null)
            {
                foreach (Range r in ranges)
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
                Debug.Log(tree[i].depth);
                tree[i].PreOrderEnumerator(tree[i].Root).ForEach(node =>
                {
                    if (node.Data.uppers.Count > 0) node.Data.uppers.Sort();
                    if (node.Data.lowers.Count > 0) node.Data.lowers.Sort();
                    if (node.depth == tree[i].depth && node.Data.insides.Count > 0) node.Data.insides.Sort();
                });
            }
        }
        public void MappingRangeDynamic(IEnumerable<Range> ranges, BinaryTree<HSPDIMNodeData>[] tree)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"MappingRangesDynamic {ranges.Count()} range\n");
            foreach (Range r in ranges)
            {
                sb.Append($"old bound {r} \n ");
                RemoveRangeFromTree(r, tree);
                r.UpdateRange(tree[0].depth);
                sb.Append($"new bound {r} \n");
                AddRangeToTree(r, tree);
            }
            PDebug.Log(sb.ToString());
        }
        public static void AddBoundToTree(Bound bound, BinaryTree<HSPDIMNodeData> tree, bool inside)
        {
            HSPDIMNodeData node = tree[bound.range.depthLevel[bound.dimId], bound.index].Data;
            List<Bound> container = null;
            int index = -100;
            int count = -100;
            if (inside)
            {
                container = node.insides;
                count = container.Count;
                index = container.BinarySearch(bound);
                if (index < 0) index = ~index;
                container.Insert(index, bound);
            }
            else
                switch (bound.isUpper)
                {
                    case -1:
                        container = node.lowers;
                        count = container.Count;
                        index = container.BinarySearch(bound);
                        if (index < 0) index = ~index;
                        container.Insert(index, bound);
                        break;
                    case 1:
                        container = node.uppers;
                        count = container.Count;
                        index = container.BinarySearch(bound);
                        if (index < 0) index = ~index;
                        container.Insert(index, bound); ;
                        break;
                    case 0:
                        container = node.covers;
                        count = container.Count;
                        //if (index<0) index = ~index;
                        container.Add(bound);
                        break;
                    default:
                        break;
                }
        }
        public static void RemoveBoundFromTree(Bound bound, BinaryTree<HSPDIMNodeData> tree, bool inside)
        {
            HSPDIMNodeData node = tree[bound.range.depthLevel[bound.dimId], bound.index].Data;
            List<Bound> container = null;
            int index = -100;
            int count = -100;
            if (inside)
            {
                container = node.insides;
                count = container.Count;
                index = container.BinarySearch(bound);
                container.RemoveAt(index);
            }
            else
                switch (bound.isUpper)
                {
                    case -1:
                        container = node.lowers;
                        count = container.Count;
                        index = container.BinarySearch(bound);
                        container.RemoveAt(index);
                        break;
                    case 1:
                        container = node.uppers;
                        count = container.Count;
                        index = container.BinarySearch(bound);
                        container.RemoveAt(index);
                        break;
                    case 0:
                        container = node.covers;
                        count = container.Count;
                        //index = container.BinarySearch(bound);
                        container.Remove(bound);
                        break;
                    default:
                        break;
                }
            bound.index = -1;
        }
        public static void RemoveRangeFromTree(Range range, BinaryTree<HSPDIMNodeData>[] tree)
        {
            for (short i = 0; i < dimension; i++)
            {
                if (range.Bounds[i, 0] == null) continue;
                if ((range.Bounds[i, 0].index == -1 || range.Bounds[i, 1].index == -1))
                {
                    Debug.LogWarning($"{range} just init/remove");
                }
                else
                {
                    if (range.entity.Modified[i])
                    {
                        if (range.Bounds[i, 1].index - range.Bounds[i, 0].index == 0 && range.depthLevel[i] == tree[i].depth)
                        {
                            RemoveBoundFromTree(range.Bounds[i, 0], tree[i], true);
                            RemoveBoundFromTree(range.Bounds[i, 1], tree[i], true);
                        }
                        else
                        {
                            RemoveBoundFromTree(range.Bounds[i, 0], tree[i], false);
                            RemoveBoundFromTree(range.Bounds[i, 1], tree[i], false);
                            if (range.Bounds[i, 1].index - range.Bounds[i, 0].index == 2) RemoveBoundFromTree(range.Bounds[i, 2], tree[i], false);
                        }
                    }
                }

            }
        }
        public static void AddRangeToTree(Range range, BinaryTree<HSPDIMNodeData>[] tree, List<Bound>[] bounds = null)
        {
            for (short i = 0; i < dimension; i++)
            {
                if (range.entity.Modified[i])
                {
                    range.Bounds[i, 0] = range.Bounds[i, 0] ?? new Bound(i, -1, range);
                    range.Bounds[i, 1] = range.Bounds[i, 1] ?? new Bound(i, 1, range);
                    range.Bounds[i, 2] = range.Bounds[i, 2] ?? new Bound(i, 0, range);
                    Bound lowerBound = range.Bounds[i, 0];
                    Bound upperBound = range.Bounds[i, 1];
                    Bound coverBound = range.Bounds[i, 2];
                    if (upperBound.index - lowerBound.index == 0 && range.depthLevel[i] == tree[i].depth)
                    {
                        AddBoundToTree(lowerBound, tree[i], true);
                        AddBoundToTree(upperBound, tree[i], true);
                    }
                    else
                    {
                        AddBoundToTree(lowerBound, tree[i], false);
                        AddBoundToTree(upperBound, tree[i], false);
                        if (upperBound.index - lowerBound.index == 2) AddBoundToTree(coverBound, tree[i], false);
                    }

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
        private static void LogTree(BinaryTree<HSPDIMNodeData>[] tree)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("Tree:\n");
            for (int i = 0; i < dimension; i++)
            {
                foreach (var node in tree[i])
                {
                    if (!node.Data.IsEmpty())
                    {
                        stringBuilder.AppendFormat("{0} [{1},{2},{3}]\n", node.Data.ToString(), i, node.depth, node.index);
                    }
                }
            }
            Debug.Log(stringBuilder.ToString());
        }

        [BurstCompile]
        public struct MathcingRangeToTreeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<NativeBound> SortedBounds;
            [ReadOnly] public FullBinaryTreeNativeDisposable<NativeHSPDIMNodeData> Tree;
            public NativeArray<int> NodeDataIndices;
            public NativeList<OverlapID>.ParallelWriter OverlapSet;
            public int TreeDepth;
            public int DimensionIndex;

            public void Execute(int index)
            {

                int leftLeaf = IndexCal(SortedBounds[0].BoundValue, TreeDepth);
                int rightLeaf = IndexCal(SortedBounds[SortedBounds.Length - 1].BoundValue, TreeDepth);
                NativeList<int> newIns = new(Allocator.Temp);
                NativeList<RangeID> subset = new(Allocator.Temp);
                NativeList<int> upset = new(Allocator.Temp);
                int j = 0;
                int m2 = leftLeaf, m = leftLeaf;
                int i = DimensionIndex;
                FullBinaryTreeNative<int3> indexTree = new FullBinaryTreeNative<int3>();
                for (int q = 0; q < indexTree.Nodes.Length; q++)
                {
                    TreeNodeNative<int3> node = indexTree.Nodes[q];
                    node.Data.x = 0;
                    node.Data.y = 0;
                    node.Data.z = 0;
                    indexTree.Nodes[q] = node;
                }
                NativeBound boundInSortedList = SortedBounds[j];
                NativeBound boundInTree = new();
                while (j < SortedBounds.Length && m <= rightLeaf)
                {
                    boundInSortedList = SortedBounds[j];
                    TreeNodeNative<NativeHSPDIMNodeData> node;
                    TreeNodeNative<int3> indexNode = new();
                    if (IndexCal(boundInSortedList.BoundValue, Tree.Depth) == m)
                    {

                        for ((short l, int k) = (Tree.Depth, m); l >= 0; l--, k = k / 2)
                        {
                            node = Tree[l, k];
                            indexNode = indexTree[l, k];
                            if (boundInSortedList.IsUpper == -1)
                            {
                                if (node.Data.Uppers.Length > 0 && indexNode.Data.y < node.Data.Uppers.Length)
                                {
                                    boundInTree = node.Data.Uppers[indexNode.Data.y];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        indexNode.Data.y++;
                                        if (indexNode.Data.y < node.Data.Uppers.Length)
                                        {
                                            boundInTree = node.Data.Uppers[indexNode.Data.y];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, boundInSortedList.IsUpper, false, indexNode.Data.y, node.Data.Uppers.Length - indexNode.Data.y), boundInSortedList.RangeIdInList));
                                //IEnumerable<Range> overlapRange = node.Data.Uppers.GetRange(indexNode.Data.y, node.Data.Uppers.Length - indexNode.Data.y).Select(b => b.range);
                                //boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                //overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                if (node.Data.Covers.Length > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, boundInSortedList.IsUpper, false, 0, node.Data.Covers.Length), boundInSortedList.RangeIdInList));
                                    //overlapRange = node.Data.Covers.Select(b => b.range).ToList();
                                    //boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                    //overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                }

                                if (l == Tree.Depth)
                                {
                                    newIns.Add(boundInSortedList.RangeIdInList);
                                }
                            }
                            else if (boundInSortedList.IsUpper == 1)
                            {
                                if (node.Data.Lowers.Length > 0 && indexNode.Data.x < node.Data.Lowers.Length)
                                {
                                    boundInTree = node.Data.Lowers[indexNode.Data.x];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        indexNode.Data.x++;
                                        if (indexNode.Data.x < node.Data.Lowers.Length)
                                        {
                                            boundInTree = node.Data.Lowers[indexNode.Data.x];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, boundInSortedList.IsUpper, false, 0, indexNode.Data.x), boundInSortedList.RangeIdInList));
                                //IEnumerable<Range> overlapRange = node.Data.Lowers.GetRange(0, indexNode.Data.x).Select(b => b.range);
                                //boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                //overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                if (l == Tree.Depth)
                                {
                                    for (int idx = 0; idx < newIns.Length; idx++)
                                    {
                                        if (newIns[idx] == boundInSortedList.RangeIdInList)
                                        {
                                            newIns.RemoveAt(idx);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        SortMatchInside(boundInTree, boundInSortedList, Tree, indexNode, i, m, subset, upset);
                        j++;
                        m2 = m;
                    }
                    else
                    {
                        if (newIns.Length > 0)
                            for ((short l, int k) = (Tree.Depth, m); l >= 0; l--)
                            {
                                indexNode = indexTree[l, k];
                                if (Tree[l, k].Data.Lowers.Length > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, boundInSortedList.IsUpper, false, 0, Tree[l, k].Data.Lowers.Length), boundInSortedList.RangeIdInList));
                                    //IEnumerable<Range> overlapRange = Tree[l, k].Data.Lowers.Select(b => b.range);
                                    //newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                                    //overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                                }
                                if (l == Tree.Depth)
                                {
                                    if (m > m2)
                                    {
                                        //IEnumerable<Range> overlapRange = Tree[l, k].Data.Insides.Select(b => b.range);
                                        //newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                                        //overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                                        OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, boundInSortedList.IsUpper, false, 0, Tree[l, k].Data.Insides.Length), boundInSortedList.RangeIdInList));
                                    }
                                    else
                                    {
                                        for (int q = 0; q < SortedBounds.Length; q++)
                                        {
                                            SortMatchInside(SortedBounds[newIns[q]], boundInSortedList, Tree, indexNode, i, m, subset, upset);
                                        }
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

            private void SortMatchInside(NativeBound boundInTree, NativeBound boundInSortedList, FullBinaryTreeNativeDisposable<NativeHSPDIMNodeData> tree, TreeNodeNative<int3> indexNode, int i, int m, NativeList<RangeID> subset, NativeList<int> upset)
            {
                TreeNodeNative<NativeHSPDIMNodeData> node = tree[tree.Depth, m];
                if (node.Data.Insides.Length > 0 && indexNode.Data.z < node.Data.Insides.Length)
                {
                    boundInTree = node.Data.Insides[indexNode.Data.z];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        if (boundInTree.IsUpper == -1)
                        {
                            //subset.Add(boundInTree.range);
                            subset.Add(new RangeID(i, tree.Depth, m, boundInTree.IsUpper, true, indexNode.Data.z, 1));
                        }
                        else if (boundInTree.IsUpper == 1)
                        {
                            //subset.Remove(boundInTree.range);
                            for (int j = 0; j < subset.Length; j++)
                            {
                                if (subset[j].Start == boundInTree.RangeIdInTree.Start)
                                {
                                    subset.RemoveAt(j);
                                    break;
                                }
                            }
                            for (int q = 0; q < upset.Length; q++)
                            {
                                OverlapSet.AddNoResize(new OverlapID(new RangeID(i, tree.Depth, m, boundInTree.IsUpper, true, indexNode.Data.z, 1), upset[q]));
                            }
                            //upset.ForEach(r =>
                            //{
                            //    r.overlapSets[i].Add(boundInTree.range);
                            //    boundInTree.range.overlapSets[i].Add(r);
                            //});
                        }
                        indexNode.Data.z++;
                        if (indexNode.Data.z < node.Data.Insides.Length)
                        {
                            boundInTree = node.Data.Insides[indexNode.Data.z];
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (boundInSortedList.IsUpper == -1)
                {
                    upset.Add(boundInSortedList.RangeIdInList);
                }
                else if (boundInSortedList.IsUpper == 1)
                {
                    upset.RemoveAt(upset.IndexOf(boundInSortedList.RangeIdInList));
                    OverlapSet.AddNoResize(new OverlapID(new RangeID(i, tree.Depth, m, boundInSortedList.IsUpper, true, 0, Tree[tree.Depth, m].Data.Insides.Length), boundInSortedList.RangeIdInList));
                    //boundInSortedList.range.overlapSets[i].AddRange(subset);
                    //subset.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                }
            }

        }
    }
}