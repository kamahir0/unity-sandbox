using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpriteAnimationEditor
{
    public sealed class SpriteAnimationAsset : ScriptableObject
    {
        private const int CurrentSerializationVersion = 1;

        [SerializeField]
        private bool loop = true;

        [SerializeField, Min(1)]
        private int defaultDurationMilliseconds = 100;

        [SerializeField]
        private List<SpriteAnimationFrame> frames = new List<SpriteAnimationFrame>();

        [SerializeField, HideInInspector]
        private int serializationVersion;

        public bool Loop => loop;

        public int DefaultDurationMilliseconds => defaultDurationMilliseconds;

        public IReadOnlyList<SpriteAnimationFrame> Frames => frames;

        public int GetDurationMilliseconds(int frameIndex)
        {
            return frames[frameIndex].ResolveDurationMilliseconds(defaultDurationMilliseconds);
        }

        internal int GetDurationMilliseconds(SpriteAnimationFrame frame)
        {
            return frame.ResolveDurationMilliseconds(defaultDurationMilliseconds);
        }

        internal void InitializeDurationData()
        {
            defaultDurationMilliseconds = Mathf.Max(1, defaultDurationMilliseconds);
            serializationVersion = CurrentSerializationVersion;
        }

        internal bool UpgradeDurationData()
        {
            if (serializationVersion >= CurrentSerializationVersion)
            {
                return false;
            }

            int migratedDefault = FindMostCommonValidDuration();
            defaultDurationMilliseconds = migratedDefault;
            if (frames != null)
            {
                foreach (SpriteAnimationFrame frame in frames)
                {
                    if (frame != null)
                    {
                        frame.SetDurationOverride(
                            frame.DurationMilliseconds != migratedDefault);
                    }
                }
            }

            serializationVersion = CurrentSerializationVersion;
            return true;
        }

        private int FindMostCommonValidDuration()
        {
            if (frames == null || frames.Count == 0)
            {
                return 100;
            }

            var counts = new Dictionary<int, int>();
            int mostCommonDuration = 100;
            int highestCount = 0;
            foreach (SpriteAnimationFrame frame in frames)
            {
                if (frame == null || frame.DurationMilliseconds < 1)
                {
                    continue;
                }

                int duration = frame.DurationMilliseconds;
                counts.TryGetValue(duration, out int count);
                count++;
                counts[duration] = count;
                if (count > highestCount)
                {
                    highestCount = count;
                    mostCommonDuration = duration;
                }
            }

            return mostCommonDuration;
        }
    }

    internal static class SpriteAnimationAssetMigration
    {
        public static bool EnsureUpToDate(SpriteAnimationAsset asset)
        {
            if (asset == null || !asset.UpgradeDurationData())
            {
                return false;
            }

            EditorUtility.SetDirty(asset);
            return true;
        }
    }
}
