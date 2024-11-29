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
        public static bool UpdateInterval(float ratio = 2f)
        {
            float time = Time.time * ratio;
            return Mathf.FloorToInt(time - Time.deltaTime * ratio) < Mathf.FloorToInt(time);
        }
        private void Update()
        {
            if (!IsServerInitialized) return;
            if (UpdateInterval() && isRunning)
            {
                StringBuilder stringBuilder = new StringBuilder();
                MappingRangeDynamic(upRanges, upTree);
                MappingRangeDynamic(subRanges, subTree);
                foreach (var range in subRanges)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        if (range.modified[i])
                        {
                            range.overlapSets[i].Clear();
                        }
                    }
                }

                foreach (var range in upRanges)
                {
                    for (int i = 0; i < dimension; i++)
                    {
                        if (range.modified[i])
                        {
                            //foreach (var range2 in range.overlapSets[i])
                            //{
                            //    range2.overlapSets[i].Clear();
                            //}
                            range.overlapSets[i].Clear();
                        }
                    }
                }
                DynamicMatching();
                foreach (var range in subRanges)
                {
                    stringBuilder.Append($"Range {range.entity.name}_{range.oldPos} intersection\n");
                    for (int i = 0; i < dimension; i++)
                    {
                        stringBuilder.Append($"{i}");
                        stringBuilder.Append("\n");
                        range.overlapSets[i].ForEach(i => stringBuilder.Append($"{i.entity.name}_{i.oldPos} "));
                        stringBuilder.Append("\n");
                    }
                    range.UpdateIntersection();
                    stringBuilder.Append($"Intersect with: ");
                    range.intersection.ForEach(i => stringBuilder.Append($"{i.entity.name}_{i.oldPos} + "));
                    stringBuilder.Append("\n");
                    //Debug.Log(stringBuilder.ToString());
                    stringBuilder.Clear();
                    if (range.modified != Vector3Bool.@false)
                    {
                        Debug.Log("Not process modified all");
                    }
                }
                upRanges.Clear();
                subRanges.Clear();
                stringBuilder.Append("Tree:\n");
                for (int i = 0; i < dimension; i++)
                {
                    foreach (var node in upTree[i])
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
                    sortedBounds[i].ForEach(b => stringBuilder.Append($"{b.boundValue},"));
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
            while (j < sortedBounds.Count() && m <= rightLeaf)
            {
                boundInSortedList = sortedBounds[j];
                sb.Append($"bound {j} In SortedList:{boundInSortedList.boundValue}_upper_({boundInSortedList.isUpper}),indexLeaf:{m},boundInListIndex:{IndexCal(boundInSortedList.boundValue, tree.depth)}\n");
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
                            if (node.Data.uppers.Count > 0)
                            {
                                boundInTree = node.Data.uppers[node.Data.upperIt];
                                while (boundInTree.boundValue <= boundInSortedList.boundValue && node.Data.upperIt < node.Data.uppers.Count - 1)
                                {
                                    node.Data.upperIt++;
                                    boundInTree = node.Data.uppers[node.Data.upperIt];
                                }
                                boundInSortedList.range.overlapSets[i].AddRange(node.Data.uppers.GetRange(node.Data.upperIt, node.Data.uppers.Count - node.Data.upperIt).Select(b => b.range));
                                sb.Append($"add Overlap Upper from{node.Data.upperIt} to {node.Data.uppers.Count}");
                            }
                            if (node.Data.covers.Count > 0)
                            {
                                boundInSortedList.range.overlapSets[i].AddRange(node.Data.covers.Select(b => b.range));
                                sb.Append($"add Overlap {node.Data.covers.Count} Cover,");
                            }

                            if (l == tree.depth)
                            {
                                newIns.Add(boundInSortedList.range);
                                sb.Append($"add {sortedBounds.IndexOf(boundInTree)} to newIns,");
                            }
                        }
                        else if (boundInSortedList.isUpper == 1)
                        {
                            if (node.Data.lowers.Count > 0)
                            {
                                boundInTree = node.Data.lowers[node.Data.lowerIt];
                                while (boundInTree.boundValue < boundInSortedList.boundValue && node.Data.lowerIt < node.Data.lowers.Count - 1)
                                {
                                    node.Data.lowerIt++;
                                    boundInTree = node.Data.lowers[node.Data.lowerIt];
                                }
                                int count = Mathf.Clamp(node.Data.lowerIt - 1, 0, node.Data.lowers.Count);
                                if (count > 0)
                                    boundInSortedList.range.overlapSets[i].AddRange(node.Data.lowers.GetRange(0, count).Select(b => b.range));
                                sb.Append($"add Overlap Lower from {0} to {count}");
                            }
                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                                sb.Append($"remove {sortedBounds.IndexOf(boundInTree)} from newIns,");
                            }
                        }
                        sb.Append($"\n");
                    }
                    sb.Append($"\n matching inside range at leaf {m}");
                    // matching inside range
                    {
                        node = tree[tree.depth, m];
                        while (node.Data.insideIt < node.Data.insides.Count - 1)
                        {
                            boundInTree = node.Data.insides[node.Data.insideIt];
                            sb.Append($"matching inside range at leaf {boundInTree}");
                            if (boundInTree.isUpper == -1)
                            {
                                subset.Add(boundInTree);
                            }
                            else if (boundInSortedList.isUpper == 1)
                            {
                                subset.Remove(boundInTree);
                                upset.ForEach(r => r.range.overlapSets[i].Add(boundInTree.range));
                            }
                            node.Data.insideIt++;
                        }
                        if (boundInSortedList.isUpper == -1)
                        {
                            upset.Add(boundInSortedList);
                        }
                        else if (boundInSortedList.isUpper == 1)
                        {
                            upset.Remove(boundInSortedList);
                            boundInSortedList.range.overlapSets[i].AddRange(subset.Select(b => b.range));
                            sb.Append($", add Overlap {subset.Count} Inside");
                        }
                    }
                    //
                    j++;
                }
                else
                {
                    sb.Append("leave leaf node");
                    for (int p = 0; p < newIns.Count; p++)
                    {
                        for ((short l, int k) = (tree.depth, m); l >= 0; l--)
                        {
                            sb.Append($"node:[{l},{k}]");
                            if (tree[l, k].Data.lowers.Count > 0)
                            {
                                newIns[p].overlapSets[i].AddRange(tree[l, k].Data.lowers.Select(b => b.range));
                                sb.Append($"add Overlap {tree[l, k].Data.lowers.Count} Lower,");
                            }
                            sb.Append($"\n");
                            if ((k + 1) % 2 == 0) k = k / 2;
                            else break;
                        }
                    }
                    m++;
                }
                //Debug.Log(sb);
                sb.Clear();
            }
            sortedBounds.ForEach(b => b.range.modified[b.dimId] = false);
        }
        private void DynamicMatching()
        {
            PDebug.Log("DynamicMatching");
            for (int i = 0; i < dimension; i++)
            {
                upTree[i].PreOrderEnumerator(upTree[i].Root).ForEach(node =>
                {
                    if (node.depth == upTree[i].depth && node.Data.insides.Count > 0)
                    {
                        Matching(node.Data.insides.Where(b => b.range.modified[i]).ToList(), subTree[i], i);
                    }
                    List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.modified[i]).ToList();
                    var upper1 = upTree[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                    var upper2 = upTree[i][node.depth, node.index + 2].Data.uppers.Where(n =>
                      n.range.modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                    crossNodeRanges.AddRange(upper1);
                    crossNodeRanges.AddRange(upper2);
                    if (crossNodeRanges.Count > 0)
                        Matching(crossNodeRanges, subTree[i], i);
                });

                subTree[i].PreOrderEnumerator(subTree[i].Root).ForEach(node =>
                {
                    if (node.depth == subTree[i].depth && node.Data.insides.Count > 0)
                    {
                        Matching(node.Data.insides.Where(b => b.range.modified[i]).ToList(), upTree[i], i);
                    }
                    List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.modified[i]).ToList();
                    var upper1 = subTree[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                    var upper2 = subTree[i][node.depth, node.index + 2].Data.uppers.Where(n =>
                      n.range.modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                    crossNodeRanges.AddRange(upper1);
                    crossNodeRanges.AddRange(upper2);
                    if (crossNodeRanges.Count > 0)
                        Matching(crossNodeRanges, upTree[i], i);
                });
            }
        }
        public static void MappingRanges(IEnumerable<Range> ranges, BinaryTree<HSPDIMRanges>[] tree, List<Bound>[] bounds = null)
        {
            PDebug.Log("MappingRanges");
            foreach (Range r in ranges)
            {
                for (short i = 0; i < dimension; i++)
                {
                    if (r == null)
                    {
                        Debug.Log("Null range");
                    }
                    Bound lowerBound = new(i, -1, r);
                    Bound upperBound = new(i, 1, r);
                    Bound coverBound = new(i, 0, r);
                    r.Bounds[i, 0] = lowerBound;
                    r.Bounds[i, 1] = upperBound;
                    r.Bounds[i, 2] = coverBound;
                    if (bounds != null)
                    {
                        bounds[i].Add(lowerBound);
                        bounds[i].Add(upperBound);
                    }
                    if (upperBound.index - lowerBound.index == 0 && r.depthLevel[i] == tree[i].depth)
                    {
                        //Debug.Log($"Add {lowerBound} to tree[{i}][{curDepth}, {lrid}]");
                        tree[i][r.depthLevel[i], lowerBound.index].Data.insides.Add(lowerBound);
                        tree[i][r.depthLevel[i], upperBound.index].Data.insides.Add(upperBound);
                    }
                    else
                    {
                        //Debug.Log($"Add {lowerBound} to tree[{i}][{curDepth}, {lrid}]");
                        tree[i][r.depthLevel[i], lowerBound.index].Data.lowers.Add(lowerBound);
                        tree[i][r.depthLevel[i], upperBound.index].Data.uppers.Add(upperBound);
                        if (upperBound.index - lowerBound.index == 2)
                        {
                            tree[i][r.depthLevel[i], lowerBound.index + 1].Data.covers.Add(coverBound);
                        }
                    }
                }
            }
            bounds?.ForEach(bs => bs.Sort());
            for (short i = 0; i < dimension; i++)
            {
                Debug.Log(tree[i].depth);
                tree[i].PreOrderEnumerator(tree[i].Root).ForEach(node =>
                {
                    if (node.Data.uppers.Count > 0) node.Data.uppers.Sort();
                    if (node.Data.lowers.Count > 0) node.Data.lowers.Sort();
                    //if (node.Data.covers.Count > 0) node.Data.covers.Sort();
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
                RemoveRangeFromTree(r, tree);
            }
            foreach (Range r in ranges)
            {
                r.UpdateRange(tree[0].depth);
                sb.Append($"new bound {r} \n");
                AddRangeToTree(r, tree);
            }
            PDebug.Log(sb.ToString());
        }
        public static void AddBoundToTree(Bound bound, BinaryTree<HSPDIMRanges> tree, bool inside)
        {
            HSPDIMRanges node = tree[bound.range.depthLevel[bound.dimId], bound.index].Data;
            List<Bound> container = null;
            int index = 0;
            int count = -1;
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
                if (!IsSorted(container))
                {
                    PDebug.Log($"add {bound.range} an {bound} is {(inside ? "" : "not")} inside of {bound.dimId}_[{bound.range.depthLevel[bound.dimId]},{bound.index}] at {index} in list count {count} [{string.Join(", ", container.Select(c => $"{c}_{c.range.GetHashCode()}"))}]");
                    Time.timeScale = 0;
                }
            }
        }
        public static void RemoveBoundFromTree(Bound bound, BinaryTree<HSPDIMRanges> tree, bool inside)
        {
            HSPDIMRanges node = tree[bound.range.depthLevel[bound.dimId], bound.index].Data;
            List<Bound> container = null;
            int index = 0;
            int count = -1;
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
                    PDebug.Log($"remove {bound.range} an {bound} is {(inside ? "" : "not")} inside of {bound.dimId}_[{bound.range.depthLevel[bound.dimId]},{bound.index}] at {index} in list count {count} [{string.Join(", ", container.Select(c => $"{c}_{c.range.GetHashCode()}"))}]");
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
                    if (range.modified == Vector3Bool.@false) Debug.LogWarning($"{range} not modified??");
                    if (range.modified[i])
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
        public static void AddRangeToTree(Range range, BinaryTree<HSPDIMRanges>[] tree)
        {
            for (short i = 0; i < dimension; i++)
            {
                if (range.modified == Vector3Bool.@false) Debug.LogWarning($"{range} not modified??");
                if (range.modified[i])
                {
                    Bound lowerBound = range.Bounds[i, 0];
                    Bound upperBound = range.Bounds[i, 1];
                    Bound coverBound = range.Bounds[i, 2];
                    lowerBound = range.Bounds[i, 0] ?? new Bound(i, -1, range);
                    upperBound = range.Bounds[i, 1] ?? new Bound(i, 1, range);
                    coverBound = range.Bounds[i, 2] ?? new Bound(i, 0, range);

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
    }
}