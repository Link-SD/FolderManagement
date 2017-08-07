﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;


namespace SD.FolderManagement.Model {

    // TreeElementUtility and TreeElement are useful helper classes for backend tree data structures.
    // See tests at the bottom for examples of how to use.

    public static class TreeElementUtility {
        public static void TreeToList<T>(T root, IList<T> result) where T : TreeElement {
            if (result == null)
                throw new NullReferenceException("The input 'IList<T> result' list is null");
            result.Clear();

            Stack<T> stack = new Stack<T>();
            stack.Push(root);

            while (stack.Count > 0) {
                T current = stack.Pop();
                result.Add(current);

                if (current.Children != null && current.Children.Count > 0) {
                    for (int i = current.Children.Count - 1; i >= 0; i--) {
                        stack.Push((T)current.Children[i]);
                    }
                }
            }
        }

        // Returns the root of the tree parsed from the list (always the first element).
        // Important: the first item and is required to have a Depth value of -1. 
        // The rest of the items should have Depth >= 0. 
        public static T ListToTree<T>(IList<T> list) where T : TreeElement {
            // Validate input
            ValidateDepthValues(list);

            // Clear old states
            foreach (var element in list) {
                element.Parent = null;
                element.Children = null;
            }

            // Set child and Parent references using Depth info
            for (int ParentIndex = 0; ParentIndex < list.Count; ParentIndex++) {
                var Parent = list[ParentIndex];
                bool alreadyHasValidChildren = Parent.Children != null;
                if (alreadyHasValidChildren)
                    continue;

                int ParentDepth = Parent.Depth;
                int childCount = 0;

                // Count Children based Depth value, we are looking at Children until it's the same Depth as this object
                for (int i = ParentIndex + 1; i < list.Count; i++) {
                    if (list[i].Depth == ParentDepth + 1)
                        childCount++;
                    if (list[i].Depth <= ParentDepth)
                        break;
                }

                // Fill child array
                List<TreeElement> childList = null;
                if (childCount != 0) {
                    childList = new List<TreeElement>(childCount); // Allocate once
                    childCount = 0;
                    for (int i = ParentIndex + 1; i < list.Count; i++) {
                        if (list[i].Depth == ParentDepth + 1) {
                            list[i].Parent = Parent;
                            childList.Add(list[i]);
                            childCount++;
                        }

                        if (list[i].Depth <= ParentDepth)
                            break;
                    }
                }

                Parent.Children = childList;
            }

            return list[0];
        }

        // Check state of input list
        public static void ValidateDepthValues<T>(IList<T> list) where T : TreeElement {
            if (list.Count == 0)
                throw new ArgumentException("list should have items, count is 0, check before calling ValidateDepthValues", "list");

            if (list[0].Depth != -1)
                throw new ArgumentException("list item at index 0 should have a Depth of -1 (since this should be the hidden root of the tree). Depth is: " + list[0].Depth, "list");

            for (int i = 0; i < list.Count - 1; i++) {
                int Depth = list[i].Depth;
                int nextDepth = list[i + 1].Depth;
                if (nextDepth > Depth && nextDepth - Depth > 1)
                    throw new ArgumentException(string.Format("Invalid Depth info in input list. Depth cannot increase more than 1 per row. Index {0} has Depth {1} while index {2} has Depth {3}", i, Depth, i + 1, nextDepth));
            }

            for (int i = 1; i < list.Count; ++i)
                if (list[i].Depth < 0)
                    throw new ArgumentException("Invalid Depth value for item at index " + i + ". Only the first item (the root) should have Depth below 0.");

            if (list.Count > 1 && list[1].Depth != 0)
                throw new ArgumentException("Input list item at index 1 is assumed to have a Depth of 0", "list");
        }


        // For updating Depth values below any given element e.g after reParenting elements
        public static void UpdateDepthValues<T>(T root) where T : TreeElement {
            if (root == null)
                throw new ArgumentNullException("root", "The root is null");

            if (!root.HasChildren)
                return;

            Stack<TreeElement> stack = new Stack<TreeElement>();
            stack.Push(root);
            while (stack.Count > 0) {
                TreeElement current = stack.Pop();
                if (current.Children != null) {
                    foreach (var child in current.Children) {
                        child.Depth = current.Depth + 1;
                        stack.Push(child);
                    }
                }
            }
        }

        // Returns true if there is an ancestor of child in the elements list
        static bool IsChildOf<T>(T child, IList<T> elements) where T : TreeElement {
            while (child != null) {
                child = (T)child.Parent;
                if (elements.Contains(child))
                    return true;
            }
            return false;
        }

        public static IList<T> FindCommonAncestorsWithinList<T>(IList<T> elements) where T : TreeElement {
            if (elements.Count == 1)
                return new List<T>(elements);

            List<T> result = new List<T>(elements);
            result.RemoveAll(g => IsChildOf(g, elements));
            return result;
        }
    }



