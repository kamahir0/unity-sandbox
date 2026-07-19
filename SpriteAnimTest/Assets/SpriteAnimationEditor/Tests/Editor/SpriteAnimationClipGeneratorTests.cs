using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpriteAnimationEditor.Tests
{
    public sealed class SpriteAnimationClipGeneratorTests
    {
        private string testRoot;
        private Sprite firstSprite;
        private Sprite secondSprite;

        [SetUp]
        public void SetUp()
        {
            string folderName = "SpriteAnimationEditorTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folderName);
            testRoot = "Assets/" + folderName;
            CreateSprites();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(testRoot) && AssetDatabase.IsValidFolder(testRoot))
            {
                AssetDatabase.DeleteAsset(testRoot);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void ApplyToClip_CreatesExpectedKeysBindingDurationAndLoop()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "Walk",
                true,
                (firstSprite, 100),
                (secondSprite, 150));
            var clip = new AnimationClip();

            try
            {
                SpriteAnimationClipGenerator.ApplyToClip(source, clip, "Visual");

                EditorCurveBinding binding =
                    AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                Assert.That(binding.path, Is.EqualTo("Visual"));
                Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
                Assert.That(binding.propertyName, Is.EqualTo("m_Sprite"));

                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(keys, Has.Length.EqualTo(3));
                Assert.That(keys[0].time, Is.EqualTo(0f).Within(0.00001f));
                Assert.That(keys[1].time, Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(keys[2].time, Is.EqualTo(0.249f).Within(0.00001f));
                Assert.That(keys[0].value, Is.SameAs(firstSprite));
                Assert.That(keys[1].value, Is.SameAs(secondSprite));
                Assert.That(keys[2].value, Is.SameAs(secondSprite));
                Assert.That(clip.frameRate, Is.EqualTo(1000f));
                Assert.That(clip.length, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void AssetFactory_PopulatesSelectedSpritesInNaturalOrder()
        {
            SpriteAnimationAsset asset = SpriteAnimationAssetFactory.Create(
                new UnityEngine.Object[] { secondSprite, firstSprite });

            try
            {
                Assert.That(asset.Frames.Count, Is.EqualTo(2));
                Assert.That(asset.Frames[0].Sprite, Is.SameAs(firstSprite));
                Assert.That(asset.Frames[1].Sprite, Is.SameAs(secondSprite));
                Assert.That(asset.Frames.All(frame => frame.DurationMilliseconds == 100), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AssetFactory_WithoutSelectedSpritesCreatesEmptyAsset()
        {
            SpriteAnimationAsset asset =
                SpriteAnimationAssetFactory.Create(Array.Empty<UnityEngine.Object>());

            try
            {
                Assert.That(asset.Frames, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Generate_ReusesGuidPreservesLabelsAndMovesAfterSourceRename()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "Walk",
                false,
                (firstSprite, 200),
                (secondSprite, 100));
            SpriteAnimationGroupAsset group = CreateGroup(
                testRoot,
                "Character",
                testRoot,
                string.Empty,
                source);

            SpriteAnimationGenerationReport firstReport = SpriteAnimationClipGenerator.Generate(group);
            Assert.That(firstReport.Succeeded, Is.True, Messages(firstReport));
            Assert.That(firstReport.Created.Count, Is.EqualTo(1));

            string originalClipPath = testRoot + "/Walk.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(originalClipPath);
            Assert.That(clip, Is.Not.Null);
            string originalGuid = AssetDatabase.AssetPathToGUID(originalClipPath);
            string[] generatedLabels = AssetDatabase.GetLabels(clip);
            Assert.That(generatedLabels, Does.Contain(SpriteAnimationClipGenerator.GeneratedLabel));

            AssetDatabase.SetLabels(clip, generatedLabels.Concat(new[] { "User.Label" }).ToArray());
            SpriteAnimationGenerationReport updateReport = SpriteAnimationClipGenerator.Generate(group);
            Assert.That(updateReport.Succeeded, Is.True, Messages(updateReport));
            Assert.That(updateReport.Updated.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.GetLabels(clip), Does.Contain("User.Label"));
            Assert.That(AssetDatabase.AssetPathToGUID(originalClipPath), Is.EqualTo(originalGuid));

            string sourcePath = AssetDatabase.GetAssetPath(source);
            Assert.That(AssetDatabase.RenameAsset(sourcePath, "Run"), Is.Empty);
            AssetDatabase.SaveAssets();

            SpriteAnimationGenerationReport moveReport = SpriteAnimationClipGenerator.Generate(group);
            Assert.That(moveReport.Succeeded, Is.True, Messages(moveReport));
            Assert.That(moveReport.Moved.Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(originalClipPath), Is.Null);
            string movedClipPath = testRoot + "/Run.anim";
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(movedClipPath), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(movedClipPath), Is.EqualTo(originalGuid));
        }

        [Test]
        public void Generate_DoesNotOverwriteUnownedClip()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "Attack",
                false,
                (firstSprite, 100));
            SpriteAnimationGroupAsset group = CreateGroup(
                testRoot,
                "Character",
                testRoot,
                string.Empty,
                source);
            var manualClip = new AnimationClip { name = "Attack" };
            string clipPath = testRoot + "/Attack.anim";
            AssetDatabase.CreateAsset(manualClip, clipPath);

            SpriteAnimationGenerationReport report = SpriteAnimationClipGenerator.Generate(group);

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Results, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath), Is.SameAs(manualClip));
            Assert.That(AssetDatabase.GetLabels(manualClip), Does.Not.Contain(
                SpriteAnimationClipGenerator.GeneratedLabel));
        }

        [Test]
        public void Generate_WhenSelectedGroupsShareOutputPath_ChangesNothing()
        {
            AssetDatabase.CreateFolder(testRoot, "SourceA");
            AssetDatabase.CreateFolder(testRoot, "SourceB");
            SpriteAnimationAsset sourceA = CreateAnimation(
                testRoot + "/SourceA",
                "Idle",
                true,
                (firstSprite, 100));
            SpriteAnimationAsset sourceB = CreateAnimation(
                testRoot + "/SourceB",
                "Idle",
                true,
                (secondSprite, 100));
            SpriteAnimationGroupAsset groupA = CreateGroup(
                testRoot,
                "GroupA",
                testRoot,
                string.Empty,
                sourceA);
            SpriteAnimationGroupAsset groupB = CreateGroup(
                testRoot,
                "GroupB",
                testRoot,
                string.Empty,
                sourceB);

            SpriteAnimationGenerationReport report =
                SpriteAnimationClipGenerator.Generate(new[] { groupA, groupB });

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Results, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(testRoot + "/Idle.anim"), Is.Null);
        }

        private void CreateSprites()
        {
            var texture = new Texture2D(4, 2) { name = "SpriteTexture" };
            texture.SetPixels(Enumerable.Repeat(Color.white, 8).ToArray());
            texture.Apply();
            string texturePath = testRoot + "/SpriteTexture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);

            Sprite first = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            first.name = "Frame01";
            Sprite second = Sprite.Create(texture, new Rect(2, 0, 2, 2), new Vector2(0.5f, 0.5f));
            second.name = "Frame02";
            AssetDatabase.AddObjectToAsset(first, texture);
            AssetDatabase.AddObjectToAsset(second, texture);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
            firstSprite = sprites[0];
            secondSprite = sprites[1];
        }

        private static SpriteAnimationAsset CreateAnimation(
            string folder,
            string name,
            bool loop,
            params (Sprite sprite, int duration)[] frames)
        {
            var animation = ScriptableObject.CreateInstance<SpriteAnimationAsset>();
            animation.name = name;
            string path = folder + "/" + name + ".asset";
            AssetDatabase.CreateAsset(animation, path);

            var serialized = new SerializedObject(animation);
            serialized.FindProperty("loop").boolValue = loop;
            SerializedProperty framesProperty = serialized.FindProperty("frames");
            framesProperty.arraySize = frames.Length;
            for (var index = 0; index < frames.Length; index++)
            {
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                frame.FindPropertyRelative("sprite").objectReferenceValue = frames[index].sprite;
                frame.FindPropertyRelative("durationMilliseconds").intValue = frames[index].duration;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animation);
            AssetDatabase.SaveAssets();
            return animation;
        }

        private static SpriteAnimationGroupAsset CreateGroup(
            string folder,
            string name,
            string outputFolder,
            string bindingPath,
            params SpriteAnimationAsset[] animations)
        {
            var group = ScriptableObject.CreateInstance<SpriteAnimationGroupAsset>();
            group.name = name;
            AssetDatabase.CreateAsset(group, folder + "/" + name + ".asset");

            var serialized = new SerializedObject(group);
            SerializedProperty animationsProperty = serialized.FindProperty("animations");
            animationsProperty.arraySize = animations.Length;
            for (var index = 0; index < animations.Length; index++)
            {
                animationsProperty.GetArrayElementAtIndex(index).objectReferenceValue = animations[index];
            }

            serialized.FindProperty("outputFolder").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputFolder);
            serialized.FindProperty("bindingPath").stringValue = bindingPath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssets();
            return group;
        }

        private static string Messages(SpriteAnimationGenerationReport report)
        {
            return string.Join("\n", report.Messages.Select(message => message.Text));
        }
    }
}
