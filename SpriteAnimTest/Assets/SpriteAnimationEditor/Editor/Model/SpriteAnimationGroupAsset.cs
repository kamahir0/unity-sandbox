using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteAnimationEditor
{
    [CreateAssetMenu(
        fileName = "NewSpriteAnimationGroup",
        menuName = "Sprite Animation Editor/Animation Group Asset")]
    public sealed class SpriteAnimationGroupAsset : ScriptableObject
    {
        [SerializeField]
        private List<SpriteAnimationAsset> animations = new List<SpriteAnimationAsset>();

        [SerializeField]
        private DefaultAsset outputFolder;

        [SerializeField]
        private string bindingPath = string.Empty;

        public IReadOnlyList<SpriteAnimationAsset> Animations => animations;

        public DefaultAsset OutputFolder => outputFolder;

        public string BindingPath => bindingPath;
    }
}