    class TreeElementUtilityTests {
        class TestElement : TreeElement {
            public TestElement(string name, int Depth) {
                this.Name = name;
                this.Depth = Depth;
            }
        }

        #region Tests
        [Test]
        public static void TestTreeToListWorks() {
            // Arrange
            TestElement root = new TestElement("root", -1);
            root.Children = new List<TreeElement>();
            root.Children.Add(new TestElement("A", 0));
            root.Children.Add(new TestElement("B", 0));
            root.Children.Add(new TestElement("C", 0));

            root.Children[1].Children = new List<TreeElement>();
            root.Children[1].Children.Add(new TestElement("Bchild", 1));

            root.Children[1].Children[0].Children = new List<TreeElement>();
            root.Children[1].Children[0].Children.Add(new TestElement("Bchildchild", 2));

            // Test
            List<TestElement> result = new List<TestElement>();
            TreeElementUtility.TreeToList(root, result);

            // Assert
            string[] namesInCorrectOrder = { "root", "A", "B", "Bchild", "Bchildchild", "C" };
            Assert.AreEqual(namesInCorrectOrder.Length, result.Count, "Result count is not match");
            for (int i = 0; i < namesInCorrectOrder.Length; ++i) {
                Assert.AreEqual(namesInCorrectOrder[i], result[i].Name);
            }
            TreeElementUtility.ValidateDepthValues(result);
        }


        [Test]
        public static void TestListToTreeWorks() {
            // Arrange
            var list = new List<TestElement>();
            list.Add(new TestElement("root", -1));
            list.Add(new TestElement("A", 0));
            list.Add(new TestElement("B", 0));
            list.Add(new TestElement("Bchild", 1));
            list.Add(new TestElement("Bchildchild", 2));
            list.Add(new TestElement("C", 0));

            // Test
            TestElement root = TreeElementUtility.ListToTree(list);

            // Assert
            Assert.AreEqual("root", root.Name);
            Assert.AreEqual(3, root.Children.Count);
            Assert.AreEqual("C", root.Children[2].Name);
            Assert.AreEqual("Bchildchild", root.Children[1].Children[0].Children[0].Name);
        }

        [Test]
        public static void TestListToTreeThrowsExceptionIfRootIsInvalidDepth() {
            // Arrange
            var list = new List<TestElement>();
            list.Add(new TestElement("root", 0));
            list.Add(new TestElement("A", 1));
            list.Add(new TestElement("B", 1));
            list.Add(new TestElement("Bchild", 2));

            // Test
            bool catchedException = false;
            try {
                TreeElementUtility.ListToTree(list);
            } catch (Exception) {
                catchedException = true;
            }

            // Assert
            Assert.IsTrue(catchedException, "We require the root.Depth to be -1, here it is: " + list[0].Depth);

        }

        [Test]
        public static void FindCommonAncestorsWithinListWorks() {
            // Arrange
            var list = new List<TestElement>();
            list.Add(new TestElement("root", -1));
            list.Add(new TestElement("A", 0));
            var b0 = new TestElement("B", 0);
            var b1 = new TestElement("Bchild", 1);
            var b2 = new TestElement("Bchildchild", 2);
            list.Add(b0);
            list.Add(b1);
            list.Add(b2);

            var c0 = new TestElement("C", 0);
            list.Add(c0);

            var f0 = new TestElement("F", 0);
            var f1 = new TestElement("Fchild", 1);
            var f2 = new TestElement("Fchildchild", 2);
            list.Add(f0);
            list.Add(f1);
            list.Add(f2);

            // Init tree structure: set Children and Parent properties
            TreeElementUtility.ListToTree(list);


            // Single element
            TestElement[] input = { b1 };
            TestElement[] expectedResult = { b1 };
            var result = TreeElementUtility.FindCommonAncestorsWithinList(input).ToArray();
            Assert.IsTrue(ArrayUtility.ArrayEquals(expectedResult, result), "Single input should return single output");

            // Single sub tree
            input = new[] { b1, b2 };
            expectedResult = new[] { b1 };
            result = TreeElementUtility.FindCommonAncestorsWithinList(input).ToArray();
            Assert.IsTrue(ArrayUtility.ArrayEquals(expectedResult, result), "Common ancestor should only be b1 ");

            // Multiple sub trees
            input = new[] { b0, b2, f0, f2, c0 };
            expectedResult = new[] { b0, f0, c0 };
            result = TreeElementUtility.FindCommonAncestorsWithinList(input).ToArray();
            Assert.IsTrue(ArrayUtility.ArrayEquals(expectedResult, result), "Common ancestor should only be b0, f0, c0");
        }

        #endregion
    }


}