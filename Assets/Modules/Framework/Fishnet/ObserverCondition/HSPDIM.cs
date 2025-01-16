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
        public HashSet<Range> modifiedUpRanges = new();
        public HashSet<Range> modifiedSubRanges = new();
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
                var uplist = modifiedUpRanges.ToList();
                var sublist = modifiedSubRanges.ToList();
                MappingRangeDynamic(uplist, upTree);
                MappingRangeDynamic(sublist, subTree);
                DynamicMatching();
                uplist.ForEach(r => r.entity.Modified = Vector3Bool.@false);
                sublist.ForEach(r => r.entity.Modified = Vector3Bool.@false);
                uplist.Clear();
                sublist.Clear();
                modifiedUpRanges.Clear();
                modifiedSubRanges.Clear();
                LogTree(upTree);
                LogTree(subTree);
            }
        }
        public void InitMappingAndMatching(GameState prev, GameState next, bool asServer)
        {
            if (!asServer) return;
            if (next == GameState.STARTED)
            {
                MappingRanges(modifiedUpRanges.ToList(), upTree);
                List<Bound>[] sortedBounds = Enumerable.Range(0, dimension).Select(_ => new List<Bound>(subTreeDepth)).ToArray();
                MappingRanges(modifiedSubRanges.ToList(), subTree, sortedBounds);
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
                modifiedUpRanges.ForEach(r => r.entity.Modified = Vector3Bool.@false);
                modifiedSubRanges.ForEach(r => r.entity.Modified = Vector3Bool.@false);
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
            //Debug.Log(sb);
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
                            IEnumerable<Range> overlapRange;
                            if (node.Data.uppers.Count - indexNode.Data.y > 0)
                            {
                                overlapRange = node.Data.uppers.GetRange(indexNode.Data.y, node.Data.uppers.Count - indexNode.Data.y).Select(b => b.range);
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add Overlap Upper from{indexNode.Data.y} to {node.Data.uppers.Count}:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))};\n ");
                            }

                            if (node.Data.covers.Count > 0)
                            {
                                overlapRange = node.Data.covers.Select(b => b.range).ToList();
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add {node.Data.covers.Count} Overlap Cover:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}; \n");
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
                                IEnumerable<Range> overlapRange = node.Data.lowers.GetRange(0, indexNode.Data.x).Select(b => b.range);
                                boundInSortedList.range.overlapSets[i].AddRange(overlapRange);
                                if (!wise)
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(boundInSortedList.range));
                                sb.Append($"add Overlap Lower from {0} to {indexNode.Data.x}:\n{string.Join(",", overlapRange.Select(r => r.ToString()))}; \n");
                            }
                            if (l == tree.depth)
                            {
                                newIns.Remove(boundInSortedList.range);
                                sb.Append($"remove {boundInSortedList.range} from newIns; \n");
                            }
                        }
                        sb.Append($"\n");
                    }
                    SortMatchInside(boundInSortedList, tree, indexTree, i, m, subset, upset, true, sb, wise);
                    j++;
                    m2 = m;
                }
                else
                {
                    sb.Append("leave leaf\n");
                    if (newIns.Count > 0)
                        sb.Append($"add Overlap Lower to\n{string.Join("\n", newIns.Select(r => r.ToString()))} \n");
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
                            sb.Append($"\t overlap {node.Data.lowers.Count} lower:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}");
                        }
                        sb.Append($"\n");
                        if (l == tree.depth)
                        {
                            IEnumerable<Range> overlapRange = node.Data.insides.Select(b => b.range);
                            newIns.ForEach(b =>
                            {
                                if (m > IndexCal(b.Bounds[i, 0].boundValue, tree.depth))
                                {
                                    b.overlapSets[i].AddRange(overlapRange);
                                    overlapRange.ForEach(r => r.overlapSets[i].Add(b));
                                    sb.Append($"\t overlap {b} inside:\n{string.Join("\n", overlapRange.Select(r => r.ToString()))}");
                                }
                                else
                                {
                                    //SortMatchInside(b.Bounds[i, 0], tree, indexTree, i, m, subset, upset, sb);
                                }
                            });
                            if (m == m2)
                            {
                                SortMatchInside(boundInSortedList, tree, indexTree, i, m, subset, upset, false, sb, wise);
                            }
                        }
                        if ((k + 1) % 2 == 0) k = k / 2;
                        else break;
                    }
                    m++;
                }
                //Debug.Log(sb);
                sb.Clear();
            }
        }
        private void SortMatchInside(Bound boundInSortedList, BinaryTree<HSPDIMNodeData> tree, BinaryTree<Vector3Int> indexTree, int i, int m, List<Range> subset, List<Range> upset, bool headEnd, StringBuilder sb, bool wise = true)
        {
            sb.Append($"matching inside range at leaf {m} ");
            TreeNode<HSPDIMNodeData> node = tree[tree.depth, m];
            Bound boundInTree;
            sb.Append($"insideIt: {indexTree[tree.depth, m].Data.z}\n");
            if (node.Data.insides.Count > 0 && indexTree[tree.depth, m].Data.z < node.Data.insides.Count)
            {
                boundInTree = node.Data.insides[indexTree[tree.depth, m].Data.z];
                while (boundInTree.boundValue <= boundInSortedList.boundValue)
                {
                    sb.Append($"subset before:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
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
                            sb.Append($"matching inside range {boundInTree.range} :\n{string.Join("\n", upset.Select(r => r.ToString()))} \n");
                        }
                    }
                    sb.Append($"subset after:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
                    Vector3Int temp = indexTree[tree.depth, m].Data;
                    temp.z++;
                    indexTree[tree.depth, m].Data = temp;
                    if (indexTree[tree.depth, m].Data.z < node.Data.insides.Count)
                    {
                        boundInTree = node.Data.insides[indexTree[tree.depth, m].Data.z];
                        sb.Append($"insideIt: {indexTree[tree.depth, m].Data.z}\n");
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
                    sb.Append($"add {subset.Count} Overlap inside {boundInSortedList}:\n{string.Join("\n", subset.Select(r => r.ToString()))} \n");
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
            subRanges.ForEach(r => { if (r.entity.IsServerInitialized) { r.UpdateIntersection(); } });
        }
        private void MatchingTreeToTree(BinaryTree<HSPDIMNodeData>[] tree1, BinaryTree<HSPDIMNodeData>[] tree2, bool wise = true)
        {
            NativeHSPDIMNodeData flattenedTree = new()
            {
                LowerDimensions = new NativeArray<NativeListElement>(dimension, Allocator.TempJob),
                UpperDimensions = new NativeArray<NativeListElement>(dimension, Allocator.TempJob),
                CoverDimensions = new NativeArray<NativeListElement>(dimension, Allocator.TempJob),
                InsideDimensions = new NativeArray<NativeListElement>(dimension, Allocator.TempJob)
            };
            List<NativeNode> lowerNodes = new();
            List<NativeNode> upperNodes = new();
            List<NativeNode> coverNodes = new();
            List<NativeNode> insideNodes = new();
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

            NativeHSPDIMListBound nativeHSPDIMListBound = new();
            nativeHSPDIMListBound.ElementDimensions = new NativeArray<NativeListElement>(dimension, Allocator.TempJob);
            List<NativeListElement> sortedListElements = new();
            List<NativeBound> listSortedNativeBounds = new();
            List<Bound> listSortedBounds = new();
            int startSortedBound = 0;
            int startSortedBoundDimension = 0;
            StringBuilder stringBuilder = new StringBuilder();

            for (int i = 0; i < dimension; i++)
            {
                int totalNode = (int)Mathf.Pow(2, tree2[i].depth + 1) - 1;
                tree2[i].ForEach(node =>
                {
                    int index = (1 << node.depth) + node.index - 1 + (int)Mathf.Pow(2, tree2[i].depth + i) - i;
                    //if (node.Data.lowers.Count > 0)
                    {
                        lowerNodes.Add(new NativeNode(node.depth, node.index, startLower, node.Data.lowers.Count));
                        startLower += node.Data.lowers.Count;
                        lowerBounds.AddRange(node.Data.lowers.Select((b, j) => b.ToNativeBound(j, false, node.index, j)));
                    }
                    //if (node.Data.uppers.Count > 0)
                    {
                        upperNodes.Add(new NativeNode(node.depth, node.index, startUpper, node.Data.uppers.Count));
                        startUpper += node.Data.uppers.Count;
                        upperBounds.AddRange(node.Data.uppers.Select((b, j) => b.ToNativeBound(j, false, b.range.Bounds[i, 0].index,
                            tree2[i][node.depth, b.range.Bounds[i, 0].index].Data.lowers.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                    //if (node.Data.covers.Count > 0)
                    {
                        coverNodes.Add(new NativeNode(node.depth, node.index, startCover, node.Data.covers.Count));
                        startCover += node.Data.covers.Count;
                        coverBounds.AddRange(node.Data.covers.Select((b, j) => b.ToNativeBound(j, false, b.range.Bounds[i, 0].index, 0)));
                    }
                    //if (node.Data.insides.Count > 0)
                    {
                        insideNodes.Add(new NativeNode(node.depth, node.index, startInside, node.Data.insides.Count));
                        startInside += node.Data.insides.Count;
                        insideBounds.AddRange(node.Data.insides.Select((b, j) => b.ToNativeBound(j, true, node.index, node.Data.insides.BinarySearch(b.range.Bounds[i, 0]))));
                    }
                });
                flattenedTree.LowerDimensions[i] = new NativeListElement(startLowerDimension, totalNode);
                startLowerDimension += totalNode;
                flattenedTree.UpperDimensions[i] = new NativeListElement(startUpperDimension, totalNode);
                startUpperDimension += totalNode;
                flattenedTree.CoverDimensions[i] = new NativeListElement(startCoverDimension, totalNode);
                startCoverDimension += totalNode;
                flattenedTree.InsideDimensions[i] = new NativeListElement(startInsideDimension, totalNode);
                startInsideDimension += totalNode;
                totalNode = 0;
                tree1[i].PreOrderEnumerator(tree1[i].Root).ForEach(node =>
                {
                    if (node.Data.lowers.Count > 0 || node.Data.insides.Count > 0)
                    {
                        List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.entity.Modified[i]).ToList();
                        if (crossNodeRanges.Count > 0)
                        {
                            var upper1 = tree1[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                            var upper2 = tree1[i][node.depth, node.index + 2].Data.uppers.Where(n =>
                              n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
                            crossNodeRanges.AddRange(upper1);
                            crossNodeRanges.AddRange(upper2);

                        }
                        List<Bound> insideRange = node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList();
                        if (crossNodeRanges.Count > 0 || insideRange.Count > 0)
                        {
                            sortedListElements.Add(new NativeListElement(startSortedBound, crossNodeRanges.Count));
                            listSortedNativeBounds.AddRange(crossNodeRanges.Select((b, j) =>
                            b.ToNativeBound(j, startSortedBoundDimension + totalNode, crossNodeRanges.BinarySearch(b.range.Bounds[i, 0]))));
                            listSortedBounds.AddRange(crossNodeRanges);
                            startSortedBound += crossNodeRanges.Count;
                            totalNode++;

                            sortedListElements.Add(new NativeListElement(startSortedBound, insideRange.Count));
                            listSortedNativeBounds.AddRange(insideRange.Select((b, j) =>
                            b.ToNativeBound(j, startSortedBoundDimension + totalNode, insideRange.BinarySearch(b.range.Bounds[i, 0]))));
                            listSortedBounds.AddRange(insideRange);
                            startSortedBound += insideRange.Count;
                            totalNode++;

                        }
                    }
                });
                nativeHSPDIMListBound.ElementDimensions[i] = new NativeListElement(startSortedBoundDimension, totalNode);
                startSortedBoundDimension += totalNode;

            }
            flattenedTree.Lowers = new NativeArray<NativeBound>(lowerBounds.ToArray(), Allocator.TempJob);
            flattenedTree.Uppers = new NativeArray<NativeBound>(upperBounds.ToArray(), Allocator.TempJob);
            flattenedTree.Covers = new NativeArray<NativeBound>(coverBounds.ToArray(), Allocator.TempJob);
            flattenedTree.Insides = new NativeArray<NativeBound>(insideBounds.ToArray(), Allocator.TempJob);
            flattenedTree.LowerNodes = new NativeArray<NativeNode>(lowerNodes.ToArray(), Allocator.TempJob);
            flattenedTree.UpperNodes = new NativeArray<NativeNode>(upperNodes.ToArray(), Allocator.TempJob);
            flattenedTree.CoverNodes = new NativeArray<NativeNode>(coverNodes.ToArray(), Allocator.TempJob);
            flattenedTree.InsideNodes = new NativeArray<NativeNode>(insideNodes.ToArray(), Allocator.TempJob);

            nativeHSPDIMListBound.Bounds = new NativeArray<NativeBound>(listSortedNativeBounds.ToArray(), Allocator.TempJob);
            nativeHSPDIMListBound.ElementList = new NativeArray<NativeListElement>(sortedListElements.ToArray(), Allocator.TempJob);

            //stringBuilder.Append($"SortedList:\n");
            //for (int i = 0; i < nativeHSPDIMListBound.ElementList.Length; i++)
            //{
            //    if (nativeHSPDIMListBound.ElementList[i].Count > 0)
            //    {
            //        stringBuilder.Append($"[{i},{nativeHSPDIMListBound.ElementList[i].Start}]: ");
            //        for (int j = nativeHSPDIMListBound.ElementList[i].Start; j < nativeHSPDIMListBound.ElementList[i].Start + nativeHSPDIMListBound.ElementList[i].Count; j++)
            //        {
            //            stringBuilder.Append($"{nativeHSPDIMListBound.Bounds[j]}, ");
            //        }
            //        stringBuilder.Append("\n");
            //    }
            //}
            //PDebug.Log(stringBuilder);
            //stringBuilder.Clear();

            int size = dimension * nativeHSPDIMListBound.Bounds.Length * (flattenedTree.Lowers.Length + flattenedTree.Uppers.Length + flattenedTree.Insides.Length / 2);
            //PDebug.Log("Size Allocation :" + size);
            NativeList<OverlapID> overlapSet = new(size, Allocator.TempJob);
            var logQueue = new NativeQueue<FixedString128Bytes>(Allocator.TempJob);
            var treeDepth = new NativeArray<short>(new short[] { tree2[0].depth, tree2[1].depth }, Allocator.TempJob);
            MathcingRangeToTreeJob job = new()
            {
                ListSortedBound = nativeHSPDIMListBound,
                Tree = flattenedTree,
                OverlapSet = overlapSet.AsParallelWriter(),
                Message = logQueue.AsParallelWriter(),
                TreeDepth = treeDepth
            };
            //for (int i = 0; i < nativeHSPDIMListBound.ElementList.Length; i++)
            //{
            //    job.Execute(i);
            //}
            JobHandle handle = job.Schedule(nativeHSPDIMListBound.ElementList.Length, 1);
            handle.Complete();

            //Debug.Log($"{overlapSet.Length}_{nativeSortedBounds.Length}");
            for (int j = 0; j < overlapSet.Length; j++)
            {
                OverlapID overlap = overlapSet[j];
                int i = overlapSet[j].rangeIDInTree.Dim;
                var overlapRange = overlapSet[j].MapRangeToTree(tree2[i], overlapSet[j].rangeIDInList.Index + overlapSet[j].rangeIDInList.IndexContainer, listSortedBounds);
                int startIndexElement = nativeHSPDIMListBound.ElementList[overlapSet[j].rangeIDInList.Index].Start;
                var boundInList = listSortedBounds[startIndexElement + overlapSet[j].rangeIDInList.IndexContainer];
                boundInList.range.overlapSets[i].AddRange(overlapRange);
                overlapRange.ForEach(r => r.overlapSets[i].Add(boundInList.range));
            }
            Debug.Log($"{string.Join("\n", Enumerable.Range(0, logQueue.Count).Select(_ => logQueue.Dequeue()))}");
            //for (int i = 0; i < dimension; i++)
            //{
            //    tree1[i].PreOrderEnumerator(tree1[i].Root).ForEach(node =>
            //    {
            //        if (node.depth == tree1[i].depth && node.Data.insides.Count > 0)
            //        {
            //            Matching(node.Data.insides.Where(b => b.range.entity.Modified[i]).ToList(), tree2[i], i, wise);
            //        }
            //        List<Bound> crossNodeRanges = node.Data.lowers.Where(b => b.range.entity.Modified[i]).ToList();
            //        if (crossNodeRanges.Count > 0)
            //        {
            //            var upper1 = tree1[i][node.depth, node.index + 1].Data.uppers.Where(n => n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
            //            var upper2 = tree1[i][node.depth, node.index + 2].Data.uppers.Where(n =>
            //              n.range.entity.Modified[i] && crossNodeRanges.Any(x => x.range == n.range));
            //            crossNodeRanges.AddRange(upper1);
            //            crossNodeRanges.AddRange(upper2);
            //            Matching(crossNodeRanges, tree2[i], i, wise);
            //        }
            //    });
            //}
            treeDepth.Dispose();
            logQueue.Dispose();
            flattenedTree.Dispose();
            overlapSet.Dispose();
            nativeHSPDIMListBound.Dispose();
        }
        public static void MappingRanges(List<Range> ranges, BinaryTree<HSPDIMNodeData>[] tree, List<Bound>[] bounds = null)
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
        public void MappingRangeDynamic(List<Range> ranges, BinaryTree<HSPDIMNodeData>[] tree)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"MappingRangesDynamic {ranges.Count()} range\n");
            foreach (Range r in ranges)
            {
                sb.Append($"old bound {r} \n ");
                int indexBefore = r.Bounds[0, 0].index;
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
                sb.Append($"new bound {r} \n");
            }
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
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
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
            if (range.Bounds[i, 0].index >= 0 && range.Bounds[i, 1].index >= 0)
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
            else
            {
                //PDebug.LogWarning("remove range");
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
            [ReadOnly] public NativeHSPDIMListBound ListSortedBound;
            [ReadOnly] public NativeHSPDIMNodeData Tree;
            public NativeList<OverlapID>.ParallelWriter OverlapSet;
            public NativeQueue<FixedString128Bytes>.ParallelWriter Message;
            [ReadOnly] public NativeArray<short> TreeDepth;

            public void Execute(int index)
            {
                NativeListElement element = ListSortedBound.ElementList[index];
                if (element.Count == 0)
                {
                    return;
                }
                int d = -1;
                int p = index;
                while (p >= 0)
                {
                    d++;
                    p -= ListSortedBound.ElementDimensions[d].Count;
                }
                if (d < 0 || d >= dimension) throw new Exception();
                Matching(element.Start, element.Count, index, d, TreeDepth[d]);
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
                int leftLeaf = IndexCal(ListSortedBound.Bounds[startBoundIndex].BoundValue, treeDepth);
                int rightLeaf = IndexCal(ListSortedBound.Bounds[endBoundIndex].BoundValue, treeDepth);
                NativeList<int2> newIns = new(Allocator.Temp);
                NativeList<RangeIDInTree> subset = new(Allocator.Temp);
                NativeList<int2> upset = new(Allocator.Temp);
                int j = startBoundIndex;
                int m2 = leftLeaf, m = leftLeaf;
                int i = DimensionIndex;
                int startNodeDimesion = Tree.UpperDimensions[DimensionIndex].Start;

                NativeBound boundInSortedList = ListSortedBound.Bounds[j];
                NativeBound boundInTree = new();
                while (j <= endBoundIndex && m <= rightLeaf)
                {
                    boundInSortedList = ListSortedBound.Bounds[j];
                    NativeNode node = new();
                    int nodeIndex;
                    int nodeIndexInTree;
                    if (IndexCal(boundInSortedList.BoundValue, treeDepth) == m)
                    {
                        for ((short l, int k) = (treeDepth, m); l >= 0; l--, k = k / 2)
                        {
                            nodeIndexInTree = (int)Mathf.Pow(2, l) + k - 1;
                            nodeIndex = nodeIndexInTree + startNodeDimesion;
                            if (boundInSortedList.RangeIdInTree.IsUpper == -1)
                            {
                                node = Tree.UpperNodes[nodeIndex];
                                if (node.Count > 0 && indexTree[nodeIndexInTree].y < node.Count)
                                {
                                    boundInTree = Tree.Uppers[indexTree[nodeIndexInTree].y + node.Start];
                                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                                    {
                                        int3 indexNode = indexTree[nodeIndexInTree];
                                        indexNode.y++;
                                        indexTree[nodeIndexInTree] = indexNode;
                                        if (indexTree[nodeIndexInTree].y < node.Count)
                                        {
                                            boundInTree = Tree.Uppers[indexTree[nodeIndexInTree].y + node.Start];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (node.Count - indexTree[nodeIndexInTree].y > 0 && node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, 1, false, indexTree[nodeIndexInTree].y, node.Count - indexTree[nodeIndexInTree].y), boundInSortedList.RangeIdInList, 0));
                                }
                                node = Tree.CoverNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, 0, false, 0, node.Count), boundInSortedList.RangeIdInList, 1));
                                }

                                if (l == treeDepth)
                                {
                                    newIns.Add(new(boundInSortedList.RangeIdInList.IndexContainer, boundInSortedList.RangeIdInList.LowerIndexContainer));
                                }
                            }
                            else if (boundInSortedList.RangeIdInTree.IsUpper == 1)
                            {
                                node = Tree.LowerNodes[nodeIndex];
                                if (node.Count > 0 && indexTree[nodeIndexInTree].x < node.Count)
                                {
                                    boundInTree = Tree.Lowers[indexTree[nodeIndexInTree].x + node.Start];
                                    while (boundInTree.BoundValue < boundInSortedList.BoundValue)
                                    {
                                        int3 indexNode = indexTree[nodeIndexInTree];
                                        indexNode.x++;
                                        indexTree[nodeIndexInTree] = indexNode;
                                        if (indexTree[nodeIndexInTree].x < node.Count)
                                        {
                                            boundInTree = Tree.Lowers[indexTree[nodeIndexInTree].x + node.Start];
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (indexTree[nodeIndexInTree].x > 0)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, -1, false, 0, indexTree[nodeIndexInTree].x), boundInSortedList.RangeIdInList, 2));
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
                        SortMatchInside(boundInSortedList, indexTree, DimensionIndex, indexSortedListElemenet, m, subset, upset, treeDepth, true, 10);
                        j++;
                        m2 = m;
                    }
                    else
                    {
                        for ((short l, int k) = (treeDepth, m); l >= 0; l--)
                        {
                            nodeIndexInTree = (int)Mathf.Pow(2, l) + k - 1;
                            nodeIndex = nodeIndexInTree + startNodeDimesion;
                            node = Tree.LowerNodes[nodeIndex];
                            if (node.Count > 0)
                            {
                                for (int q = 0; q < newIns.Length; q++)
                                {
                                    OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, -1, false, 0, node.Count), new(i, indexSortedListElemenet, newIns[q].x, newIns[q].y), 3));
                                }
                            }
                            if (l == treeDepth)
                            {
                                node = Tree.InsideNodes[nodeIndex];
                                if (node.Count > 0)
                                {
                                    for (int q = 0; q < newIns.Length; q++)
                                    {
                                        if (m > IndexCal(ListSortedBound.Bounds[newIns[q].y + startBoundIndex].BoundValue, treeDepth))
                                        {
                                            OverlapSet.AddNoResize(new OverlapID(new RangeIDInTree(i, l, k, boundInTree.RangeIdInTree.IsUpper, true, 0, node.Count), new(i, indexSortedListElemenet, newIns[q].x, newIns[q].y), 4));
                                        }
                                    }
                                }
                                if (m == m2)
                                {
                                    SortMatchInside(boundInSortedList, indexTree, DimensionIndex, indexSortedListElemenet, m, subset, upset, treeDepth, false, 15);
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
            private void SortMatchInside(NativeBound boundInSortedList, NativeArray<int3> indexTree, int DimensionIndex, int indexSortedListElemenet, int m, NativeList<RangeIDInTree> subset, NativeList<int2> upset, short treeDepth, bool headEnd, int hint)
            {

                NativeBound boundInTree;
                int nodeIndexInTree = (int)Mathf.Pow(2, treeDepth) + m - 1;
                int nodeIndex = nodeIndexInTree + Tree.UpperDimensions[DimensionIndex].Start;
                NativeNode node = Tree.InsideNodes[nodeIndex];
                Message.Enqueue($"\nSortMatchInside {boundInSortedList.BoundValue} HeadEnd_{headEnd} [{DimensionIndex},{treeDepth},{m}]  node.Count={node.Count}");
                Message.Enqueue($"SubsetCount={subset.Length} UpsetCount={upset.Length}");
                Message.Enqueue($"Index {indexTree[nodeIndexInTree].z}\n");
                if (node.Count > 0 && indexTree[nodeIndexInTree].z < node.Count)
                {
                    boundInTree = Tree.Insides[indexTree[nodeIndexInTree].z + node.Start];
                    while (boundInTree.BoundValue <= boundInSortedList.BoundValue)
                    {
                        Message.Enqueue($"Index {indexTree[nodeIndexInTree].z}\n");
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
                                Message.Enqueue($"Overlap {indexTree[nodeIndexInTree].z} & {upset[q].x}\n");
                                OverlapSet.AddNoResize(new OverlapID(boundInTree.RangeIdInTree, new(DimensionIndex, indexSortedListElemenet, upset[q].x, upset[q].y), 5 + hint + ((indexTree[nodeIndexInTree].z == boundInTree.RangeIdInTree.LowerIndexContainer) ? 2 : 3) * 10));
                            }
                        }
                        int3 indexNode = indexTree[nodeIndexInTree];
                        indexNode.z++;
                        indexTree[nodeIndexInTree] = indexNode;
                        if (indexTree[nodeIndexInTree].z < node.Count)
                        {
                            boundInTree = Tree.Insides[indexTree[nodeIndexInTree].z + node.Start];
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                Message.Enqueue($"SubsetCount={subset.Length} UpsetCount={upset.Length}");
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
                        Message.Enqueue($"Overlap {subset[q].Start} & {boundInSortedList.RangeIdInList.IndexContainer}\n");
                        OverlapSet.AddNoResize(new OverlapID(subset[q], boundInSortedList.RangeIdInList, 6 + hint));
                    }
                }
                Message.Enqueue($"SubsetCount={subset.Length} UpsetCount={upset.Length}");
            }
        }
    }
}