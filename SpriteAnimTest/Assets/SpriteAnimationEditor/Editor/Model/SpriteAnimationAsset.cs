using System.Collections.Generic;
using UnityEngine;

namespace SpriteAnimationEditor
{
    public sealed class SpriteAnimationAsset : ScriptableObject
    {
        [SerializeField]
        private bool loop = true;

        [SerializeField]
        private List<SpriteAnimationFrame> frames = new List<SpriteAnimationFrame>();

        public bool Loop => loop;

        public IReadOnlyList<SpriteAnimationFrame> Frames => frames;
    }
}
