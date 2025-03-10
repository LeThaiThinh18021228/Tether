using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Framework.HSPDIMAlgo
{
    public class HSPDIMBound : IComparable<HSPDIMBound>
    {
        public int id;
        public bool isSub;
        public float boundValue;
        public short isUpper;
        public short dimId;
        public int index = -1;
        public HSPDIMRange range;
        public IHSPDIMEntity entity;
        public HSPDIMBound(short dimId, short isUpper, HSPDIMRange range, IHSPDIMEntity entity, bool isSub)
        {
            this.dimId = dimId;
            this.isUpper = isUpper;
            this.range = range;
            this.entity = entity;
            this.isSub = isSub;
            UpdateBound();
            index = -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(HSPDIMBound other)
        {
            if (boundValue > other.boundValue) return 1;
            else if (boundValue < other.boundValue) return -1;
            else return entity.Id.CompareTo(other.entity.Id);
        }
        public override string ToString()
        {
            return $"{boundValue}({isUpper})";
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateBound()
        {
            boundValue = entity.Position[dimId] + isUpper * range.range[dimId] / 2 + HSPDIM.mapSizeEstimate / 2;
            index = entity.Enable ? HSPDIM.IndexCal(boundValue, range.depthLevel[dimId]) : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeBound ToNativeBound(HSPDIMBound bound, int indexInContainer, bool isInside = false)
        {
            var range = bound.range;
            var entity = bound.entity;
            int dim = bound.dimId;
            return new NativeBound(bound.boundValue,
                entity.Id, range.isSub, dim, range.depthLevel[dim], bound.index, range.Bounds[dim, 0].index, bound.isUpper, isInside, indexInContainer, 1, entity.Modified[dim]);
        }
    }
    public class HSPDIMRange
    {
        public bool isSub;
        public Vector3 range;
        public Vector3Int depthLevel;
        public IHSPDIMEntity entity;
        public HSPDIMBound[,] Bounds = new HSPDIMBound[HSPDIM.dimension, 3];
        public NativeBound[,] Boundss = new NativeBound[HSPDIM.dimension, 3];
        public HashSet<int>[] overlapSetsId;
        public HashSet<int> intersectionId;
        public Action OnUpdateIntersection;
        public HSPDIMRange(Vector3 range, IHSPDIMEntity entity, short treeDepth, bool isSub, int preallocateHash = 100)
        {
            this.isSub = isSub;
            this.range = range;
            this.entity = entity;
            overlapSetsId = Enumerable.Range(0, HSPDIM.dimension).Select(_ => new HashSet<int>(preallocateHash)).ToArray();
            intersectionId = new HashSet<int>((int)Math.Sqrt(preallocateHash));
            entity.UpdatePos();
            for (short j = 0; j < HSPDIM.dimension; j++)
            {
                Bounds[j, 0] = Bounds[j, 0] ?? new HSPDIMBound(j, -1, this, entity, isSub);
                Bounds[j, 1] = Bounds[j, 1] ?? new HSPDIMBound(j, 1, this, entity, isSub);
                Bounds[j, 2] = Bounds[j, 2] ?? new HSPDIMBound(j, 0, this, entity, isSub);
                Boundss[j, 0] = new NativeBound(0, entity.Id, isSub, j, 0, 0, 0, -1, false, 0, 1, true);
                Boundss[j, 1] = new NativeBound(0, entity.Id, isSub, j, 0, 0, 0, 1, false, 0, 1, true);
                Boundss[j, 2] = new NativeBound(0, entity.Id, isSub, j, 0, 0, 0, 0, false, 0, 1, true);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateRange(short i, int treeDepth)
        {
            if (range[i] < HSPDIM.mapSizeEstimate / (1 << treeDepth))
            {
                depthLevel[i] = treeDepth;
            }
            else
            {
                depthLevel[i] = HSPDIM.DepthCal(range[i]);
            }
            Bounds[i, 0].UpdateBound();
            Bounds[i, 1].UpdateBound();
            Bounds[i, 2].UpdateBound();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateRange(int treeDepth)
        {
            entity.UpdatePos();
            for (short i = 0; i < HSPDIM.dimension; i++)
            {
                if (entity.Modified[i])
                {
                    UpdateRange(i, treeDepth);
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateIntersectionId()
        {
            intersectionId.Clear();
            intersectionId.UnionWith(overlapSetsId[0]);
            for (int j = 1; j < HSPDIM.dimension; j++)
            {
                intersectionId.IntersectWith(overlapSetsId[j]);
            }
            //int minIndex = 0;
            //int minCount = overlapSetsId[0].Count;
            //for (int i = 1; i < overlapSetsId.Length; i++)
            //{
            //    int currentCount = overlapSetsId[i].Count;
            //    if (currentCount < minCount)
            //    {
            //        minCount = currentCount;
            //        minIndex = i;
            //    }
            //}
            //var smallestList = overlapSetsId[minIndex].ToArray();
            //for (int i = 0; i < smallestList.Length; i++)
            //{
            //    bool presentInAll = true;
            //    for (int j = 0; j < HSPDIM.dimension; j++)
            //    {
            //        if (!overlapSetsId[j].Contains(smallestList[i]))
            //        {
            //            presentInAll = false;
            //            break;
            //        }
            //    }
            //    if (presentInAll)
            //    {
            //        intersectionId.Add(smallestList[i]);
            //    }
            //}
            OnUpdateIntersection?.Invoke();
        }
        
        public override string ToString()
        {
            return $"{GetHashCode()}_{range}_{entity.Position}_{entity.Modified}_({Bounds[0, 0].boundValue}_{Bounds[0, 0].index},{Bounds[0, 1].boundValue}_{Bounds[0, 1].index},{Bounds[1, 0].boundValue}_{Bounds[1, 0].index},{Bounds[1, 1].boundValue}_{Bounds[1, 1].index})";
        }
    }

    public struct Vector3Bool
    {
        public bool X;
        public bool Y;
        public bool Z;

        public Vector3Bool(bool x, bool y, bool z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        public static readonly Vector3Bool @false = new(false, false, false);
        public static readonly Vector3Bool @true = new(true, true, true);
        public bool this[int index]
        {
            get
            {
                return index switch
                {
                    0 => X,
                    1 => Y,
                    2 => Z,
                    _ => throw new IndexOutOfRangeException("Index must be 0, 1, or 2")
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Index must be 0, 1, or 2");
                }
            }
        }
        public override string ToString() => $"({X}, {Y}, {Z})";

        public override bool Equals(object obj)
        {
            if (obj is Vector3Bool other)
            {
                return this == other;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (X ? 1 : 0) | (Y ? 2 : 0) | (Z ? 4 : 0);
        }

        public static bool operator ==(Vector3Bool left, Vector3Bool right)
        {
            return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
        }
        public static bool operator !=(Vector3Bool left, Vector3Bool right)
        {
            return !(left == right);
        }
    }
}
