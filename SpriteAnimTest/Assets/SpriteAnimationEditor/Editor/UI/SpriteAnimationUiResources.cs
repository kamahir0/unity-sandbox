using UnityEditor;
using UnityEngine.UIElements;

namespace SpriteAnimationEditor
{
    internal static class SpriteAnimationUiResources
    {
        private const string UiRoot = "Assets/SpriteAnimationEditor/Editor/UI/";

        public static VisualElement Clone(string uxmlFileName, string ussFileName = null)
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiRoot + uxmlFileName);
            VisualElement root = tree != null ? tree.CloneTree() : new VisualElement();

            if (!string.IsNullOrEmpty(ussFileName))
            {
                AddStyleSheet(root, ussFileName);
            }

            return root;
        }

        public static void AddStyleSheet(VisualElement root, string ussFileName)
        {
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiRoot + ussFileName);
            if (style != null)
            {
                root.styleSheets.Add(style);
            }
        }
    }
}
