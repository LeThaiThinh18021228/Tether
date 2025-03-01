using System;
using Unity.Collections;
using UnityEngine;

namespace Framework
{
    using Unity.Collections;
    using System;
    using Framework.HSPDIMAlgo;

    public enum NodeColor
    {
        Red,
        Black
    }

    public struct FlatRBNode<T> where T : unmanaged, IComparable<T>
    {
        public T Value;
        public NodeColor Color;
        public int Left;   // index of left child (-1 if none)
        public int Right;  // index of right child (-1 if none)
        public int Parent; // index of parent (-1 if root)
    }

    public struct FlatRedBlackTree<T> where T : unmanaged, IComparable<T>
    {
        public NativeList<FlatRBNode<T>> Nodes;
        public int Root;  // index of the root node (-1 if empty)

        public FlatRedBlackTree(Allocator allocator)
        {
            Nodes = new NativeList<FlatRBNode<T>>(allocator);
            Root = -1;
        }

        public void Dispose()
        {
            if (Nodes.IsCreated)
                Nodes.Dispose();
        }

        #region Insert

        public void Insert(T value)
        {
            // Standard BST insert logic using array-based storage
            FlatRBNode<T> newNode = new FlatRBNode<T>
            {
                Value = value,
                Color = NodeColor.Red,
                Left = -1,
                Right = -1,
                Parent = -1
            };

            int newIndex = Nodes.Length;
            int y = -1;
            int x = Root;

            while (x != -1)
            {
                y = x;
                if (value.CompareTo(Nodes[x].Value) < 0)
                    x = Nodes[x].Left;
                else
                    x = Nodes[x].Right;
            }

            newNode.Parent = y;
            if (y == -1)
            {
                // Tree was empty
                Root = newIndex;
            }
            else
            {
                var tempY = Nodes[y];
                if (value.CompareTo(tempY.Value) < 0)
                    tempY.Left = newIndex;
                else
                    tempY.Right = newIndex;
                Nodes[y] = tempY;
            }

            Nodes.Add(newNode);
            InsertFixup(newIndex);
        }

        private void InsertFixup(int zIndex)
        {
            // Restores red–black properties after insert
            while (zIndex != Root && GetColor(Nodes[Nodes[zIndex].Parent]) == NodeColor.Red)
            {
                int parent = Nodes[zIndex].Parent;
                int grandparent = Nodes[parent].Parent;

                if (parent == Nodes[grandparent].Left)
                {
                    int uncle = Nodes[grandparent].Right;
                    if (uncle != -1 && GetColor(Nodes[uncle]) == NodeColor.Red)
                    {
                        // Case 1
                        SetNodeColor(uncle, NodeColor.Black);
                        SetNodeColor(parent, NodeColor.Black);
                        SetNodeColor(grandparent, NodeColor.Red);
                        zIndex = grandparent;
                    }
                    else
                    {
                        if (zIndex == Nodes[parent].Right)
                        {
                            zIndex = parent;
                            LeftRotate(zIndex);
                            parent = Nodes[zIndex].Parent;
                            grandparent = Nodes[parent].Parent;
                        }
                        SetNodeColor(parent, NodeColor.Black);
                        SetNodeColor(grandparent, NodeColor.Red);
                        RightRotate(grandparent);
                    }
                }
                else
                {
                    int uncle = Nodes[grandparent].Left;
                    if (uncle != -1 && GetColor(Nodes[uncle]) == NodeColor.Red)
                    {
                        // Case 1 (mirror)
                        SetNodeColor(uncle, NodeColor.Black);
                        SetNodeColor(parent, NodeColor.Black);
                        SetNodeColor(grandparent, NodeColor.Red);
                        zIndex = grandparent;
                    }
                    else
                    {
                        if (zIndex == Nodes[parent].Left)
                        {
                            zIndex = parent;
                            RightRotate(zIndex);
                            parent = Nodes[zIndex].Parent;
                            grandparent = Nodes[parent].Parent;
                        }
                        SetNodeColor(parent, NodeColor.Black);
                        SetNodeColor(grandparent, NodeColor.Red);
                        LeftRotate(grandparent);
                    }
                }
            }
            if (Root != -1)
            {
                var rootNode = Nodes[Root];
                rootNode.Color = NodeColor.Black;
                Nodes[Root] = rootNode;
            }
        }

        #endregion

        #region Delete

        /// <summary>
        /// Deletes the given value from the tree, returning true if found and removed,
        /// or false if the value was not in the tree.
        /// </summary>
        public bool Delete(T value)
        {
            int z = Search(value);
            if (z == -1)
                return false; // Not found

            DeleteNode(z);
            return true;
        }

