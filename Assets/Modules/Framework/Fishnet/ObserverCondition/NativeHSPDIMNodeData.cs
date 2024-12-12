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

        public void Dispose()
        {
            if (Lowers.IsCreated) Lowers.Dispose();
            if (Uppers.IsCreated) Uppers.Dispose();
            if (Covers.IsCreated) Covers.Dispose();
            if (Insides.IsCreated) Insides.Dispose();
        }
    }
    public struct NativeBound : IComparable<NativeBound>
    {
        public float BoundValue;
        public short IsUpper;
        public short DimId;
        public int Index;
        public short AlterDim;
        public int RangeIdInList;
        public RangeID RangeIdInTree;

        public NativeBound(short dimId, short alterDim, short isUpper, float boundValue, int index, int rangeIdInList, RangeID rangeIdInTree)
        {
            DimId = dimId;
            IsUpper = isUpper;
            BoundValue = boundValue;
            AlterDim = alterDim;
            Index = index;
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

    public struct RangeID
    {
        public int Dim;
        public int Depth;
        public int Index;
        public int IsUpper;
        public bool IsInside;
        public int Start;
        public int Count;
        public RangeID(int dim, int depth, int index, int isUpper, bool isInside, int start, int count)
        {
            Dim = dim;
            Depth = depth;
            Index = index;
            IsUpper = isUpper;
            IsInside = isInside;
            Start = start;
            Count = count;
        }
    }

    public struct OverlapID
    {
        public RangeID rangeIDInTree;
        public int rangeIDInList;

        public OverlapID(RangeID rangeIDInTree, int rangeIDInList)
        {
            this.rangeIDInTree = rangeIDInTree;
            this.rangeIDInList = rangeIDInList;
        }

        public IEnumerable<Range> MapRangeToTree(BinaryTree<HSPDIMNodeData> tree)
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
            range = container.GetRange(rangeIDInTree.Start, rangeIDInTree.Count).Select(r => r.range);
            return range;
        }
    }
}
