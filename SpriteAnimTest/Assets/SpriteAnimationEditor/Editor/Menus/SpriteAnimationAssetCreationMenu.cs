using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteAnimationEditor
{
    internal static class SpriteAnimationAssetCreationMenu
    {
        private const string MenuPath =
            "Assets/Create/Sprite Animation Editor/Animation Asset";

        [MenuItem(MenuPath, false, 201)]
        private static void CreateAnimationAsset()
        {
            SpriteAnimationAsset asset = SpriteAnimationAssetFactory.Create(Selection.objects);
            ProjectWindowUtil.CreateAsset(asset, "New Sprite Animation.asset");
        }
    }

    internal static class SpriteAnimationAssetFactory
    {
        public static SpriteAnimationAsset Create(IEnumerable<UnityEngine.Object> selectedObjects)
        {
            var asset = ScriptableObject.CreateInstance<SpriteAnimationAsset>();
            List<Sprite> sprites = SpriteAnimationSpriteCollector.Collect(selectedObjects);
            if (sprites.Count == 0)
            {
                return asset;
            }

            var serialized = new SerializedObject(asset);
            SerializedProperty frames = serialized.FindProperty("frames");
            frames.arraySize = sprites.Count;
            for (var index = 0; index < sprites.Count; index++)
            {
                SerializedProperty frame = frames.GetArrayElementAtIndex(index);
                frame.FindPropertyRelative("sprite").objectReferenceValue = sprites[index];
                frame.FindPropertyRelative("durationMilliseconds").intValue = 100;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }
    }
}