        private void DeleteNode(int zIndex)
        {
            // Standard RB-Tree deletion approach:
            // 1) If the node has two children, swap with successor so we remove a node w/ <2 children
            // 2) Remove that node from the tree
            // 3) If the removed node was black, do DeleteFixup

            int yIndex = zIndex;
            var yNode = Nodes[yIndex];
            NodeColor yOriginalColor = yNode.Color;

            int xIndex;
            var zNode = Nodes[zIndex];

            if (zNode.Left == -1)
            {
                xIndex = zNode.Right;
                RBTransplant(zIndex, xIndex);
            }
            else if (zNode.Right == -1)
            {
                xIndex = zNode.Left;
                RBTransplant(zIndex, xIndex);
            }
            else
            {
                // Two children: find the successor
                int yMin = TreeMinimum(zNode.Right);
                yIndex = yMin;
                yNode = Nodes[yIndex];
                yOriginalColor = yNode.Color;
                xIndex = yNode.Right;

                // If successor is direct child of z
                if (Nodes[yIndex].Parent == zIndex)
                {
                    if (xIndex != -1)
                    {
                        var xNode = Nodes[xIndex];
                        xNode.Parent = yIndex;
                        Nodes[xIndex] = xNode;
                    }
                }
                else
                {
                    // Transplant y with x
                    RBTransplant(yIndex, xIndex);
                    // y's right = z's right
                    var yLocal = Nodes[yIndex];
                    yLocal.Right = zNode.Right;
                    Nodes[yIndex] = yLocal;

                    var zRight = Nodes[zNode.Right];
                    if (zNode.Right != -1)
                    {
                        zRight.Parent = yIndex;
                        Nodes[zNode.Right] = zRight;
                    }
                }
                // Now transplant z with y
                RBTransplant(zIndex, yIndex);

                // Copy color & left
                var finalY = Nodes[yIndex];
                finalY.Left = zNode.Left;
                finalY.Color = zNode.Color;
                Nodes[yIndex] = finalY;

                var zLeft = Nodes[zNode.Left];
                if (zNode.Left != -1)
                {
                    zLeft.Parent = yIndex;
                    Nodes[zNode.Left] = zLeft;
                }
            }

            // If the removed node was black, fix
            if (yOriginalColor == NodeColor.Black)
            {
                DeleteFixup(xIndex);
            }

            // Optionally mark zIndex as free or swap with last element in Nodes, etc.
            // If you want to re-use that slot, you'd do that here.
        }

        private void RBTransplant(int uIndex, int vIndex)
        {
            int uParent = Nodes[uIndex].Parent;
            if (uParent == -1)
            {
                Root = vIndex;
            }
            else
            {
                var pNode = Nodes[uParent];
                if (uIndex == pNode.Left)
                    pNode.Left = vIndex;
                else
                    pNode.Right = vIndex;
                Nodes[uParent] = pNode;
            }
            if (vIndex != -1)
            {
                var vNode = Nodes[vIndex];
                vNode.Parent = uParent;
                Nodes[vIndex] = vNode;
            }
        }

        private void DeleteFixup(int xIndex)
        {
            while (xIndex != -1 && xIndex != Root && GetColor(Nodes[xIndex]) == NodeColor.Black)
            {
                int xp = Nodes[xIndex].Parent;
                if (xIndex == Nodes[xp].Left)
                {
                    int wIndex = Nodes[xp].Right; // sibling
                    var wNode = Nodes[wIndex];

                    if (GetColor(wNode) == NodeColor.Red)
                    {
                        // Case 1
                        SetNodeColor(wIndex, NodeColor.Black);
                        SetNodeColor(xp, NodeColor.Red);
                        LeftRotate(xp);
                        wNode = Nodes[Nodes[xp].Right]; // update w
                    }

                    var wLeft = (wNode.Left == -1) ? NodeColor.Black : GetColor(Nodes[wNode.Left]);
                    var wRight = (wNode.Right == -1) ? NodeColor.Black : GetColor(Nodes[wNode.Right]);

                    if (wLeft == NodeColor.Black && wRight == NodeColor.Black)
                    {
                        // Case 2
                        SetNodeColor(wIndex, NodeColor.Red);
                        xIndex = xp;
                    }
                    else
                    {
                        if (wRight == NodeColor.Black)
                        {
                            // Case 3
                            if (wNode.Left != -1)
                                SetNodeColor(wNode.Left, NodeColor.Black);
                            SetNodeColor(wIndex, NodeColor.Red);
                            RightRotate(wIndex);
                            wNode = Nodes[Nodes[xp].Right]; // update w
                        }
                        // Case 4
                        SetNodeColor(wIndex, GetColor(Nodes[xp]));
                        SetNodeColor(xp, NodeColor.Black);
                        if (wNode.Right != -1)
                            SetNodeColor(wNode.Right, NodeColor.Black);
                        LeftRotate(xp);
                        xIndex = Root;
                    }
                }
                else
                {
                    // Mirror
                    int wIndex = Nodes[xp].Left;
                    var wNode = Nodes[wIndex];

                    if (GetColor(wNode) == NodeColor.Red)
                    {
                        SetNodeColor(wIndex, NodeColor.Black);
                        SetNodeColor(xp, NodeColor.Red);
                        RightRotate(xp);
                        wNode = Nodes[Nodes[xp].Left];
                    }

                    var wLeft = (wNode.Left == -1) ? NodeColor.Black : GetColor(Nodes[wNode.Left]);
                    var wRight = (wNode.Right == -1) ? NodeColor.Black : GetColor(Nodes[wNode.Right]);

                    if (wLeft == NodeColor.Black && wRight == NodeColor.Black)
                    {
                        SetNodeColor(wIndex, NodeColor.Red);
                        xIndex = xp;
                    }
                    else
                    {
                        if (wLeft == NodeColor.Black)
                        {
                            if (wNode.Right != -1)
                                SetNodeColor(wNode.Right, NodeColor.Black);
                            SetNodeColor(wIndex, NodeColor.Red);
                            LeftRotate(wIndex);
                            wNode = Nodes[Nodes[xp].Left];
                        }
                        SetNodeColor(wIndex, GetColor(Nodes[xp]));
                        SetNodeColor(xp, NodeColor.Black);
                        if (wNode.Left != -1)
                            SetNodeColor(wNode.Left, NodeColor.Black);
                        RightRotate(xp);
                        xIndex = Root;
                    }
                }
            }
            if (xIndex != -1)
            {
                var xNode = Nodes[xIndex];
                xNode.Color = NodeColor.Black;
                Nodes[xIndex] = xNode;
            }
        }

