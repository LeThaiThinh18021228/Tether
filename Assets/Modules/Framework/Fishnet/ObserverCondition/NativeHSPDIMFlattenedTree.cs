using Framework.ADS;
using System;
using System.Collections.Generic;
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
            if (Lowers.IsCreated) Lowers.Dispose();
            if (Uppers.IsCreated) Uppers.Dispose();
            if (Covers.IsCreated) Covers.Dispose();
            if (Insides.IsCreated) Insides.Dispose();
        }
        public void DisposePersistent()
        {
            if (depth.IsCreated) depth.Dispose();
            if (LowerNodes.IsCreated) LowerNodes.Dispose();
            if (LowerDimensions.IsCreated) LowerDimensions.Dispose();
            if (UpperNodes.IsCreated) UpperNodes.Dispose();
            if (UpperDimensions.IsCreated) UpperDimensions.Dispose();
            if (CoverNodes.IsCreated) CoverNodes.Dispose();
            if (CoverDimensions.IsCreated) CoverDimensions.Dispose();
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
                    {
                        s += $"node{j}[";
                        for (int k = LowerNodes[j].Start; k < LowerNodes[j].Start + LowerNodes[j].Count; k++)
                        {
                            s += $"{Lowers[k]}_ {Lowers[k].Dim} , ";
                        }
                        s += $"]\n";
                    }

                }
            }
            s += $"u:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = UpperDimensions[i].Start; j < UpperDimensions[i].Count + UpperDimensions[i].Start; j++)
                {
                    if (UpperNodes[j].Count > 0)
                    {
                        s += $"node{j}[";
                        for (int k = UpperNodes[j].Start; k < UpperNodes[j].Start + UpperNodes[j].Count; k++)
                        {
                            s += $"{Uppers[k]}, {Uppers[k].Dim} ";
                        }
                        s += $"]\n";
                    }

                }
            }
            s += $"in:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = InsideDimensions[i].Start; j < InsideDimensions[i].Count + InsideDimensions[i].Start; j++)
                {
                    if (InsideNodes[j].Count > 0)
                    {
                        s += $"node{j}[";
                        for (int k = InsideNodes[j].Start; k < InsideNodes[j].Start + InsideNodes[j].Count; k++)
                        {
                            s += $"{Insides[k]}, {Insides[k].Dim} ";
                        }
                        s += $"]\n";
                    }

                }
            }
            s += $"co:";
            for (int i = 0; i < HSPDIM.dimension; i++)
            {
                for (int j = CoverDimensions[i].Start; j < CoverDimensions[i].Count + CoverDimensions[i].Start; j++)
                {
                    if (CoverNodes[j].Count > 0)
                    {
                        s += $"node{j}[";
                        for (int k = CoverNodes[j].Start; k < CoverNodes[j].Start + CoverNodes[j].Count; k++)
                        {
                            s += $"{Covers[k]}, ";
                        }
                        s += $"]\n";
                    }

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
            //if (ElementList.IsCreated) ElementList.Dispose();
            //if (ElementDimensions.IsCreated) ElementDimensions.Dispose();

        }
        public void DisposePersistent()
        {
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
                        for (int k = ElementList[j].Start; k < ElementList[j].Start + ElementList[j].Count; k++)
                        {
                            s += $"{Bounds[k]}_{Bounds[k].Dim}, ";
                        }
                        s += $"]\n";
                    }
                }
            }
            return s;
        }
    }
    public struct NativeBound : IComparable<NativeBound>
    {
        public float BoundValue;
        public int Id;
        public bool IsSub;
        public int Dim;
        public int Depth;
        public int Index;
        public int LowerIndex;
        public int IsUpper;
        public bool IsInside;
        public int Start;
        public int Count;
        public bool Modified;

        public NativeBound(float boundValue, int id, bool isSub, int dim, int depth, int index, int lowerIndex, int isUpper, bool isInside, int start, int count, bool modified)
        {
            BoundValue = boundValue;
            Id = id;
            IsSub = isSub;
            Dim = dim;
            Depth = depth;
            Index = index;
            LowerIndex = lowerIndex;
            IsUpper = isUpper;
            IsInside = isInside;
            Start = start;
            Count = count;
            Modified = modified;
        }

        public readonly int CompareTo(NativeBound other)
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
    public struct OverlapID
    {
        public NativeBound rangeIDInTree;
        public int rangeIDInSortedListTree;

        public OverlapID(NativeBound rangeIDInTree, int rangeIDInSortedListTree)
        {
            this.rangeIDInTree = rangeIDInTree;
            this.rangeIDInSortedListTree = rangeIDInSortedListTree;
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
            for (int i = 0; i < rangeIDInTree.Count; i++)
            {
                yield return container[rangeIDInTree.Start+i].range;
            }
        }
        public void MapRangeToTreeArray(BinaryTree<HSPDIMTreeNodeData> tree, HSPDIMRange[] ranges)
        {
            var node = tree[rangeIDInTree.Depth, rangeIDInTree.Index].Data;
            var container = rangeIDInTree.IsInside
                ? node.insides
                : rangeIDInTree.IsUpper switch
                {
                    -1 => node.lowers,
                    1 => node.uppers,
                    0 => node.covers,
                    _ => node.lowers,
                };
            for (int i = 0; i < rangeIDInTree.Count; i++)
            {
                ranges[i] = container[rangeIDInTree.Start + i].range;
            }
        }

    }
}
