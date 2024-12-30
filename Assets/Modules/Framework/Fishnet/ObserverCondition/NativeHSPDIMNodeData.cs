using Framework.ADS;
using HSPDIMAlgo;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Range = HSPDIMAlgo.Range;
namespace Framework
{
    public struct NativeHSPDIMNodeData : System.IDisposable
    {
        public NativeArray<NativeBound> Lowers;
        public NativeArray<NativeBound> Uppers;
        public NativeArray<NativeBound> Covers;
        public NativeArray<NativeBound> Insides;

        public NativeArray<NativeNode> LowerNodes;
        public NativeArray<NativeNode> UpperNodes;
        public NativeArray<NativeNode> CoverNodes;
        public NativeArray<NativeNode> InsideNodes;


        public void Dispose()
        {
            if (Lowers.IsCreated) Lowers.Dispose();
            if (LowerNodes.IsCreated) LowerNodes.Dispose();
            if (Uppers.IsCreated) Uppers.Dispose();
            if (UpperNodes.IsCreated) UpperNodes.Dispose();
            if (Covers.IsCreated) Covers.Dispose();
            if (CoverNodes.IsCreated) CoverNodes.Dispose();
            if (Insides.IsCreated) Insides.Dispose();
            if (InsideNodes.IsCreated) InsideNodes.Dispose();
        }


    }
    public struct NativeBound : IComparable<NativeBound>
    {
        public float BoundValue;
        public int LowerIndex;
        public int RangeIdInList;
        public RangeID RangeIdInTree;

        public NativeBound(float boundValue, int lowerIndex, int rangeIdInList, RangeID rangeIdInTree)
        {
            BoundValue = boundValue;
            LowerIndex = lowerIndex;
            RangeIdInList = rangeIdInList;
            RangeIdInTree = rangeIdInTree;
        }

        public int CompareTo(NativeBound other)
        {
            if (BoundValue > other.BoundValue) return 1;
            if (BoundValue < other.BoundValue) return -1;
            return 0;
        }
    }
    public struct NativeNode
    {
        public short Depth;
        public int Index;
        public int Start;
        public int Count;

        public NativeNode(short depth, int index, int start, int count)
        {
            Depth = depth;
            Index = index;
            Start = start;
            Count = count;
        }
    }

    public struct RangeID
    {
        public int Dim;
        public int Depth;
        public int Index;
        public int IsUpper;
        public bool IsInside;
        public int Start;
        public int Count;
        public int IndexContainer;
        public RangeID(int dim, int depth, int index, int isUpper, bool isInside, int start, int count, int lowerId = -1)
        {
            Dim = dim;
            Depth = depth;
            Index = index;
            IsUpper = isUpper;
            IsInside = isInside;
            Start = start;
            Count = count;
            IndexContainer = lowerId;
        }

        public override string ToString()
        {
            return $"RangeID [{Dim},{Depth},{Index}] IsUpper = {IsUpper}, IsInside = {IsInside}, Start = {Start}, Count = {Count} LowerIndex = {IndexContainer}";
        }
    }

    public struct OverlapID
    {
        public RangeID rangeIDInTree;
        public int rangeIDInList;
        public int hint;

        public OverlapID(RangeID rangeIDInTree, int rangeIDInList, int hint)
        {
            this.rangeIDInTree = rangeIDInTree;
            this.rangeIDInList = rangeIDInList;
            this.hint = hint;
        }

        public IEnumerable<Range> MapRangeToTree(BinaryTree<HSPDIMNodeData> tree, List<Bound> sortbounds)
        {
            HSPDIMNodeData node = tree[rangeIDInTree.Depth, rangeIDInTree.Index].Data;
            List<Bound> container = node.lowers;
            IEnumerable<Range> range;
            if (rangeIDInTree.IsInside)
            {
                container = node.insides;
            }
            else
                switch (rangeIDInTree.IsUpper)
                {
                    case -1:
                        container = node.lowers;
                        break;
                    case 1:
                        container = node.uppers;
                        break;
                    case 0:
                        container = node.covers;
                        break;
                    default:
                        break;
                }
            //PDebug.Log($"{hint} _ {rangeIDInTree} _ rangeIDInList {sortbounds[rangeIDInList].range}  _ container {container.Count}");
            range = container.GetRange(rangeIDInTree.Start, rangeIDInTree.Count).Select(r => r.range);
            return range;
        }
    }
}
