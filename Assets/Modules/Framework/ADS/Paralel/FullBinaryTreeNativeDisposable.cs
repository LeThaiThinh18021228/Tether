using System;
using Unity.Collections;
using UnityEngine;

namespace Framework.ADS.Paralel
{
    public struct TreeNodeNative<T> where T : struct
    {
        public T Data;
        public short Depth;
        public int Index;

        public TreeNodeNative(T data, short depth, int index)
        {
            Data = data;
            Depth = depth;
            Index = index;
        }
    }
    public struct FullBinaryTreeNativeDisposable<T> where T : struct, IDisposable
    {
        public NativeArray<TreeNodeNative<T>> Nodes;
        public short Depth;

        public FullBinaryTreeNativeDisposable(short depth, Allocator allocator)
        {
            Depth = depth;
            int totalNodes = (int)Mathf.Pow(2, depth + 1) - 1;
            Nodes = new NativeArray<TreeNodeNative<T>>(totalNodes, allocator);

            for (int i = 0; i < totalNodes; i++)
            {
                short nodeDepth = (short)(Mathf.FloorToInt(Mathf.Log(i + 1, 2)));
                Nodes[i] = new TreeNodeNative<T>(default, nodeDepth, i);
            }
        }

        public TreeNodeNative<T> GetLeftChild(int index)
        {
            int leftIndex = 2 * index + 1;
            if (leftIndex < Nodes.Length)
            {
                return Nodes[leftIndex];
            }
            throw new System.IndexOutOfRangeException("No left child");
        }

        public TreeNodeNative<T> GetRightChild(int index)
        {
            int rightIndex = 2 * index + 2;
            if (rightIndex < Nodes.Length)
            {
                return Nodes[rightIndex];
            }
            throw new System.IndexOutOfRangeException("No right child");
        }
        public TreeNodeNative<T> this[int depth, int index]
        {
            get
            {
                return Nodes[2 ^ depth + index - 1];
            }
        }
        public TreeNodeNative<T> GetNode(int index)
        {
            int rightIndex = 2 * index + 2;
            if (rightIndex < Nodes.Length)
            {
                return Nodes[rightIndex];
            }
            throw new System.IndexOutOfRangeException("No right child");
        }
        public void Dispose()
        {
            if (Nodes.IsCreated)
            {
                for (int i = 0; i < Nodes.Length; i++)
                {
                    Nodes[i].Data.Dispose();
                }
                Nodes.Dispose();
            }
        }

    }
    public struct FullBinaryTreeNative<T> where T : struct
    {
        public NativeArray<TreeNodeNative<T>> Nodes;
        public short Depth;

        public FullBinaryTreeNative(short depth, Allocator allocator)
        {
            Depth = depth;
            int totalNodes = (int)Mathf.Pow(2, depth + 1) - 1;
            Nodes = new NativeArray<TreeNodeNative<T>>(totalNodes, allocator);

            for (int i = 0; i < totalNodes; i++)
            {
                short nodeDepth = (short)(Mathf.FloorToInt(Mathf.Log(i + 1, 2)));
                Nodes[i] = new TreeNodeNative<T>(default, nodeDepth, i);
            }
        }

        public TreeNodeNative<T> GetLeftChild(int index)
        {
            int leftIndex = 2 * index + 1;
            if (leftIndex < Nodes.Length)
            {
                return Nodes[leftIndex];
            }
            throw new System.IndexOutOfRangeException("No left child");
        }

        public TreeNodeNative<T> GetRightChild(int index)
        {
            int rightIndex = 2 * index + 2;
            if (rightIndex < Nodes.Length)
            {
                return Nodes[rightIndex];
            }
            throw new System.IndexOutOfRangeException("No right child");
        }
        public TreeNodeNative<T> this[int depth, int index]
        {
            get
            {
                return Nodes[2 ^ depth + index - 1];
            }
        }
        public TreeNodeNative<T> GetNode(int index)
        {
            int rightIndex = 2 * index + 2;
            if (rightIndex < Nodes.Length)
            {
                return Nodes[rightIndex];
            }
            throw new System.IndexOutOfRangeException("No right child");
        }
        public void Dispose()
        {
            if (Nodes.IsCreated)
            {
                Nodes.Dispose();
            }
        }
    }
}
