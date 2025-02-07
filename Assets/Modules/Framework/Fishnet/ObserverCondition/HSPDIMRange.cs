using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Framework.HSPDIMAlgo
{
    public class HSPDIMBound : IComparable<HSPDIMBound>
    {
        public float boundValue;
        public short isUpper;
        public short dimId;
        public int index = -1;
        public short alterDim;
        public HSPDIMRange range;
        public HSPDIMBound(short dimId, short isUpper, HSPDIMRange range)
        {
            this.dimId = dimId;
            this.isUpper = isUpper;
            this.range = range;
            alterDim = dimId;
            if (HSPDIM.dimension == 2 && dimId == 1)
            {
                alterDim = 2;
            }
            UpdateBound();
            index = -1;
        }

        public int CompareTo(HSPDIMBound other)
        {
            if (boundValue > other.boundValue) return 1;
            else if (boundValue < other.boundValue) return -1;
            else return range.entity.Id.CompareTo(other.range.entity.Id);
        }
        public override string ToString()
        {
            return $"{boundValue}({isUpper})";
        }

        public void UpdateBound()
        {
            boundValue = range.entity.Position[dimId] + isUpper * range.range[alterDim] / 2 + HSPDIM.mapSizeEstimate / 2;
            index = range.entity.Enable ? HSPDIM.IndexCal(boundValue, range.depthLevel[dimId]) : -1;
        }

        public NativeBound ToNativeBound(int indexInContainer, bool isInside = false, int lowerIndexInContainer = -1)
        {
            return new NativeBound(boundValue,
                default,
                new(dimId, range.depthLevel[dimId], this.index, isUpper, isInside, indexInContainer, 1, lowerIndexInContainer));
        }
        public NativeBound ToNativeBound(int depth, int indexInContainer, bool isInside = false, int index = -1, int lowerIndexInContainer = -1)
        {
            return new NativeBound(boundValue,
                new(dimId, depth, index, isUpper, isInside, indexInContainer, lowerIndexInContainer),
                default);
        }
    }
    public class HSPDIMRange
    {
        public Vector3 range;
        public Vector3Int depthLevel;
        public IHSPDIMEntity entity;
        public HSPDIMBound[,] Bounds = new HSPDIMBound[HSPDIM.dimension, 3];
        public HashSet<HSPDIMRange>[] overlapSets = Enumerable.Range(0, HSPDIM.dimension).Select(_ => new HashSet<HSPDIMRange>()).ToArray();
        public HashSet<HSPDIMRange> intersection;
        public Action OnUpdateIntersection;
        public HSPDIMRange(Vector3 range, IHSPDIMEntity entity, short treeDepth)
        {
            this.range = range;
            this.entity = entity;
            intersection = new HashSet<HSPDIMRange>();
            entity.UpdatePos();
            for (short j = 0; j < HSPDIM.dimension; j++)
            {
                Bounds[j, 0] = Bounds[j, 0] ?? new HSPDIMBound(j, -1, this);
                Bounds[j, 1] = Bounds[j, 1] ?? new HSPDIMBound(j, 1, this);
                Bounds[j, 2] = Bounds[j, 2] ?? new HSPDIMBound(j, 0, this);
            }
            //UpdateRange(treeDepth);
        }
        public void UpdateRange(short i, int treeDepth)
        {
            short dimId = i;
            if (HSPDIM.dimension == 2 && i == 1)
            {
                dimId = 2;
            }
            if (range[dimId] < HSPDIM.mapSizeEstimate / Mathf.Pow(2, treeDepth))
            {
                depthLevel[i] = treeDepth;
            }
            else
            {
                depthLevel[i] = HSPDIM.DepthCal(range[dimId]);
            }
            Bounds[i, 0].UpdateBound();
            Bounds[i, 1].UpdateBound();
            Bounds[i, 2].UpdateBound();
        }
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
        public void UpdateIntersection()
        {
            intersection = overlapSets[0];
            for (int i = 1; i < HSPDIM.dimension; i++)
            {
                intersection.IntersectWith(overlapSets[i]);
            }
            if (intersection.Count() > 0)
            {
                //PDebug.Log($"range {entity.ObjectId} intersect {string.Join(",", intersection.Select(s => s))}");
            }
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
