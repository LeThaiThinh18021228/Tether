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

        public NativeArray<NativeListElement> LowerDimensions;
        public NativeArray<NativeListElement> UpperDimensions;
        public NativeArray<NativeListElement> CoverDimensions;
        public NativeArray<NativeListElement> InsideDimensions;
        public void Dispose()
        {
            if (Lowers.IsCreated) Lowers.Dispose();
            if (LowerNodes.IsCreated) LowerNodes.Dispose();
            if (LowerDimensions.IsCreated) LowerDimensions.Dispose();
            if (Uppers.IsCreated) Uppers.Dispose();
            if (UpperNodes.IsCreated) UpperNodes.Dispose();
            if (UpperDimensions.IsCreated) UpperDimensions.Dispose();
            if (Covers.IsCreated) Covers.Dispose();
            if (CoverNodes.IsCreated) CoverNodes.Dispose();
            if (CoverDimensions.IsCreated) CoverDimensions.Dispose();
            if (Insides.IsCreated) Insides.Dispose();
            if (InsideNodes.IsCreated) InsideNodes.Dispose();
            if (InsideDimensions.IsCreated) InsideDimensions.Dispose();
        }
    }
    public struct NativeHSPDIMListBound : System.IDisposable
    {
        public NativeArray<NativeBound> Bounds;
        public NativeArray<NativeListElement> ElementList;
        public NativeArray<NativeListElement> ElementDimensions;

        public void Dispose()
        {
            if (Bounds.IsCreated) Bounds.Dispose();
            if (ElementList.IsCreated) ElementList.Dispose();
        }
    }
    public struct NativeBound : IComparable<NativeBound>
    {
        public float BoundValue;
        public RangeIDInList RangeIdInList;
        public RangeIDInTree RangeIdInTree;

        public NativeBound(float boundValue, RangeIDInList rangeIDInList = default, RangeIDInTree rangeIdInTree = default)
        {
            BoundValue = boundValue;
            RangeIdInList = rangeIDInList;
            RangeIdInTree = rangeIdInTree;
        }

        public int CompareTo(NativeBound other)
        {
            if (BoundValue > other.BoundValue) return 1;
            if (BoundValue < other.BoundValue) return -1;
            return 0;
        }
        public override string ToString()
        {
            return BoundValue.ToString();
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
    public struct NativeListElement
    {
        public int Start;
        public int Count;

        public NativeListElement(int start, int count)
        {
            Start = start;
            Count = count;
        }
    }
    public struct RangeIDInTree
    {
        public int Dim;
        public int Depth;
        public int Index;
        public int IsUpper;
        public bool IsInside;
        public int Start;
        public int Count;
        public int LowerIndex;
        public int LowerIndexContainer;
        public RangeIDInTree(int dim, int depth, int index, int isUpper, bool isInside, int start, int count, int lowerIndexContainer = -1, int lowerIndex = -1)
        {
            Dim = dim;
            Depth = depth;
            Index = index;
            IsUpper = isUpper;
            IsInside = isInside;
            Start = start;
            Count = count;
            LowerIndex = lowerIndex;
            LowerIndexContainer = lowerIndexContainer;
        }

        public override string ToString()
        {
            return $"RangeID [{Dim},{Depth},{Index}] IsUpper = {IsUpper}, IsInside = {IsInside}, Start = {Start}, Count = {Count} LowerIndexContainer = {LowerIndexContainer}";
        }
    }
    public struct RangeIDInList
    {
        public int Dim;
        public int Index;
        public int IndexContainer;
        public int LowerIndexContainer;

        public RangeIDInList(int dim, int index, int indexContainer, int lowerIndexContainer)
        {
            Dim = dim;
            Index = index;
            IndexContainer = indexContainer;
            LowerIndexContainer = lowerIndexContainer;
        }

        public override string ToString()
        {
            return $"RangeID [{Dim},{Index}] IndexContainer = {IndexContainer}";
        }
    }
    public struct OverlapID
    {
        public RangeIDInTree rangeIDInTree;
        public RangeIDInList rangeIDInList;
        public int hint;

        public OverlapID(RangeIDInTree rangeIDInTree, RangeIDInList rangeIDInList, int hint)
        {
            this.rangeIDInTree = rangeIDInTree;
            this.rangeIDInList = rangeIDInList;
            this.hint = hint;
        }

        public IEnumerable<Range> MapRangeToTree(BinaryTree<HSPDIMNodeData> tree, int indexInList, List<Bound> sortbounds = null)
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
            PDebug.Log($"{hint} _ {rangeIDInTree} _ rangeIDInList {((0 < indexInList && indexInList < sortbounds.Count) ? sortbounds[indexInList].range : indexInList)}  _ container {container.Count}");
            range = container.GetRange(rangeIDInTree.Start, rangeIDInTree.Count).Select(r => r.range);
            return range;
        }
    }
}
