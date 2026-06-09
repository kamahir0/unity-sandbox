#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UxmlToUgui
{
    public static class UxmlLayoutPostProcessor
    {
        [MenuItem("Tools/UxmlLayoutPostProcessor/ProcessSujimonPrefab")]
        public static void ProcessSujimonPrefab()
        {
            ProcessPrefab("Assets/UxmlToUgui/sujimon_battle_ui.prefab");
        }

        public static void ProcessPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var todos = new List<Transform>();
                FindTodos(root.transform, todos);

                foreach (var todo in todos)
                {
                    var parent = todo.parent;
                    if (parent == null) continue;

                    var isRow = parent.GetComponent<HorizontalLayoutGroup>() != null;
                    var isCol = parent.GetComponent<VerticalLayoutGroup>() != null;

                    if (!isRow && !isCol)
                    {
                        Debug.LogWarning($"Parent {parent.name} of TODO {todo.name} does not have a Horizontal or Vertical LayoutGroup.");
                        continue;
                    }

                    // Gather normal layout children
                    var children = new List<Transform>();
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var child = parent.GetChild(i);
                        if (child == todo) continue;
                        if (child.name.StartsWith("[TODO]")) continue;
                        if (child.name.StartsWith("[Overlay]")) continue;
                        
                        var le = child.GetComponent<LayoutElement>();
                        if (le != null && le.ignoreLayout) continue;

                        children.Add(child);
                    }

                    if (children.Count == 0)
                    {
                        Object.DestroyImmediate(todo.gameObject);
                        continue;
                    }

                    bool isSpaceBetween = todo.name.Contains("SpaceBetween");
                    bool isSpaceAround = todo.name.Contains("SpaceAround");

                    if (isSpaceBetween)
                    {
                        // Add spacer between children
                        for (int i = children.Count - 2; i >= 0; i--)
                        {
                            var spacer = CreateSpacer(parent, isRow);
                            spacer.SetSiblingIndex(children[i].GetSiblingIndex() + 1);
                        }
                    }
                    else if (isSpaceAround)
                    {
                        // Add spacer before first, between, and after last
                        for (int i = children.Count - 1; i >= 0; i--)
                        {
                            var spacerAfter = CreateSpacer(parent, isRow);
                            spacerAfter.SetSiblingIndex(children[i].GetSiblingIndex() + 1);
                        }
                        var spacerBefore = CreateSpacer(parent, isRow);
                        spacerBefore.SetSiblingIndex(children[0].GetSiblingIndex());
                    }

                    Object.DestroyImmediate(todo.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"Successfully processed prefab: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FindTodos(Transform current, List<Transform> result)
        {
            if (current.name.StartsWith("[TODO]"))
            {
                result.Add(current);
            }
            for (int i = 0; i < current.childCount; i++)
            {
                FindTodos(current.GetChild(i), result);
            }
        }

        private static Transform CreateSpacer(Transform parent, bool isRow)
        {
            var go = new GameObject("[Spacer]", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            if (isRow)
            {
                le.flexibleWidth = 1f;
                le.preferredWidth = 0f;
            }
            else
            {
                le.flexibleHeight = 1f;
                le.preferredHeight = 0f;
            }
            return go.transform;
        }
    }
}
#endif
