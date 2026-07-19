using System;
using UnityEngine;

namespace SpriteAnimationEditor
{
    [Serializable]
    public sealed class SpriteAnimationFrame
    {
        [SerializeField]
        private Sprite sprite;

        [SerializeField, Min(1)]
        private int durationMilliseconds = 100;

        public Sprite Sprite => sprite;

        public int DurationMilliseconds => durationMilliseconds;
    }
}
