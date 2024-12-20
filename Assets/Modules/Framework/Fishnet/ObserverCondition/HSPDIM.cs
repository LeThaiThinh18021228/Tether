using Framework;
using Framework.ADS;
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
        public static bool UpdateInterval(float ratio = 5f)
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
                            IEnumerable<Range> overlapRange;
                            if (node.Data.uppers.Count - indexNode.Data.y > 0)
                            {
                                overlapRange = node.Data.uppers.GetRange(indexNode.Data.y, node.Data.uppers.Count - indexNode.Data.y).Select(b => b.range);
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add Overlap Upper from{indexNode.Data.y} to {node.Data.uppers.Count}: {string.Join(",", overlapRange.Select(r => r.ToString()))};\n ");
                            }

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
                            if (indexNode.Data.x > 0)
                            {
                                IEnumerable<Range> overlapRange = node.Data.lowers.GetRange(0, indexNode.Data.x).Select(b => b.range);
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add Overlap Lower from {0} to {indexNode.Data.x}: {string.Join(",", overlapRange.Select(r => r.ToString()))}; \n");
                            }
                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                                sb.Append($"remove {boundInSortedList.range} from newIns; \n");
                            }
                        }
                        sb.Append($"\n");
                    }
                    SortMatchInside(boundInSortedList, tree, indexNode, i, m, subset, upset, sb);
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
                        node = tree[l, k];
                        sb.Append($"node:[{i},{l},{k}]");
                        if (node.Data.lowers.Count > 0)
                        {
                            IEnumerable<Range> overlapRange = node.Data.lowers.Select(b => b.range);
                            newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                            if (!wise)
                                overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                            sb.Append($"\t overlap {node.Data.lowers.Count} lower: {string.Join(",", overlapRange.Select(r => r.ToString()))}");
                        }
                        sb.Append($"\n");
                        if (l == tree.depth)
                        {
                            if (m > m2)
                            {
                                IEnumerable<Range> overlapRange = node.Data.insides.Select(b => b.range);
                                newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                                sb.Append($"\t overlap {node.Data.insides} inside: {string.Join(",", overlapRange.Select(r => r.ToString()))}");
                            }
                            else
                            {
                                SortMatchInside(boundInSortedList, tree, indexNode, i, m, subset, upset, sb);
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
        void MatchingParalel(List<Bound> sortedBounds, BinaryTree<HSPDIMNodeData> tree, NativeHSPDIMNodeData flattenedTree, int i, bool wise = true)
        {
            if (sortedBounds == null || sortedBounds.Count == 0)
                return;

            NativeArray<NativeBound> nativeSortedBounds = new(sortedBounds.Count, Allocator.TempJob);
            for (int j = 0; j < sortedBounds.Count; j++)
            {
                int lowerIndexInContainer = sortedBounds.IndexOf(sortedBounds[j].range.Bounds[i, 0]);
                nativeSortedBounds[j] = sortedBounds[j].ToNativeBound(j, false, -1, lowerIndexInContainer);
            }
            string.Join(",", nativeSortedBounds.Select(b => $"{b.LowerIndex}_{b.RangeIdInList}"));


            NativeList<OverlapID> overlapSet = new(512, Allocator.TempJob);

            MathcingRangeToTreeJob job = new()
            {
                SortedBounds = nativeSortedBounds,
                Tree = flattenedTree,
                DimensionIndex = i,
                TreeDepth = tree.depth,
                OverlapSet = overlapSet.AsParallelWriter(),
            };
            JobHandle handle = job.Schedule(nativeSortedBounds.Length, 64);
            handle.Complete();

            //Debug.Log($"{overlapSet.Length}_{nativeSortedBounds.Length}");
            for (int j = 0; j < overlapSet.Length; j++)
            {
                OverlapID overlap = overlapSet[j];
                var overlapRange = overlapSet[j].MapRangeToTree(tree, sortedBounds);
                var boundInList = sortedBounds[overlap.rangeIDInList];
                boundInList.range.overlapSets[i].AddRange(overlapRange);
                overlapRange.ForEach(r => r.overlapSets[i].Add(boundInList.range));
            }

            nativeSortedBounds.Dispose();
            overlapSet.Dispose();
        }
        private void SortMatchInside(Bound boundInSortedList, BinaryTree<HSPDIMNodeData> tree, TreeNode<Vector3Int> indexNode, int i, int m, List<Range> subset, List<Range> upset, StringBuilder sb, bool wise = true)
        {
            sb.Append($"matching inside range at leaf {m} ");
            TreeNode<HSPDIMNodeData> node = tree[tree.depth, m];
            Bound boundInTree;
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

                        if (upset.Count > 0)
                        {
                            sb.Append($"matching inside range {boundInTree.range} : {string.Join(",", upset.Select(r => r.ToString()))} \n");
                        }
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
                if (subset.Count > 0)
                {
                    boundInSortedList.range.overlapSets[i].AddRange(subset);
                    sb.Append($"add {subset.Count} Overlap inside {boundInSortedList}: {string.Join(",", subset.Select(r => r.ToString()))} \n");
                    if (!wise)
                        subset.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                }
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
                NativeHSPDIMNodeData flattenedTree = new();
                int startLower = 0;
                int startUpper = 0;
                int startCover = 0;
                int startInside = 0;
                List<NativeBound> lowerBounds = new();
                List<NativeBound> upperBounds = new();
                List<NativeBound> coverBounds = new();
                List<NativeBound> insideBounds = new();
                flattenedTree.LowerNodes = new NativeArray<NativeNode>((int)Mathf.Pow(2, tree2[i].depth + 1) - 1, Allocator.TempJob);
                flattenedTree.UpperNodes = new NativeArray<NativeNode>((int)Mathf.Pow(2, tree2[i].depth + 1) - 1, Allocator.TempJob);
                flattenedTree.CoverNodes = new NativeArray<NativeNode>((int)Mathf.Pow(2, tree2[i].depth + 1) - 1, Allocator.TempJob);
                flattenedTree.InsideNodes = new NativeArray<NativeNode>((int)Mathf.Pow(2, tree2[i].depth + 1) - 1, Allocator.TempJob);
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append($"Tree:\n");
                tree2[i].ForEach(node =>
                {
                    int index = (1 << node.depth) + node.index - 1;
                    if (node.Data.lowers.Count > 0)
                    {
                        flattenedTree.LowerNodes[index] = new NativeNode(node.depth, node.index, startLower, node.Data.lowers.Count);
                        startLower += node.Data.lowers.Count;
                        lowerBounds.AddRange(node.Data.lowers.Select((b, j) => b.ToNativeBound(j, false, b.range.Bounds[i, 0].index, node.Data.lowers.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    if (node.Data.uppers.Count > 0)
                    {
                        flattenedTree.UpperNodes[index] = new NativeNode(node.depth, node.index, startUpper, node.Data.uppers.Count);
                        startUpper += node.Data.uppers.Count;
                        upperBounds.AddRange(node.Data.uppers.Select((b, j) => b.ToNativeBound(j, false, b.range.Bounds[i, 0].index,
                           node.Data.uppers.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    if (node.Data.covers.Count > 0)
                    {
                        flattenedTree.CoverNodes[index] = new NativeNode(node.depth, node.index, startCover, node.Data.covers.Count);
                        startCover += node.Data.covers.Count;
                        coverBounds.AddRange(node.Data.covers.Select((b, j) => b.ToNativeBound(j, false, b.range.Bounds[i, 0].index, node.Data.covers.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    if (node.Data.insides.Count > 0)
                    {
                        flattenedTree.InsideNodes[index] = new NativeNode(node.depth, node.index, startInside, node.Data.insides.Count);
                        startInside += node.Data.insides.Count;
                        insideBounds.AddRange(node.Data.insides.Select((b, j) => b.ToNativeBound(j, true, b.range.Bounds[i, 0].index, node.Data.insides.BinarySearch(b.range.Bounds[i, 0]))));
                    }

                    stringBuilder.Append($"[{i},{node.depth},{node.index}] {node}\n");
                });
                PDebug.Log(stringBuilder);
                stringBuilder.Clear();
                flattenedTree.Lowers = new NativeArray<NativeBound>(lowerBounds.ToArray(), Allocator.TempJob);
                flattenedTree.Uppers = new NativeArray<NativeBound>(upperBounds.ToArray(), Allocator.TempJob);
                flattenedTree.Covers = new NativeArray<NativeBound>(coverBounds.ToArray(), Allocator.TempJob);
                flattenedTree.Insides = new NativeArray<NativeBound>(insideBounds.ToArray(), Allocator.TempJob);


                tree1[i].PreOrderEnumerator(tree1[i].Root).ForEach(node =>
                {
                    if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                    {
                        //MatchingParalel(node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], flattenedTree, i, wise);
                        Matching(node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], i, wise);
                        stringBuilder.Append(string.Join(",", node.Data.insides.Select(b => b.range.ToString())));
                    }
                    List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.entity.Modified[i]).ToList();
                    if (crossNodeRanges.Count > 0)
                    {
                        var upper1 = tree1[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                        var upper2 = tree1[i][node.depth, node.index + 2].Data.uppers.Where(n =>
                          n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                        crossNodeRanges.AddRange(upper1);
                        crossNodeRanges.AddRange(upper2);
                        //MatchingParalel(crossNodeRanges, tree2[i], flattenedTree, i, wise);
                        Matching(crossNodeRanges, tree2[i], i, wise);
                    }
                    stringBuilder.Append(string.Join(",", node.Data.lowers.Select(b => b.range.ToString())));

                });
                PDebug.Log(stringBuilder);
                stringBuilder.Clear();
                flattenedTree.Dispose();
            }
        }
        public static void MappingRanges(IEnumerable<Range> ranges, BinaryTree<HSPDIMNodeData>[] tree, List<Bound>[] bounds = null)
        {
            PDebug.Log("MappingRanges");
            foreach (Range r in ranges)
            {
                r.UpdateRange(tree[0].depth);
                for (short i = 0; i < dimension; i++)
                {
                    AddRangeToTree(i, r, tree, bounds);
                }
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
                if (!r.entity.IsServerInitialized || !r.entity.enabled)
                {
                    PDebug.LogError($"enabled {r.entity.enabled} IsServerInitialized {r.entity.IsServerInitialized}");
                }
                sb.Append($"new bound {r} \n");
            }
            PDebug.LogWarning(sb.ToString());
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
            try
            {
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
            }
            catch
            {
                PDebug.LogError($"[{bound.dimId},{bound.range.depthLevel[bound.dimId]},{bound.index}] {bound.range} \n {string.Join(",", container.Select(b => b.range))}");
            }

            bound.index = -1;
        }
        public static void RemoveRangeFromTree(short i, Range range, BinaryTree<HSPDIMNodeData>[] tree)
        {
            if (range.Bounds[i, 0] == null) return;
            if ((range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0))
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
        public static void AddRangeToTree(short i, Range range, BinaryTree<HSPDIMNodeData>[] tree, List<Bound>[] bounds = null)
        {
            Bound lowerBound = range.Bounds[i, 0] = range.Bounds[i, 0] ?? new Bound(i, -1, range);
            Bound upperBound = range.Bounds[i, 1] = range.Bounds[i, 1] ?? new Bound(i, 1, range);
            Bound coverBound = range.Bounds[i, 2] = range.Bounds[i, 2] ?? new Bound(i, 0, range);
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
            [ReadOnly] public NativeHSPDIMNodeData Tree;
            public NativeList<OverlapID>.ParallelWriter OverlapSet;
            public short TreeDepth;
            public int DimensionIndex;

            public void Execute(int index)
            {
                int totalNodes = (int)Mathf.Pow(2, TreeDepth + 1) - 1;
                NativeArray<int3> indexTree = new(totalNodes, Allocator.Temp);
                for (int q = 0; q < totalNodes; q++)
                {
                    indexTree[q] = new int3();
                }
                int leftLeaf = IndexCal(SortedBounds[0].BoundValue, TreeDepth);
                int rightLeaf = IndexCal(SortedBounds[SortedBounds.Length - 1].BoundValue, TreeDepth);
                NativeList<int2> newIns = new(Allocator.Temp);
                NativeList<RangeID> subset = new(Allocator.Temp);
                NativeList<int2> upset = new(Allocator.Temp);
                int j = 0;
                int m2 = leftLeaf, m = leftLeaf;
                int i = DimensionIndex;

                NativeBound boundInSortedList = SortedBounds[j];
                NativeBound boundInTree = new();
                while (j < SortedBounds.Length && m <= rightLeaf)
                {
                    boundInSortedList = SortedBounds[j];
                    NativeNode node = new();
                    int nodeIndex;
                    if (IndexCal(boundInSortedList.BoundValue, TreeDepth) == m)
                    {
                        for ((short l, int k) = (TreeDepth, m); l >= 0; l--, k = k / 2)
                        {
                            nodeIndex = (int)Mathf.Pow(2, l) + k - 1;
                            if (boundInSortedList.RangeIdInTree.IsUpper == -1)
                            {
                                node = Tree.UpperNodes[nodeIndex];
                                if (node.Count > 0 && indexTree[nodeIndex].y < node.Count)
                                {
                                    boundInTree = Tree.Uppers[indexTree[nodeIndex].y + node.Start];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        int3 indexNode = indexTree[nodeIndex];
                                        indexNode.y++;
                                        indexTree[nodeIndex] = indexNode;
                                        if (indexTree[nodeIndex].y < node.Count)
                                        {
                                            boundInTree = Tree.Uppers[indexTree[nodeIndex].y + node.Start];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (node.Count - indexTree[nodeIndex].y > 0 && node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, 1, false, indexTree[nodeIndex].y, node.Count - indexTree[nodeIndex].y), boundInSortedList.RangeIdInList, 0));
                                }

                                //IEnumerable<Range> overlapRange = node.Data.Uppers.GetRange(indexNode.Data.y, node.Data.Uppers.Length - indexNode.Data.y).Select(b => b.range);
                                //boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                ///overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                node = Tree.CoverNodes[nodeIndex];
                                if (node.Count > 0)
                                {

                                    OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, 0, false, 0, node.Count), boundInSortedList.RangeIdInList, 1));
                                    //overlapRange = node.Data.Covers.Select(b => b.range).ToList();
                                    //boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                    ///overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                }

                                if (l == TreeDepth)
                                {
                                    newIns.Add(new(boundInSortedList.RangeIdInList, boundInSortedList.LowerIndex));
                                }
                            }
                            else if (boundInSortedList.RangeIdInTree.IsUpper == 1)
                            {
                                node = Tree.LowerNodes[nodeIndex];
                                if (node.Count > 0 && indexTree[nodeIndex].x < node.Count)
                                {
                                    boundInTree = Tree.Lowers[indexTree[nodeIndex].x + node.Start];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        int3 indexNode = indexTree[nodeIndex];
                                        indexNode.x++;
                                        indexTree[nodeIndex] = indexNode;
                                        if (indexTree[nodeIndex].x < node.Count)
                                        {
                                            boundInTree = Tree.Lowers[indexTree[nodeIndex].x + node.Start];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (indexTree[nodeIndex].x > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, -1, false, 0, indexTree[nodeIndex].x), boundInSortedList.RangeIdInList, 2));
                                }
                                //IEnumerable<Range> overlapRange = node.Data.Lowers.GetRange(0, indexNode.Data.x).Select(b => b.range);
                                //boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                ///overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                if (l == TreeDepth)
                                {
                                    for (int idx = 0; idx < newIns.Length; idx++)
                                    {
                                        if (newIns[idx].y == boundInSortedList.LowerIndex)
                                        {
                                            newIns.RemoveAt(idx);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        SortMatchInside(boundInSortedList, indexTree, m, subset, upset, 10);
                        j++;
                        m2 = m;
                    }
                    else
                    {
                        if (newIns.Length > 0)
                            for ((short l, int k) = (TreeDepth, m); l >= 0; l--)
                            {
                                nodeIndex = (int)Mathf.Pow(2, l) + k - 1;
                                node = Tree.LowerNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    for (int q = 0; q < newIns.Length; q++)
                                    {
                                        OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, -1, false, 0, node.Count), newIns[q].x, 3));
                                    }

                                    //IEnumerable<Range> overlapRange = Tree[l, k].Data.Lowers.Select(b => b.range);
                                    //newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                                    ///overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                                }
                                if (l == TreeDepth)
                                {
                                    if (m > m2)
                                    {
                                        node = Tree.InsideNodes[nodeIndex];
                                        //IEnumerable<Range> overlapRange = Tree[l, k].Data.Insides.Select(b => b.range);
                                        //newIns.ForEach(b => b.overlapSets[i].AddRange(overlapRange));
                                        ///overlapRange.ForEach(r => r.overlapSets[i].AddRange(newIns));
                                        if (node.Count > 0)
                                        {
                                            for (int q = 0; q < newIns.Length; q++)
                                            {
                                                OverlapSet.AddNoResize(new OverlapID(new RangeID(i, l, k, boundInTree.RangeIdInTree.IsUpper, true, 0, node.Count), newIns[q].x, 4));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        for (int q = 0; q < newIns.Length; q++)
                                        {
                                            SortMatchInside(SortedBounds[newIns[q].x], indexTree, m, subset, upset, 15);
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

            private void SortMatchInside(NativeBound boundInSortedList, NativeArray<int3> indexTree, int m, NativeList<RangeID> subset, NativeList<int2> upset, int hint)
            {
                NativeBound boundInTree;
                int nodeIndex = (int)Mathf.Pow(2, TreeDepth) + m - 1;
                NativeNode node = Tree.InsideNodes[nodeIndex];
                if (node.Count > 0 && indexTree[nodeIndex].z < node.Count)
                {
                    boundInTree = Tree.Insides[indexTree[nodeIndex].z + node.Start];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        if (boundInTree.RangeIdInTree.IsUpper == -1)
                        {
                            //subset.Add(boundInTree.range);
                            subset.Add(boundInTree.RangeIdInTree);
                        }
                        else if (boundInTree.RangeIdInTree.IsUpper == 1)
                        {
                            //subset.Remove(boundInTree.range);
                            for (int j = 0; j < subset.Length; j++)
                            {
                                if (subset[j].IndexContainer == boundInTree.RangeIdInTree.IndexContainer)
                                {
                                    subset.RemoveAt(j);
                                    break;
                                }
                            }
                            for (int q = 0; q < upset.Length; q++)
                            {
                                OverlapSet.AddNoResize(new OverlapID(boundInTree.RangeIdInTree, upset[q].x, 5 + hint + ((indexTree[nodeIndex].z == boundInTree.RangeIdInTree.IndexContainer) ? 2 : 3) * 10));
                            }
                            //upset.ForEach(r =>
                            //{
                            //    r.overlapSets[i].Add(boundInTree.range);
                            //    boundInTree.range.overlapSets[i].Add(r);
                            //});
                        }
                        int3 indexNode = indexTree[nodeIndex];
                        indexNode.z++;
                        indexTree[nodeIndex] = indexNode;
                        if (indexTree[nodeIndex].z < node.Count)
                        {
                            boundInTree = Tree.Insides[indexTree[nodeIndex].z + node.Start];
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (boundInSortedList.RangeIdInTree.IsUpper == -1)
                {
                    upset.Add(new int2(boundInSortedList.RangeIdInList, boundInSortedList.LowerIndex));
                }
                else if (boundInSortedList.RangeIdInTree.IsUpper == 1)
                {
                    for (int idx = 0; idx < upset.Length; idx++)
                    {
                        if (upset[idx].y == boundInSortedList.LowerIndex)
                        {
                            upset.RemoveAt(idx);
                            break;
                        }
                    }
                    for (int q = 0; q < subset.Length; q++)
                    {
                        OverlapSet.AddNoResize(new OverlapID(subset[q], boundInSortedList.RangeIdInList, 6 + hint));
                    }
                    //boundInSortedList.range.overlapSets[i].AddRange(subset);
                    //subset.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                }
            }
        }
    }
}