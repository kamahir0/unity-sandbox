using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpriteAnimationEditor
{
    internal static class SpriteAnimationSpriteCollector
    {
        public static List<Sprite> Collect(IEnumerable<UnityEngine.Object> objects)
        {
            var sprites = new List<Sprite>();
            var seenInstanceIds = new HashSet<int>();

            foreach (UnityEngine.Object item in objects ?? Array.Empty<UnityEngine.Object>())
            {
                if (item is Sprite sprite)
                {
                    if (seenInstanceIds.Add(sprite.GetInstanceID()))
                    {
                        sprites.Add(sprite);
                    }

                    continue;
                }

                if (!(item is Texture2D))
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(item);
                foreach (Sprite subSprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    if (seenInstanceIds.Add(subSprite.GetInstanceID()))
                    {
                        sprites.Add(subSprite);
                    }
                }
            }

            return sprites
                .OrderBy(sprite => sprite.name, NaturalStringComparer.Instance)
                .ToList();
        }
    }
}
