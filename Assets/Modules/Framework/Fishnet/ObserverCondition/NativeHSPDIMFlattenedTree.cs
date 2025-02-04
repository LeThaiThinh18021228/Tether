using Framework.ADS;
using HSPDIMAlgo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Unity.Collections;
namespace Framework.HSPDIMAlgo
{
    public struct NativeHSPDIMFlattenedTree : System.IDisposable
    {
        public NativeArray<short> depth;
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
            depth.Dispose();
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

        public override string ToString()
        {
            string s = "";
            s += $"l:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = LowerDimensions[i].Start; j < LowerDimensions[i].Count + LowerDimensions[i].Start; j++)
                {
                    if (LowerNodes[j].Count > 0)
                        s += $"node{j}[";
                    for (int k = LowerNodes[j].Start; k < LowerNodes[j].Start + LowerNodes[j].Count; k++)
                    {
                        s += $"{Lowers[k]}, ";
                    }
                    if (LowerNodes[j].Count > 0)
                        s += $"]\n";
                }
            }
            s += $"u:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = UpperDimensions[i].Start; j < UpperDimensions[i].Count + UpperDimensions[i].Start; j++)
                {
                    if (UpperNodes[j].Count > 0)
                        s += $"node{j}[";
                    for (int k = UpperNodes[j].Start; k < UpperNodes[j].Start + UpperNodes[j].Count; k++)
                    {
                        s += $"{Uppers[k]}, ";
                    }
                    if (UpperNodes[j].Count > 0)
                        s += $"]\n";
                }
            }
            s += $"in:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = InsideDimensions[i].Start; j < InsideDimensions[i].Count + InsideDimensions[i].Start; j++)
                {
                    if (InsideNodes[j].Count > 0)
                        s += $"node{j}[";
                    for (int k = InsideNodes[j].Start; k < InsideNodes[j].Start + InsideNodes[j].Count; k++)
                    {
                        s += $"{Insides[k]}, ";
                    }
                    if (InsideNodes[j].Count > 0)
                        s += $"]\n";
                }
            }
            s += $"co:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = CoverDimensions[i].Start; j < CoverDimensions[i].Count + CoverDimensions[i].Start; j++)
                {
                    if (CoverNodes[j].Count > 0)
                        s += $"node{j}[";
                    for (int k = CoverNodes[j].Start; k < CoverNodes[j].Start + CoverNodes[j].Count; k++)
                    {
                        s += $"{Covers[k]}, ";
                    }
                    if (CoverNodes[j].Count > 0)
                        s += $"]\n";
                }
            }
            return s;
        }
    }
    public struct NativeHSPDIMFlattenedSortListTree : System.IDisposable
    {
        public NativeArray<NativeBound> Bounds;
        public NativeArray<NativeListElement> ElementList;
        public NativeArray<NativeListElement> ElementDimensions;

        public void Dispose()
        {
            if (Bounds.IsCreated) Bounds.Dispose();
            if (ElementList.IsCreated) ElementList.Dispose();
            if (ElementDimensions.IsCreated) ElementDimensions.Dispose();
        }
        public override string ToString()
        {
            string s = "";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = ElementDimensions[i].Start; j < ElementDimensions[i].Count + ElementDimensions[i].Start; j++)
                {
                    if (ElementList[j].Count > 0)
                    {
                        s += $"node{j}[";
                        if (j % 2 == 0)
                        {
                            s += "cr:";
                        }
                        else
                        {
                            s += "in:";
                        }
                    }
                    for (int k = ElementList[j].Start; k < ElementList[j].Start + ElementList[j].Count; k++)
                    {
                        s += $"{Bounds[k]}, ";
                    }
                    if (ElementList[j].Count > 0)
                        s += $"]\n";

                }
            }
            return s;
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
        public int LowerIndexContainer;
        public RangeIDInTree(int dim, int depth, int index, int isUpper, bool isInside, int start, int count, int lowerIndexContainer = -1)
        {
            Dim = dim;
            Depth = depth;
            Index = index;
            IsUpper = isUpper;
            IsInside = isInside;
            Start = start;
            Count = count;
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

        public OverlapID(RangeIDInTree rangeIDInTree, RangeIDInList rangeIDInList)
        {
            this.rangeIDInTree = rangeIDInTree;
            this.rangeIDInList = rangeIDInList;
        }

        public IEnumerable<HSPDIMRange> MapRangeToTree(BinaryTree<HSPDIMTreeNodeData> tree)
        {
            HSPDIMTreeNodeData node = tree[rangeIDInTree.Depth, rangeIDInTree.Index].Data;
            List<HSPDIMBound> container = rangeIDInTree.IsInside ? node.insides :
                rangeIDInTree.IsUpper switch
                {
                    -1 => node.lowers,
                    1 => node.uppers,
                    0 => node.covers,
                    _ => node.lowers // Fallback case
                };
            for (int i = rangeIDInTree.Start; i < rangeIDInTree.Start + rangeIDInTree.Count; i++)
            {
                yield return container[i].range;
            }
        }

        public IEnumerable<int> MapRangeToTree(NativeHSPDIMFlattenedTree flattendedTree)
        {
            NativeArray<NativeBound> container = rangeIDInTree.IsInside ? flattendedTree.Insides :
                rangeIDInTree.IsUpper switch
                {
                    -1 => flattendedTree.Lowers,
                    1 => flattendedTree.Uppers,
                    0 => flattendedTree.Covers,
                    _ => flattendedTree.Lowers // Fallback case
                };
            for (int i = rangeIDInTree.Start; i < rangeIDInTree.Start + rangeIDInTree.Count; i++)
            {
                yield return container[i].RangeIdInTree.Start;
            }
        }
    }
}
