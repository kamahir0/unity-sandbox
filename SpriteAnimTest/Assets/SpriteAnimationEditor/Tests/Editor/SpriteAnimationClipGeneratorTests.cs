using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

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
                Assert.That(asset.Frames.All(frame => !frame.OverrideDuration), Is.True);
                Assert.That(asset.DefaultDurationMilliseconds, Is.EqualTo(100));
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
        public void Generate_SkipsInvalidAnimationAndGeneratesValidAnimation()
        {
            SpriteAnimationAsset valid = CreateAnimation(
                testRoot,
                "Idle",
                true,
                (firstSprite, 100));
            SpriteAnimationAsset invalid = CreateAnimation(
                testRoot,
                "Empty",
                false);
            SpriteAnimationGroupAsset group = CreateGroup(
                testRoot,
                "Character",
                testRoot,
                string.Empty,
                valid,
                invalid);

            SpriteAnimationGenerationReport report = SpriteAnimationClipGenerator.Generate(group);

            Assert.That(report.Succeeded, Is.True, Messages(report));
            Assert.That(report.Results, Has.Count.EqualTo(1));
            Assert.That(report.Messages.Count(message =>
                message.Severity == SpriteAnimationGenerationMessageSeverity.Warning), Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(testRoot + "/Idle.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(testRoot + "/Empty.anim"), Is.Null);
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

        [Test]
        public void ApplyToClip_MigratesLegacyDurationsWithoutChangingTiming()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "Legacy",
                false,
                (firstSprite, 125),
                (secondSprite, 250),
                (firstSprite, 125));
            var clip = new AnimationClip();

            try
            {
                SpriteAnimationClipGenerator.ApplyToClip(source, clip, string.Empty);

                Assert.That(source.DefaultDurationMilliseconds, Is.EqualTo(125));
                Assert.That(source.Frames.Select(frame => frame.OverrideDuration),
                    Is.EqualTo(new[] { false, true, false }));
                Assert.That(Enumerable.Range(0, source.Frames.Count)
                        .Select(source.GetDurationMilliseconds),
                    Is.EqualTo(new[] { 125, 250, 125 }));

                EditorCurveBinding binding =
                    AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(keys.Select(key => key.time), Is.EqualTo(new[]
                {
                    0f,
                    0.125f,
                    0.375f,
                    0.499f,
                }).Within(0.00001f));
                Assert.That(clip.length, Is.EqualTo(0.5f).Within(0.00001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ApplyToClip_UsesDefaultAndPerFrameOverrideDurations()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "Overrides",
                false,
                (firstSprite, 999),
                (secondSprite, 120));
            SetDurationData(source, 80, (false, 999), (true, 120));
            var clip = new AnimationClip();

            try
            {
                SpriteAnimationClipGenerator.ApplyToClip(source, clip, string.Empty);

                EditorCurveBinding binding =
                    AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(keys.Select(key => key.time), Is.EqualTo(new[]
                {
                    0f,
                    0.08f,
                    0.199f,
                }).Within(0.00001f));
                Assert.That(clip.length, Is.EqualTo(0.2f).Within(0.00001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Inspector_DirectDurationInputEnablesOverrideAndGroupsUndoRedo()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "Inspector",
                false,
                (firstSprite, 80),
                (secondSprite, 140));
            SetDurationData(source, 80, (false, 80), (true, 140));
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(source);

            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                ListView list = root.Q<ListView>();
                VisualElement firstRow = list.makeItem();
                list.bindItem(firstRow, 0);
                IntegerField durationField = firstRow.Q<IntegerField>("duration-field");
                TextElement durationText = durationField.Q<TextElement>();
                VisualElement durationTextInput = durationField.Q<VisualElement>(
                    TextInputBaseField<int>.textInputUssName);
                Assert.That(durationText, Is.Not.Null);
                Assert.That(durationField.textEdition.placeholder, Is.EqualTo("80"));
                Assert.That(durationField.textEdition.hidePlaceholderOnFocus, Is.True);
                Assert.That(durationTextInput.ClassListContains(
                    TextInputBaseField<int>.placeholderUssClassName), Is.True);

                MethodInfo setAllDurationOverrides = editor.GetType().GetMethod(
                    "SetAllDurationOverrides",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setAllDurationOverrides, Is.Not.Null);
                setAllDurationOverrides.Invoke(editor, new object[] { true });
                list.bindItem(firstRow, 0);
                Assert.That(durationTextInput.ClassListContains(
                    TextInputBaseField<int>.placeholderUssClassName), Is.False,
                    "Enabling all overrides must immediately restore normal text styling.");
                setAllDurationOverrides.Invoke(editor, new object[] { false });
                list.bindItem(firstRow, 0);
                Assert.That(durationTextInput.ClassListContains(
                    TextInputBaseField<int>.placeholderUssClassName), Is.True);

                MethodInfo handleInput = firstRow.GetType().GetMethod(
                    "HandleDurationTextInput",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handleInput, Is.Not.Null);
                MethodInfo endInput = firstRow.GetType().GetMethod(
                    "EndDurationTextInput",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(endInput, Is.Not.Null);

                Undo.IncrementCurrentGroup();
                handleInput.Invoke(firstRow, new object[] { "1" });
                handleInput.Invoke(firstRow, new object[] { "17" });
                handleInput.Invoke(firstRow, new object[] { "175" });
                endInput.Invoke(firstRow, Array.Empty<object>());

                Assert.That(source.Frames[0].OverrideDuration, Is.True,
                    "Typing into an inherited duration should enable its override.");
                Assert.That(source.Frames[0].DurationMilliseconds, Is.EqualTo(175));

                Undo.PerformUndo();
                Assert.That(source.Frames[0].OverrideDuration, Is.False);
                Assert.That(source.Frames[0].DurationMilliseconds, Is.EqualTo(80));

                Undo.PerformRedo();
                Assert.That(source.Frames[0].OverrideDuration, Is.True);
                Assert.That(source.Frames[0].DurationMilliseconds, Is.EqualTo(175));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                Undo.ClearUndo(source);
            }
        }

        [Test]
        public void Inspector_FrameCountFieldResizesFramesAndSupportsUndoRedo()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "InspectorFrameCount",
                false,
                (firstSprite, 80));
            SetDurationData(source, 80, (false, 80));
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(source);

            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                ListView list = root.Q<ListView>();
                TextField frameCountField = list.Q<TextField>(
                    BaseListView.arraySizeFieldUssClassName);
                Assert.That(frameCountField, Is.Not.Null);
                Assert.That(frameCountField.value, Is.EqualTo("1"));

                Undo.IncrementCurrentGroup();
                int resizeUndoGroup = Undo.GetCurrentGroup();
                MethodInfo resizeFrameList = editor.GetType().GetMethod(
                    "ResizeFrameList",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(resizeFrameList, Is.Not.Null);
                resizeFrameList.Invoke(editor, new object[] { 3 });
                Undo.CollapseUndoOperations(resizeUndoGroup);

                Assert.That(source.Frames, Has.Count.EqualTo(3));
                Assert.That(source.Frames[0].Sprite, Is.SameAs(firstSprite));
                for (var index = 1; index < 3; index++)
                {
                    Assert.That(source.Frames[index].Sprite, Is.Null);
                    Assert.That(source.Frames[index].OverrideDuration, Is.False);
                    Assert.That(source.Frames[index].DurationMilliseconds, Is.EqualTo(80));
                }

                Undo.PerformUndo();
                Assert.That(source.Frames, Has.Count.EqualTo(1));
                Assert.That(frameCountField.value, Is.EqualTo("1"));

                Undo.PerformRedo();
                Assert.That(source.Frames, Has.Count.EqualTo(3));
                Assert.That(frameCountField.value, Is.EqualTo("3"));

                Undo.IncrementCurrentGroup();
                resizeFrameList.Invoke(editor, new object[] { 1 });
                Assert.That(source.Frames, Has.Count.EqualTo(1));
                Assert.That(source.Frames[0].Sprite, Is.SameAs(firstSprite));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                Undo.ClearUndo(source);
            }
        }

        [Test]
        public void Inspector_DefaultDurationToggleShowsMixedStateAndSupportsUndoRedo()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "InspectorToggle",
                false,
                (firstSprite, 80),
                (secondSprite, 140));
            SetDurationData(source, 80, (false, 80), (true, 140));
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(source);

            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                VisualElement defaultDurationControl =
                    root.Q<VisualElement>("default-duration-control");
                Toggle defaultToggle = defaultDurationControl?.Q<Toggle>(
                    "duration-override-toggle");
                Assert.That(defaultToggle, Is.Not.Null);
                Assert.That(defaultToggle.showMixedValue, Is.True,
                    "The top toggle should show the mixed state.");
                Assert.That(defaultToggle.value, Is.False);

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                MethodInfo setAllOverrides = editor.GetType().GetMethod(
                    "SetAllDurationOverrides",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setAllOverrides, Is.Not.Null);
                setAllOverrides.Invoke(editor, new object[] { false });
                Undo.CollapseUndoOperations(undoGroup);
                Assert.That(source.Frames.All(frame => !frame.OverrideDuration), Is.True,
                    "Checking the top toggle should clear every override.");

                Undo.PerformUndo();
                Assert.That(source.Frames.Select(frame => frame.OverrideDuration),
                    Is.EqualTo(new[] { false, true }));

                Undo.PerformRedo();
                Assert.That(source.Frames.All(frame => !frame.OverrideDuration), Is.True,
                    "Redo should clear every override again.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
                Undo.ClearUndo(source);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(11)]
        [TestCase(100)]
        public void Inspector_FrameListUsesUnityStandardControls(int frameCount)
        {
            (Sprite sprite, int duration)[] frames = Enumerable.Range(0, frameCount)
                .Select(index => (index % 2 == 0 ? firstSprite : secondSprite, 100))
                .ToArray();
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                $"InspectorFrames{frameCount}",
                false,
                frames);
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(source);

            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                ListView list = root.Q<ListView>();
                Assert.That(list, Is.Not.Null);
                Assert.That(list.showFoldoutHeader, Is.True);
                Assert.That(list.showBorder, Is.True);
                Assert.That(list.showAddRemoveFooter, Is.True);
                Assert.That(list.showBoundCollectionSize, Is.False);
                Assert.That(list.showAlternatingRowBackgrounds,
                    Is.EqualTo(AlternatingRowBackground.None));
                Assert.That(list.reorderMode, Is.EqualTo(ListViewReorderMode.Animated));
                Assert.That(list.horizontalScrollingEnabled, Is.False);
                Assert.That(list.makeHeader, Is.Null,
                    "A custom header would replace Unity's native Foldout header.");

                Foldout foldout = list.Q<Foldout>();
                Assert.That(foldout, Is.Not.Null);
                Assert.That(foldout.text, Is.EqualTo($"Frames ({frameCount})"));
                TextField frameCountField = list.Q<TextField>(
                    BaseListView.arraySizeFieldUssClassName);
                Assert.That(frameCountField, Is.Not.Null);
                Assert.That(frameCountField.value, Is.EqualTo(frameCount.ToString()));
                Assert.That(frameCountField.ClassListContains(
                    BaseListView.arraySizeFieldUssClassName), Is.True);
                Assert.That(frameCountField.ClassListContains(
                    BaseListView.arraySizeFieldWithHeaderUssClassName), Is.True);
                Assert.That(frameCountField.ClassListContains(
                    BaseListView.arraySizeFieldWithFooterUssClassName), Is.True);
                Assert.That(list.Q<ToolbarMenu>("frame-list-options"), Is.Not.Null);
                Assert.That(list.Q<Button>("unity-list-view__add-button"), Is.Not.Null);
                Assert.That(list.Q<Button>("unity-list-view__remove-button"), Is.Not.Null);
                Assert.That(list.allowRemove, Is.EqualTo(frameCount > 0));
                Assert.That(root.Query<Button>().ToList()
                    .Any(button => button.text == "Duplicate"), Is.False);
                Assert.That(root.Query<Label>().ToList()
                    .Any(label => label.text.Contains("item selected")), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [UnityTest]
        public IEnumerator Inspector_AttachedLayoutUsesAlignedFieldsAndBoundedRows()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "InspectorLayout",
                false,
                Enumerable.Range(0, 100)
                    .Select(index => (
                        index % 2 == 0 ? firstSprite : secondSprite,
                        100))
                    .ToArray());
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(source);
            InspectorLayoutTestWindow window =
                ScriptableObject.CreateInstance<InspectorLayoutTestWindow>();

            try
            {
                var inspector = new InspectorElement(editor);
                inspector.style.flexGrow = 1f;
                window.rootVisualElement.Add(inspector);
                window.position = new Rect(100f, 100f, 729f, 900f);
                window.ShowUtility();
                yield return null;
                yield return null;

                ListView naturalHeightList = inspector.Q<ListView>();
                Assert.That(naturalHeightList.worldBound.height, Is.GreaterThan(4800f),
                    "A large frame list should use its natural height and the outer " +
                    "Inspector scroll view.");

                foreach (float width in new[] { 320f, 729f, 900f })
                {
                    Rect position = window.position;
                    position.width = width;
                    window.position = position;
                    yield return null;
                    yield return null;
                    AssertInspectorLayout(inspector, width);
                }

                ListView list = inspector.Q<ListView>();
                Foldout foldout = list.Q<Foldout>();
                foldout.value = false;
                yield return null;
                yield return null;

                VisualElement foldoutContent =
                    foldout.Q<VisualElement>("unity-content");
                Assert.That(foldoutContent.resolvedStyle.display,
                    Is.EqualTo(DisplayStyle.None));
                Assert.That(foldoutContent.worldBound.height, Is.EqualTo(0f).Within(0.5f));
                Assert.That(list.worldBound.height, Is.LessThanOrEqualTo(24.5f),
                    "Collapsing Frames should leave only the native Foldout header.");
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(window);
                if (editor != null)
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
        }

        [Test]
        public void Inspector_FrameListAddRemoveSortAndReorderPreserveFrameData()
        {
            SpriteAnimationAsset source = CreateAnimation(
                testRoot,
                "InspectorListOperations",
                false,
                (secondSprite, 120),
                (firstSprite, 80));
            SetDurationData(source, 80, (true, 120), (false, 80));
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(source);

            try
            {
                VisualElement root = editor.CreateInspectorGUI();
                ListView list = root.Q<ListView>();

                list.onAdd(list);
                Assert.That(source.Frames, Has.Count.EqualTo(3));
                Assert.That(source.Frames[2].Sprite, Is.Null);
                Assert.That(source.Frames[2].OverrideDuration, Is.False);
                Assert.That(source.Frames[2].DurationMilliseconds, Is.EqualTo(80));

                list.SetSelection(2);
                list.onRemove(list);
                Assert.That(source.Frames, Has.Count.EqualTo(2));

                MethodInfo sortFrames = editor.GetType().GetMethod(
                    "SortFramesByName",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(sortFrames, Is.Not.Null);
                sortFrames.Invoke(editor, Array.Empty<object>());
                Assert.That(source.Frames.Select(frame => frame.Sprite),
                    Is.EqualTo(new[] { firstSprite, secondSprite }));
                Assert.That(source.Frames.Select(frame => frame.OverrideDuration),
                    Is.EqualTo(new[] { false, true }));
                Assert.That(source.Frames.Select(frame => frame.DurationMilliseconds),
                    Is.EqualTo(new[] { 80, 120 }));

                MethodInfo moveFrame = editor.GetType().GetMethod(
                    "OnFrameMoved",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(moveFrame, Is.Not.Null);
                moveFrame.Invoke(editor, new object[] { 0, 1 });
                Assert.That(source.Frames.Select(frame => frame.Sprite),
                    Is.EqualTo(new[] { secondSprite, firstSprite }));
                Assert.That(source.Frames.Select(frame => frame.OverrideDuration),
                    Is.EqualTo(new[] { true, false }));
                Assert.That(source.Frames.Select(frame => frame.DurationMilliseconds),
                    Is.EqualTo(new[] { 120, 80 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        private static void AssertInspectorLayout(VisualElement inspector, float width)
        {
            const float tolerance = 1f;
            VisualElement inputClassRoot = inspector;
            PropertyField loopField = inspector.Q<PropertyField>("loop-field");
            VisualElement defaultDuration =
                inspector.Q<VisualElement>("default-duration-control");
            VisualElement loopInput = loopField.Q<VisualElement>(
                className: BaseField<bool>.inputUssClassName);
            VisualElement defaultInput = defaultDuration.Q<VisualElement>(
                className: BaseField<int>.inputUssClassName);
            Assert.That(Mathf.Abs(loopInput.worldBound.x - defaultInput.worldBound.x),
                Is.LessThanOrEqualTo(tolerance),
                $"Top-level field inputs are misaligned at {width}px.");

            ListView list = inputClassRoot.Q<ListView>();
            TextField frameCountField = list.Q<TextField>(
                BaseListView.arraySizeFieldUssClassName);
            ToolbarMenu frameOptionsMenu = list.Q<ToolbarMenu>("frame-list-options");
            Assert.That(frameOptionsMenu.worldBound.xMax,
                Is.LessThanOrEqualTo(frameCountField.worldBound.xMin + tolerance),
                $"Frame count and options overlap at {width}px.");
            Assert.That(frameOptionsMenu.resolvedStyle.borderLeftWidth, Is.Zero);
            Assert.That(frameOptionsMenu.resolvedStyle.borderRightWidth, Is.Zero);
            Assert.That(frameOptionsMenu.resolvedStyle.borderTopWidth, Is.Zero);
            Assert.That(frameOptionsMenu.resolvedStyle.borderBottomWidth, Is.Zero);
            Assert.That(frameOptionsMenu.resolvedStyle.backgroundColor.a, Is.Zero);
            Assert.That(frameCountField.worldBound.xMax,
                Is.LessThanOrEqualTo(list.worldBound.xMax + tolerance));

            VisualElement row = list.Query<VisualElement>(
                className: "sprite-animation-frame-row").First();
            ObjectField spriteField = row.Q<ObjectField>("sprite-field");
            VisualElement durationControl = row.Q<VisualElement>("duration-control");
            VisualElement spriteInput = spriteField.Q<VisualElement>(
                className: BaseField<UnityEngine.Object>.inputUssClassName);
            VisualElement durationInput = durationControl.Q<VisualElement>(
                className: BaseField<int>.inputUssClassName);
            Assert.That(Mathf.Abs(spriteInput.worldBound.x - durationInput.worldBound.x),
                Is.LessThanOrEqualTo(tolerance),
                $"Frame field inputs are misaligned at {width}px.");

            VisualElement[] boundedFields =
            {
                spriteField,
                durationControl,
                row.Q<Toggle>("duration-override-toggle"),
                row.Q<IntegerField>("duration-field"),
            };
            foreach (VisualElement field in boundedFields)
            {
                Assert.That(field.worldBound.yMin,
                    Is.GreaterThanOrEqualTo(row.worldBound.yMin - tolerance));
                Assert.That(field.worldBound.yMax,
                    Is.LessThanOrEqualTo(row.worldBound.yMax + tolerance));
                Assert.That(field.worldBound.xMax,
                    Is.LessThanOrEqualTo(row.worldBound.xMax + tolerance));
            }

            VisualElement listItem = row.parent.parent.parent;
            VisualElement[] handleBars = listItem.Query<VisualElement>(
                className: "unity-list-view__reorderable-handle-bar").ToList().ToArray();
            Assert.That(handleBars, Has.Length.EqualTo(2));
            float handleCenter = (handleBars[0].worldBound.center.y +
                                  handleBars[1].worldBound.center.y) / 2f;
            Assert.That(Mathf.Abs(handleCenter - listItem.worldBound.center.y),
                Is.LessThanOrEqualTo(tolerance));
            Assert.That(list.horizontalScrollingEnabled, Is.False);
        }

        private sealed class InspectorLayoutTestWindow : EditorWindow
        {
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

        private static void SetDurationData(
            SpriteAnimationAsset animation,
            int defaultDuration,
            params (bool overrideDuration, int duration)[] frames)
        {
            var serialized = new SerializedObject(animation);
            serialized.FindProperty("serializationVersion").intValue = 1;
            serialized.FindProperty("defaultDurationMilliseconds").intValue = defaultDuration;
            SerializedProperty framesProperty = serialized.FindProperty("frames");
            Assert.That(framesProperty.arraySize, Is.EqualTo(frames.Length));
            for (var index = 0; index < frames.Length; index++)
            {
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                frame.FindPropertyRelative("overrideDuration").boolValue =
                    frames[index].overrideDuration;
                frame.FindPropertyRelative("durationMilliseconds").intValue =
                    frames[index].duration;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(animation);
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
