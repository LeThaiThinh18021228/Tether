using Framework;
using Framework.ADS;
using Framework.FishNet;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
namespace HSPDIMAlgo
{
    public class HSPDIM : SingletonNetwork<HSPDIM>
    {
        public static float mapSizeEstimate = 100;
        public static float minEntitySubRegSize = 10;
        public static float minEntityUpRegSize = 3;

        public static short subTreeDepth = DepthCal(minEntitySubRegSize);
        public static short upTreeDepth = DepthCal(minEntityUpRegSize);
        public static short dimension = 2;
        public static int intervalUpdate = 4;

        public HashSet<Range> upRanges = new();
        public HashSet<Range> subRanges = new();
        public List<Bound>[] sortedBounds = Enumerable.Range(0, dimension).Select(_ => new List<Bound>(subTreeDepth)).ToArray();
        public BinaryTree<HSPDIMRanges>[] upTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMRanges>(upTreeDepth)).ToArray();
        public BinaryTree<HSPDIMRanges>[] subTree = Enumerable.Range(0, dimension).Select(_ => new BinaryTree<HSPDIMRanges>(subTreeDepth)).ToArray();
        public bool isRunning;

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
                MappingRangeDynamic(upRanges, upTree);
                MappingRangeDynamic(subRanges, subTree);
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
                MappingRanges(upRanges, upTree);
                MappingRanges(subRanges, subTree, sortedBounds);
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("Uptree:");
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in upTree[i])
                    {
                        if (!node.Data.IsEmpty())
                        {
                            stringBuilder.AppendFormat("{0} [{1},{2},{3}] ", node.Data.ToString(), i, node.depth, node.index);
                        }
                    }
                }
                Debug.Log(stringBuilder.ToString());
                stringBuilder.Clear();
                stringBuilder.Append("Subtree:");
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in subTree[i])
                    {
                        if (!node.Data.IsEmpty())
                        {
                            stringBuilder.AppendFormat("{0} [{1},{2},{3}] ", node.Data.ToString(), i, node.depth, node.index);
                        }
                    }
                }
                Debug.Log(stringBuilder.ToString());
                stringBuilder.Clear();
                stringBuilder.Append("SortedBounds:");
                for (int i = 0; i < dimension; i++)
                {
                    sortedBounds[i].ForEach(b => stringBuilder.Append($"{b},"));
                    stringBuilder.Append("\n");
                }
                Debug.Log(stringBuilder.ToString());
                for (int i = 0; i < dimension; i++)
                {
                    Matching(sortedBounds[i], upTree[i], i);
                    sortedBounds[i].Clear();
                }
                stringBuilder.Clear();

                foreach (var range in subRanges)
                {
                    stringBuilder.Append($"Range {range.entity.name}_{range.oldPos}\n");
                    for (int i = 0; i < dimension; i++)
                    {
                        stringBuilder.Append($"{i}");
                        stringBuilder.Append("\n");
                        range.overlapSets[i].ForEach(i => stringBuilder.Append($"{i.entity.name}_{i.oldPos} "));
                        stringBuilder.Append("\n");
                    }
                    range.UpdateIntersection();
                    stringBuilder.Append($" intersect with:");
                    range.intersection.ForEach(i => stringBuilder.Append($"{i.entity.name}_{i.oldPos}+&"));
                    stringBuilder.Append("\n");
                    Debug.Log(stringBuilder.ToString());
                    stringBuilder.Clear();
                }
                upRanges.Clear();
                subRanges.Clear();
                isRunning = true;
            }
        }
        private void Matching(List<Bound> sortedBounds, BinaryTree<HSPDIMRanges> tree, int i)
        {
            if (sortedBounds.Count == 0) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("StartMatching\n");
            tree.PreOrderEnumerator(tree.Root).ForEach(t =>
            {
                t.Data.lowerIt = 0;
                t.Data.upperIt = 0;
                t.Data.insideIt = 0;
            });
            List<Range> newIns = new();
            List<Bound> subset = new();
            List<Bound> upset = new();
            int leftLeaf = IndexCal(sortedBounds.First().boundValue, tree.depth);
            int rightLeaf = IndexCal(sortedBounds.Last().boundValue, tree.depth);
            int j = 0;
            int m = leftLeaf;
            Bound boundInSortedList;
            Bound boundInTree = null;

            sb.Append($"sortedListCount:{sortedBounds.Count},leftLeaf:{leftLeaf},rightLeaf:{rightLeaf}\n");
            Debug.Log(sb);
            sb.Clear();
            while (j < sortedBounds.Count() && m <= rightLeaf)
            {
                boundInSortedList = sortedBounds[j];
                sb.Append($"bound in SortedList:{boundInSortedList.boundValue},indexLeaf:{m},boundInListIndex:{IndexCal(boundInSortedList.boundValue, tree.depth)}\n");
                if (IndexCal(boundInSortedList.boundValue, tree.depth) == m)
                {
                    sb.Append("go at leaf node:");
                    TreeNode<HSPDIMRanges> node;
                    for ((short l, int k) = (tree.depth, m); l >= 0; l--, k = k / 2)
                    {
                        sb.Append($"node:[{l},{k}] -> ");
                        node = tree[l, k];
                        if (boundInSortedList.isUpper == -1)
                        {
                            if (node.Data.uppers.Count > 0 && node.Data.upperIt < node.Data.uppers.Count)
                            {
                                boundInTree = node.Data.uppers[node.Data.upperIt];
                                while (boundInTree.boundValue <= boundInSortedList.boundValue && node.Data.upperIt < node.Data.uppers.Count - 1)
                                {
                                    node.Data.upperIt++;
                                    boundInTree = node.Data.uppers[node.Data.upperIt];
                                }
                                IEnumerable<Range> overlapRange = node.Data.uppers.GetRange(node.Data.upperIt, node.Data.uppers.Count - node.Data.upperIt).Select(b => b.range);
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                //overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add Overlap Upper from{node.Data.upperIt} to {node.Data.uppers.Count}: {string.Join(",", overlapRange.Select(r => r.ToString()))};\n ");
                            }
                            if (node.Data.covers.Count > 0)
                            {
                                IEnumerable<Range> overlapRange = node.Data.covers.Select(b => b.range).ToList();
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                //overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
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
                            if (node.Data.lowers.Count > 0 && node.Data.lowerIt < node.Data.lowers.Count)
                            {
                                boundInTree = node.Data.lowers[node.Data.lowerIt];
                                while (boundInTree.boundValue < boundInSortedList.boundValue && node.Data.lowerIt < node.Data.lowers.Count - 1)
                                {
                                    node.Data.lowerIt++;
                                    boundInTree = node.Data.lowers[node.Data.lowerIt];
                                }
                                int count = Mathf.Clamp(node.Data.lowerIt, 0, node.Data.lowers.Count);
                                if (count > 0)
                                {
                                    IEnumerable<Range> overlapRange = node.Data.lowers.GetRange(0, count).Select(b => b.range);
                                    boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                    //overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                    sb.Append($"add Overlap Lower from {0} to {count}: {string.Join(",", overlapRange.Select(r => r.ToString()))}; \n");
                                }

                            }
                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                                sb.Append($"remove {boundInSortedList.range} from newIns; \n");
                            }
                        }
                        sb.Append($"\n");
                    }
                    sb.Append($"matching inside range at leaf {m}\n");
                    // matching inside range
                    {
                        node = tree[tree.depth, m];
                        if (node.Data.insides.Count > 0 && node.Data.insideIt < node.Data.insides.Count)
                        {
                            boundInTree = node.Data.insides[node.Data.insideIt];
                            while (node.Data.insideIt < node.Data.insides.Count && boundInTree.boundValue <= boundInSortedList.boundValue)
                            {
                                boundInTree = node.Data.insides[node.Data.insideIt];
                                if (boundInTree.isUpper == -1)
                                {
                                    subset.Add(boundInTree);
                                }
                                else if (boundInTree.isUpper == 1)
                                {
                                    subset.Remove(boundInTree);
                                    upset.ForEach(r => r.range.overlapSets[i].Add(boundInTree.range));
                                    sb.Append($"matching inside range at leaf {boundInTree} to : {string.Join(",", upset.Select(r => r.ToString()))} \n");
                                }
                                node.Data.insideIt++;
                            }
                        }

                        if (boundInSortedList.isUpper == -1)
                        {
                            upset.Add(boundInSortedList);
                        }
                        else if (boundInSortedList.isUpper == 1)
                        {
                            upset.Remove(boundInSortedList);
                            boundInSortedList.range.overlapSets[i].AddRange(subset.Select(b => b.range));
                            sb.Append($"add {subset.Count} Overlap {boundInSortedList}: {string.Join(",", subset.Select(r => r.ToString()))} \n");
                        }
                    }
                    //
                    j++;
                }
                else
                {
                    sb.Append("leave leaf\n");
                    for (int p = 0; p < newIns.Count; p++)
                    {
                        for ((short l, int k) = (tree.depth, m); l >= 0; l--)
                        {
                            sb.Append($"node:[{i},{l},{k}]");
                            if (tree[l, k].Data.lowers.Count > 0)
                            {
                                IEnumerable<Range> overlapRange = tree[l, k].Data.lowers.Select(b => b.range);
                                newIns[p].overlapSets[i].AddRange(overlapRange);
                                //overlapRange.ForEach(r => r.overlapSets[i].Add(newIns[p]));
                                sb.Append($"add {overlapRange.Count()} Overlap Lower to {newIns[p]}:  {string.Join(",", overlapRange.Select(r => r.ToString()))} \n");
                            }
                            if ((k + 1) % 2 == 0) k = k / 2;
                            else break;
                        }
                    }
                    m++;
                }
                Debug.Log(sb);
                sb.Clear();
            }
        }
        private void DynamicMatching()
        {
            PDebug.Log("DynamicMatching");
            MatchingTreeToTree(upTree, subTree);
            MatchingTreeToTree(subTree, upTree);
            foreach (var range in subRanges)
            {
                range.UpdateIntersection();
            }
            upRanges.ForEach(r =>
            {
                r.entity.Modified = Vector3Bool.@false;
            });
            subRanges.ForEach(r =>
            {
                r.entity.Modified = Vector3Bool.@false;
            });
            upRanges.Clear();
            subRanges.Clear();
        }
        private void MatchingTreeToTree(BinaryTree<HSPDIMRanges>[] tree1, BinaryTree<HSPDIMRanges>[] tree2)
        {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < dimension; i++)
            {
                tree1[i].PreOrderEnumerator(tree1[i].Root).ForEach(node =>
                {
                    if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
                    {
                        Matching(node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], i);
                    }
                    List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.entity.Modified[i]).ToList();
                    if (crossNodeRanges.Count > 0)
                    {
                        var upper1 = tree1[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                        var upper2 = tree1[i][node.depth, node.index + 2].Data.uppers.Where(n =>
                          n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                        //s.Append($"[{i},{node.depth},{node.index}] {crossNodeRanges.Count}({string.Join(",", crossNodeRanges.Select(r => r.ToString()))}) = {(upper1?.Count() ?? 0)}({string.Join(",", upper1?.Select(r => r.ToString()) ?? Array.Empty<string>())}) + {(upper2?.Count() ?? 0)}({string.Join(",", upper2?.Select(r => r.ToString()) ?? Array.Empty<string>())})\n");
                        crossNodeRanges.AddRange(upper1);
                        crossNodeRanges.AddRange(upper2);
                        if (!IsSorted(crossNodeRanges)) PDebug.LogWarning("NOT SORTED");
                        Matching(crossNodeRanges, tree2[i], i);
                    }

                });
            }
            //Debug.Log(s);
        }
        public static void MappingRanges(IEnumerable<Range> ranges, BinaryTree<HSPDIMRanges>[] tree, List<Bound>[] bounds = null)
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
        public void MappingRangeDynamic(IEnumerable<Range> ranges, BinaryTree<HSPDIMRanges>[] tree)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"MappingRangesDynamic {ranges.Count()} range\n");
            foreach (Range r in ranges)
            {
                sb.Append($"old bound {r} \n ");
                if (r.entity.Modified == Vector3Bool.@false) Debug.LogWarning($"{r} not modified??");
                RemoveRangeFromTree(r, tree);
            }
            foreach (Range r in ranges)
            {
                r.UpdateRange(tree[0].depth);
                sb.Append($"new bound {r} \n");
                if (r.entity.Modified == Vector3Bool.@false) Debug.LogWarning($"{r} not modified??");
                AddRangeToTree(r, tree);
            }
            PDebug.Log(sb.ToString());
        }
        public static void AddBoundToTree(Bound bound, BinaryTree<HSPDIMRanges> tree, bool inside)
        {
            HSPDIMRanges node = tree[bound.range.depthLevel[bound.dimId], bound.index].Data;
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
            catch (System.Exception)
            {
                PDebug.Log($"add {bound.range} an {bound} is {(inside ? "" : "not")} inside of {bound.dimId}_[{bound.range.depthLevel[bound.dimId]},{bound.index}] at {index} in list count {count} [{string.Join(", ", container.Select(c => $"{c}_{c.range.GetHashCode()}"))}]");
                PDebug.Log("IsSorted: " + IsSorted(container));
                Time.timeScale = 0;
                throw;
            }
            finally
            {
                if (!IsSorted(container) || !container.Contains(bound))
                {
                    PDebug.LogError($"add {bound.range} an {bound} is {(inside ? "" : "not")} inside of {bound.dimId}_[{bound.range.depthLevel[bound.dimId]},{bound.index}] at {index} in list count {count} [{string.Join(", ", container.Select(c => $"{c}_{c.range.GetHashCode()}"))}]");
                    Time.timeScale = 0;
                }
            }
        }
        public static void RemoveBoundFromTree(Bound bound, BinaryTree<HSPDIMRanges> tree, bool inside)
        {
            HSPDIMRanges node = tree[bound.range.depthLevel[bound.dimId], bound.index].Data;
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
            catch (System.Exception)
            {
                PDebug.Log($"remove {bound.range} an {bound} is {(inside ? "" : "not")} inside of {bound.dimId}_[{bound.range.depthLevel[bound.dimId]},{bound.index}] at {index} in list count {count} [{string.Join(", ", container.Select(c => $"{c}_{c.range.GetHashCode()}"))}]");
                PDebug.Log("IsSorted: " + IsSorted(container));
                Time.timeScale = 0;
                throw;
            }
            finally
            {
                if (!IsSorted(container))
                {
                    PDebug.LogError($"remove {bound.range} an {bound} is {(inside ? "" : "not")} inside of {bound.dimId}_[{bound.range.depthLevel[bound.dimId]},{bound.index}] at {index} in list count {count} [{string.Join(", ", container.Select(c => $"{c}_{c.range.GetHashCode()}"))}]");
                    Time.timeScale = 0;
                }
            }

            bound.index = -1;
        }
        public static void RemoveRangeFromTree(Range range, BinaryTree<HSPDIMRanges>[] tree)
        {
            for (short i = 0; i < dimension; i++)
            {
                if (range.Bounds[i, 0].index == -1 || range.Bounds[i, 1].index == -1)
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
        public static void AddRangeToTree(Range range, BinaryTree<HSPDIMRanges>[] tree, List<Bound>[] bounds = null)
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
        private static void LogTree(BinaryTree<HSPDIMRanges>[] tree)
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
    }
}