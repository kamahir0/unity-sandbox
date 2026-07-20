using System;
using UnityEngine;

namespace SpriteAnimationEditor
{
    [Serializable]
    public sealed class SpriteAnimationFrame
    {
        [SerializeField]
        private Sprite sprite;

        [SerializeField]
        private bool overrideDuration;

        [SerializeField, Min(1)]
        private int durationMilliseconds = 100;

        public Sprite Sprite => sprite;

        public bool OverrideDuration => overrideDuration;

        public int DurationMilliseconds => durationMilliseconds;

        internal int ResolveDurationMilliseconds(int defaultDurationMilliseconds)
        {
            return overrideDuration ? durationMilliseconds : defaultDurationMilliseconds;
        }

        internal void SetDurationOverride(bool value)
        {
            overrideDuration = value;
        }
    }
}
