using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.ADS
{
    public class TreeNode<T> where T : IComparable, new()
    {
        public T Data;
        public short depth;
        public int index;
        public TreeNode<T> Left;
        public TreeNode<T> Right;

        public TreeNode(short depth, int index)
        {
            this.depth = depth;
            this.index = index;
            Left = null;
            Right = null;
            Data = new T();
        }
        public TreeNode(T data)
        {
            Data = data;
            Left = null;
            Right = null;
        }

    }

    public class BinaryTree<T> : IEnumerable<TreeNode<T>> where T : IComparable, new()
    {
        public short depth;
        public TreeNode<T> Root;

        public BinaryTree()
        {
            Root = new TreeNode<T>(0, 0);
        }
        public BinaryTree(short depth)
        {
            this.depth = depth;
            Root = new TreeNode<T>(0, 0);
            InitNode(Root);
        }

        public void InitNode(TreeNode<T> root)
        {
            if (root.depth > this.depth) return;
            root.Left = new TreeNode<T>((short)(root.depth + 1), root.index * 2);
            root.Right = new TreeNode<T>((short)(root.depth + 1), root.index * 2 + 1);
            if (root.depth < this.depth)
            {
                InitNode(root.Left);
                InitNode(root.Right);
            }

        }

        public TreeNode<T> this[int depth, int index]
        {
            get
            {
                TreeNode<T> curNode = Root;
                while (curNode.depth < depth)
                {
                    if (index - curNode.index * MathF.Pow(2, (depth - curNode.depth)) >= MathF.Pow(2, depth - curNode.depth) / 2)
                    {
                        curNode = curNode.Right;
                    }
                    else
                    {
                        curNode = curNode.Left;
                    }
                }

                return curNode;
            }
        }

        public void Insert(T data)
        {
            Root = InsertRec(Root, data);
        }

        private TreeNode<T> InsertRec(TreeNode<T> root, T data)
        {
            if (root == null)
            {
                root = new TreeNode<T>(data);
                return root;
            }

            if (data.CompareTo(root.Data) < 0)
                root.Left = InsertRec(root.Left, data);
            else if (data.CompareTo(root.Data) > 0)
                root.Right = InsertRec(root.Right, data);

            return root;
        }


        // In-order traversal of the tree
        public void InOrderTraversal(TreeNode<T> node, Action<TreeNode<T>> action)
        {
            if (node != null)
            {
                InOrderTraversal(node.Left, action);
                action?.Invoke(node);
                InOrderTraversal(node.Right, action);
            }
        }

        // Pre-order traversal of the tree
        public void PreOrderTraversal(TreeNode<T> node, Action<TreeNode<T>> action)
        {
            if (node != null)
            {
                action?.Invoke(node);
                PreOrderTraversal(node.Left, action);
                PreOrderTraversal(node.Right, action);
            }
        }

        // Post-order traversal of the tree
        public void PostOrderTraversal(TreeNode<T> node, Action<TreeNode<T>> action)
        {
            if (node != null)
            {
                PostOrderTraversal(node.Left, action);
                PostOrderTraversal(node.Right, action);
                action?.Invoke(node);
            }
        }

        public IEnumerator<TreeNode<T>> GetEnumerator()
        {
            return DefaultEnumerator(Root).GetEnumerator();
        }


        private IEnumerable<TreeNode<T>> DefaultEnumerator(TreeNode<T> node)
        {
            for (short j = 0; j <= depth; j++)
            {
                float maxIndex = Mathf.Pow(2, j);
                for (int k = 0; k <= maxIndex; k++)
                {
                    yield return this[j, k];
                }
            }
        }
        public IEnumerable<TreeNode<T>> PreOrderEnumerator(TreeNode<T> node)
        {
            if (node == null)
            {
                yield break;
            }

            yield return node; // Visit the current node first

            // Recursively traverse the left subtree
            if (node.Left != null)
            {
                foreach (var leftNode in PreOrderEnumerator(node.Left))
                {
                    yield return leftNode;
                }
            }

            // Recursively traverse the right subtree
            if (node.Right != null)
            {
                foreach (var rightNode in PreOrderEnumerator(node.Right))
                {
                    yield return rightNode;
                }
            }
        }
        public IEnumerable<TreeNode<T>> PreOrderYield(TreeNode<T> node)
        {
            yield return node;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return DefaultEnumerator(Root).GetEnumerator();
        }
    }
}