        private int TreeMinimum(int startIndex)
        {
            int current = startIndex;
            while (Nodes[current].Left != -1)
                current = Nodes[current].Left;
            return current;
        }

        #endregion

        #region Search

        // Returns the index of the node if found, else -1
        public int Search(T value)
        {
            int x = Root;
            while (x != -1)
            {
                int cmp = value.CompareTo(Nodes[x].Value);
                if (cmp == 0) return x;
                x = (cmp < 0) ? Nodes[x].Left : Nodes[x].Right;
            }
            return -1;
        }

        #endregion

        #region Rotations

        private void LeftRotate(int xIndex)
        {
            int yIndex = Nodes[xIndex].Right;
            var xNode = Nodes[xIndex];
            var yNode = Nodes[yIndex];

            // x.right = y.left
            xNode.Right = yNode.Left;
            Nodes[xIndex] = xNode;
            if (yNode.Left != -1)
            {
                var yLeftNode = Nodes[yNode.Left];
                yLeftNode.Parent = xIndex;
                Nodes[yNode.Left] = yLeftNode;
            }

            // y.parent = x.parent
            yNode.Parent = xNode.Parent;
            Nodes[yIndex] = yNode;

            if (xNode.Parent == -1)
            {
                Root = yIndex;
            }
            else
            {
                var parentNode = Nodes[xNode.Parent];
                if (xIndex == parentNode.Left)
                {
                    parentNode.Left = yIndex;
                }
                else
                {
                    parentNode.Right = yIndex;
                }
                Nodes[xNode.Parent] = parentNode;
            }

            // y.left = x
            yNode = Nodes[yIndex]; // re-read in case changed
            xNode.Parent = yIndex;
            Nodes[xIndex] = xNode;
            yNode.Left = xIndex;
            Nodes[yIndex] = yNode;
        }

        private void RightRotate(int yIndex)
        {
            int xIndex = Nodes[yIndex].Left;
            var yNode = Nodes[yIndex];
            var xNode = Nodes[xIndex];

            yNode.Left = xNode.Right;
            Nodes[yIndex] = yNode;
            if (xNode.Right != -1)
            {
                var xRightNode = Nodes[xNode.Right];
                xRightNode.Parent = yIndex;
                Nodes[xNode.Right] = xRightNode;
            }

            xNode.Parent = yNode.Parent;
            Nodes[xIndex] = xNode;

            if (yNode.Parent == -1)
            {
                Root = xIndex;
            }
            else
            {
                var pNode = Nodes[yNode.Parent];
                if (yIndex == pNode.Left)
                    pNode.Left = xIndex;
                else
                    pNode.Right = xIndex;
                Nodes[yNode.Parent] = pNode;
            }

            var xNode2 = Nodes[xIndex];
            yNode.Parent = xIndex;
            Nodes[yIndex] = yNode;
            xNode2.Right = yIndex;
            Nodes[xIndex] = xNode2;
        }

        #endregion

        #region Helpers

        private NodeColor GetColor(FlatRBNode<T> node) => node.Color;

        private void SetNodeColor(int index, NodeColor color)
        {
            var temp = Nodes[index];
            temp.Color = color;
            Nodes[index] = temp;
        }

        #endregion

        public void InOrderTraversal(ref NativeList<T> output)
        {
            InOrderInternal(Root, ref output);
        }

        private void InOrderInternal(int nodeIndex, ref NativeList<T> output)
        {
            if (nodeIndex == -1) return;

            InOrderInternal(Nodes[nodeIndex].Left, ref output);
            output.Add(Nodes[nodeIndex].Value); // 'Value' is NativeBound
            InOrderInternal(Nodes[nodeIndex].Right, ref output);
        }
    }

}
