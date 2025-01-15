using Framework;
using Framework.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HSPDIMAlgo
{
    public class HSPDIMNodeData
    {
        public float lowerBound;
        public float upperBound;
        public List<Bound> lowers = new();
        public List<Bound> uppers = new();
        public List<Bound> covers = new();
        public List<Bound> insides = new();
        public int CompareTo(object obj)
        {
            throw new NotImplementedException();
        }
        public override string ToString()
        {
            JSONNode tree = new JSONClass();
            tree.Add("node", $"[{lowerBound},{upperBound}]");
            if (lowers.Count > 0)
            {
                JSONArray l = new JSONArray();
                lowers.ForEach(x => l.Add(new JSONData(x.boundValue)));
                tree.Add("l", l);
            }
            if (uppers.Count > 0)
            {
                JSONArray u = new JSONArray();
                uppers.ForEach(x => u.Add(new JSONData(x.boundValue)));
                tree.Add("u", u);
            }
            if (covers.Count > 0)
            {
                JSONArray c = new JSONArray();
                covers.ForEach(x => c.Add(new JSONData(x.boundValue)));
                tree.Add("c", c);
            }
            if (insides.Count > 0)
            {
                JSONArray i = new JSONArray();
                insides.ForEach(x => i.Add(new JSONData(x.boundValue)));
                tree.Add("i", i);
            }
            return tree.ToString();
        }
        public bool IsEmpty()
        {
            return lowers.Count == 0 && uppers.Count == 0 && covers.Count == 0 && insides.Count == 0;
        }

        public NativeHSPDIMNodeData ToNativeNodeData()
        {
            return new NativeHSPDIMNodeData()
            {

            };
        }
    }
    public class Bound : IComparable<Bound>
    {
        public float boundValue;
        public short isUpper;
        public short dimId;
        public int index = -1;
        public short alterDim;
        public Range range;
        public Bound(short dimId, short isUpper, Range range)
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
        }

        public int CompareTo(Bound other)
        {
            if (boundValue > other.boundValue) return 1;
            else if (boundValue < other.boundValue) return -1;
            else return range.GetHashCode().CompareTo(other.range.GetHashCode());
        }
        public override string ToString()
        {
            return $"{boundValue}({isUpper})";
        }

        public void UpdateBound()
        {
            boundValue = range.oldPos[dimId] + isUpper * range.range[alterDim] / 2 + HSPDIM.mapSizeEstimate / 2;
            index = (range.entity.IsServerInitialized && range.entity.enabled) ? HSPDIM.IndexCal(boundValue, range.depthLevel[dimId]) : -1;
        }

        public NativeBound ToNativeBound(int indexInContainer, bool isInside = false, int lowerIndex = -1, int lowerIndexInContainer = -1)
        {
            if (lowerIndex == -1)
            {
                throw new ArgumentException();
            }
            return new NativeBound(boundValue, default,
                new(dimId, range.depthLevel[dimId], this.index, isUpper, isInside, indexInContainer, 1, lowerIndexInContainer, lowerIndex));
        }
        public NativeBound ToNativeBound(int indexInContainer, int index = -1, int lowerIndexInContainer = -1)
        {
            return new NativeBound(boundValue, new(dimId, index, indexInContainer, lowerIndexInContainer), new(dimId, range.depthLevel[dimId], this.index, isUpper, false, indexInContainer, 1, -1, -1));
        }
    }
    public class Range
    {
        public Vector3 range;
        public Vector3 oldPos;
        public Vector3Int depthLevel;
        public HSPDIMEntity entity;
        public Bound[,] Bounds = new Bound[HSPDIM.dimension, 3];
        public HashSet<Range>[] overlapSets = Enumerable.Range(0, HSPDIM.dimension).Select(_ => new HashSet<Range>()).ToArray();
        public IEnumerable<Range> intersection;
        public Action OnUpdateIntersection;
        public Range(Vector3 range, HSPDIMEntity entity, short treeDepth)
        {
            this.range = range;
            this.entity = entity;
            intersection = new HashSet<Range>();
            oldPos = new Vector3(entity.transform.position.x, entity.transform.position.z);

            for (short j = 0; j < HSPDIM.dimension; j++)
            {
                Bounds[j, 0] = Bounds[j, 0] ?? new Bound(j, -1, this);
                Bounds[j, 1] = Bounds[j, 1] ?? new Bound(j, 1, this);
                Bounds[j, 2] = Bounds[j, 2] ?? new Bound(j, 0, this);
            }
            UpdateRange(treeDepth);
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
            oldPos = new Vector3(entity.transform.position.x, entity.transform.position.z);
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
                intersection = intersection.Intersect(overlapSets[i]);
            }
            OnUpdateIntersection?.Invoke();
        }
        public override string ToString()
        {
            return $"{entity.name}_{GetHashCode()}_{range}_{oldPos}_{entity.Modified}_({Bounds[0, 0].boundValue}_{Bounds[0, 0].index},{Bounds[0, 1].boundValue}_{Bounds[0, 1].index},{Bounds[1, 0].boundValue}_{Bounds[1, 0].index},{Bounds[1, 1].boundValue}_{Bounds[1, 1].index})";
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

        // Implement the != operator
        public static bool operator !=(Vector3Bool left, Vector3Bool right)
        {
            return !(left == right);
        }
    }
}
