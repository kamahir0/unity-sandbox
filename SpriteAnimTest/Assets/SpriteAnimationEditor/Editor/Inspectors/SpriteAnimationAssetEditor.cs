using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpriteAnimationEditor
{
    [CustomEditor(typeof(SpriteAnimationAsset))]
    public sealed class SpriteAnimationAssetEditor : UnityEditor.Editor
    {
        private readonly List<int> frameIndices = new List<int>();

        private SerializedProperty loopProperty;
        private SerializedProperty framesProperty;
        private ListView frameList;
        private Button setDurationButton;
        private IntegerField batchDurationField;
        private Image previewImage;
        private Button previousFrameButton;
        private Button playPauseButton;
        private Button nextFrameButton;
        private SliderInt previewSlider;
        private Label previewStatus;
        private IVisualElementScheduledItem previewSchedule;
        private bool isRebuildingFrames;
        private bool isPlaying;
        private double lastPreviewTime;
        private double previewMilliseconds;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            loopProperty = serializedObject.FindProperty("loop");
            framesProperty = serializedObject.FindProperty("frames");

            VisualElement root = SpriteAnimationUiResources.Clone(
                "SpriteAnimationAssetEditor.uxml",
                "SpriteAnimationEditor.uss");
            VisualElement fields = root.Q<VisualElement>("asset-fields");
            var loopField = new PropertyField(loopProperty);
            loopField.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnFrameValueChanged());
            fields.Add(loopField);

            BuildFrameToolbar(root.Q<VisualElement>("frame-toolbar"));
            BuildFrameList(root.Q<VisualElement>("frame-list-container"));

            root.Bind(serializedObject);
            RebuildFrameList();
            RefreshPreview();
            return root;
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            return EditorGUIUtility.TrTextContent("Preview");
        }

        public override VisualElement CreatePreview(VisualElement inspectorPreviewWindow)
        {
            VisualElement previewPane = inspectorPreviewWindow.Q("content-container");
            VisualElement toolbar = inspectorPreviewWindow.Q("toolbar");
            if (previewPane == null || toolbar == null)
            {
                return null;
            }

            previousFrameButton = CreatePreviewToolbarButton(
                "sprite-animation-previous-frame",
                "Animation.PrevKey",
                "Previous Frame",
                PreviousFrame);
            playPauseButton = CreatePreviewToolbarButton(
                "sprite-animation-play-pause",
                "PlayButton",
                "Play",
                TogglePlayback);
            nextFrameButton = CreatePreviewToolbarButton(
                "sprite-animation-next-frame",
                "Animation.NextKey",
                "Next Frame",
                NextFrame);
            toolbar.Add(previousFrameButton);
            toolbar.Add(playPauseButton);
            toolbar.Add(nextFrameButton);

            var root = new VisualElement
            {
                name = "sprite-animation-preview",
                focusable = true,
            };
            root.AddToClassList("sprite-animation-standard-preview");
            SpriteAnimationUiResources.AddStyleSheet(root, "SpriteAnimationEditor.uss");

            previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
            };
            previewImage.AddToClassList("sprite-animation-preview-image");
            root.Add(previewImage);

            var footer = new VisualElement();
            footer.AddToClassList("sprite-animation-preview-footer");

            previewSlider = new SliderInt(0, 0);
            previewSlider.AddToClassList("sprite-animation-preview-slider");
            previewSlider.RegisterCallback<PointerDownEvent>(_ => SetPlaying(false));
            previewSlider.RegisterValueChangedCallback(evt =>
            {
                previewMilliseconds = evt.newValue;
                RefreshPreviewImage();
            });
            footer.Add(previewSlider);

            previewStatus = new Label();
            previewStatus.AddToClassList("sprite-animation-preview-status");
            footer.Add(previewStatus);
            root.Add(footer);
            previewPane.Add(root);

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                previewSchedule?.Pause();
                previewSchedule = root.schedule.Execute(UpdatePlayback).Every(16);
                RefreshPreview();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                SetPlaying(false);
                previewSchedule?.Pause();
                previewSchedule = null;
            });
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Space)
                {
                    return;
                }

                TogglePlayback();
                evt.StopPropagation();
            });

            RefreshPreview();
            return inspectorPreviewWindow;
        }

        private static Button CreatePreviewToolbarButton(
            string name,
            string iconName,
            string tooltip,
            Action clicked)
        {
            var button = new Button(clicked)
            {
                name = name,
                tooltip = tooltip,
            };
            button.style.backgroundImage =
                EditorGUIUtility.IconContent(iconName).image as Texture2D;
            return button;
        }

        private void BuildFrameToolbar(VisualElement toolbar)
        {
            var duplicateButton = new Button(DuplicateSelectedFrames) { text = "Duplicate" };
            var removeButton = new Button(RemoveSelectedFrames) { text = "Remove" };
            var sortButton = new Button(SortFramesByName) { text = "Sort by Name" };
            batchDurationField = new IntegerField { value = 100, isDelayed = true };
            batchDurationField.label = "Duration (ms)";
            batchDurationField.style.width = 150f;
            setDurationButton = new Button(SetSelectedDuration) { text = "Set" };
            setDurationButton.SetEnabled(false);

            toolbar.Add(duplicateButton);
            toolbar.Add(removeButton);
            toolbar.Add(sortButton);
            toolbar.Add(new VisualElement { style = { flexGrow = 1f } });
            toolbar.Add(batchDurationField);
            toolbar.Add(setDurationButton);
        }

        private void BuildFrameList(VisualElement container)
        {
            frameList = new ListView
            {
                fixedItemHeight = 42f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Multiple,
                reorderable = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                itemsSource = frameIndices,
                makeItem = () => new FrameRow(OnFrameValueChanged),
                bindItem = (element, index) =>
                {
                    serializedObject.UpdateIfRequiredOrScript();
                    int frameIndex = frameIndices[index];
                    SerializedProperty frame = framesProperty.GetArrayElementAtIndex(frameIndex);
                    ((FrameRow)element).Bind(frame);
                },
                unbindItem = (element, _) => ((FrameRow)element).Unbind(),
            };

            frameList.itemIndexChanged += OnFrameMoved;
            frameList.selectedIndicesChanged += _ =>
                setDurationButton.SetEnabled(frameList.selectedIndices.Any());
            frameList.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            frameList.RegisterCallback<DragPerformEvent>(OnDragPerform);
            container.Add(frameList);
        }

        private void RebuildFrameList()
        {
            if (frameList == null)
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            isRebuildingFrames = true;
            frameIndices.Clear();
            for (var index = 0; index < framesProperty.arraySize; index++)
            {
                frameIndices.Add(index);
            }

            frameList.Rebuild();
            isRebuildingFrames = false;
            setDurationButton.SetEnabled(false);
        }

        private void OnFrameMoved(int sourceIndex, int destinationIndex)
        {
            if (isRebuildingFrames || sourceIndex == destinationIndex)
            {
                return;
            }

            serializedObject.Update();
            framesProperty.MoveArrayElement(sourceIndex, destinationIndex);
            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void OnDragUpdated(DragUpdatedEvent dragEvent)
        {
            if (SpriteAnimationSpriteCollector.Collect(DragAndDrop.objectReferences).Count == 0)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            dragEvent.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent dragEvent)
        {
            List<Sprite> sprites =
                SpriteAnimationSpriteCollector.Collect(DragAndDrop.objectReferences);
            if (sprites.Count == 0)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            AddSprites(sprites);
            dragEvent.StopPropagation();
        }

        private void AddSprites(IReadOnlyList<Sprite> sprites)
        {
            if (sprites == null || sprites.Count == 0)
            {
                return;
            }

            serializedObject.Update();
            foreach (Sprite sprite in sprites)
            {
                int index = framesProperty.arraySize;
                framesProperty.InsertArrayElementAtIndex(index);
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                frame.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                frame.FindPropertyRelative("durationMilliseconds").intValue = 100;
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void DuplicateSelectedFrames()
        {
            int[] selected = frameList.selectedIndices.OrderByDescending(index => index).ToArray();
            if (selected.Length == 0)
            {
                return;
            }

            serializedObject.Update();
            foreach (int index in selected)
            {
                SerializedProperty sourceFrame = framesProperty.GetArrayElementAtIndex(index);
                UnityEngine.Object sprite = sourceFrame.FindPropertyRelative("sprite").objectReferenceValue;
                int duration = sourceFrame.FindPropertyRelative("durationMilliseconds").intValue;
                framesProperty.InsertArrayElementAtIndex(index + 1);
                SerializedProperty duplicate = framesProperty.GetArrayElementAtIndex(index + 1);
                duplicate.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                duplicate.FindPropertyRelative("durationMilliseconds").intValue = duration;
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void RemoveSelectedFrames()
        {
            int[] selected = frameList.selectedIndices.OrderByDescending(index => index).ToArray();
            if (selected.Length == 0)
            {
                return;
            }

            serializedObject.Update();
            foreach (int index in selected)
            {
                framesProperty.DeleteArrayElementAtIndex(index);
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void SortFramesByName()
        {
            serializedObject.Update();
            var values = new List<FrameValue>();
            for (var index = 0; index < framesProperty.arraySize; index++)
            {
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                values.Add(new FrameValue(
                    frame.FindPropertyRelative("sprite").objectReferenceValue as Sprite,
                    frame.FindPropertyRelative("durationMilliseconds").intValue,
                    index));
            }

            FrameValue[] sorted = values
                .OrderBy(value => value.Sprite != null ? value.Sprite.name : string.Empty,
                    NaturalStringComparer.Instance)
                .ThenBy(value => value.OriginalIndex)
                .ToArray();
            for (var index = 0; index < sorted.Length; index++)
            {
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                frame.FindPropertyRelative("sprite").objectReferenceValue = sorted[index].Sprite;
                frame.FindPropertyRelative("durationMilliseconds").intValue = sorted[index].Duration;
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void SetSelectedDuration()
        {
            int duration = Mathf.Max(1, batchDurationField.value);
            int[] selected = frameList.selectedIndices.ToArray();
            if (selected.Length == 0)
            {
                return;
            }

            batchDurationField.SetValueWithoutNotify(duration);
            serializedObject.Update();
            foreach (int index in selected)
            {
                framesProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("durationMilliseconds").intValue = duration;
            }

            serializedObject.ApplyModifiedProperties();
            frameList.RefreshItems();
            OnFrameValueChanged();
        }

        private void OnFrameValueChanged()
        {
            RefreshPreview();
        }

        private void TogglePlayback()
        {
            SpriteAnimationAsset asset = (SpriteAnimationAsset)target;
            if (asset.Frames == null || asset.Frames.Count == 0 || TotalMilliseconds(asset) <= 0)
            {
                return;
            }

            if (!isPlaying && !asset.Loop &&
                previewMilliseconds >= TotalMilliseconds(asset) - 1)
            {
                previewMilliseconds = 0;
            }

            SetPlaying(!isPlaying);
        }

        private void SetPlaying(bool value)
        {
            if (isPlaying == value)
            {
                return;
            }

            isPlaying = value;
            lastPreviewTime = EditorApplication.timeSinceStartup;
            UpdatePlayPauseButton();
            Repaint();
        }

        private void UpdatePlayPauseButton()
        {
            if (playPauseButton == null)
            {
                return;
            }

            string iconName = isPlaying ? "PauseButton" : "PlayButton";
            playPauseButton.style.backgroundImage =
                EditorGUIUtility.IconContent(iconName).image as Texture2D;
            playPauseButton.tooltip = isPlaying ? "Pause" : "Play";
        }

        private void UpdatePlayback()
        {
            if (!isPlaying)
            {
                return;
            }

            SpriteAnimationAsset asset = (SpriteAnimationAsset)target;
            long totalMilliseconds = TotalMilliseconds(asset);
            if (totalMilliseconds <= 0)
            {
                SetPlaying(false);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            previewMilliseconds += (now - lastPreviewTime) * 1000d;
            lastPreviewTime = now;

            if (asset.Loop)
            {
                previewMilliseconds %= totalMilliseconds;
            }
            else if (previewMilliseconds >= totalMilliseconds)
            {
                previewMilliseconds = totalMilliseconds - 1;
                SetPlaying(false);
            }

            RefreshPreviewImage();
        }

        private void PreviousFrame()
        {
            SetPlaying(false);
            SpriteAnimationAsset asset = (SpriteAnimationAsset)target;
            int current = FrameIndexAtMilliseconds(asset, previewMilliseconds);
            if (current < 0)
            {
                return;
            }

            int previous = current - 1;
            if (previous < 0)
            {
                previous = asset.Loop ? asset.Frames.Count - 1 : 0;
            }

            previewMilliseconds = StartMillisecondsOfFrame(asset, previous);
            RefreshPreviewImage();
        }

        private void NextFrame()
        {
            SetPlaying(false);
            SpriteAnimationAsset asset = (SpriteAnimationAsset)target;
            int current = FrameIndexAtMilliseconds(asset, previewMilliseconds);
            if (current < 0)
            {
                return;
            }

            int next = current + 1;
            if (next >= asset.Frames.Count)
            {
                next = asset.Loop ? 0 : asset.Frames.Count - 1;
            }

            previewMilliseconds = StartMillisecondsOfFrame(asset, next);
            RefreshPreviewImage();
        }

        private void RefreshPreview()
        {
            if (previewSlider == null)
            {
                return;
            }

            SpriteAnimationAsset asset = (SpriteAnimationAsset)target;
            long totalMilliseconds = TotalMilliseconds(asset);
            int highValue = totalMilliseconds > 0
                ? (int)Math.Min(totalMilliseconds - 1, int.MaxValue)
                : 0;
            previewSlider.lowValue = 0;
            previewSlider.highValue = highValue;
            previewMilliseconds = Math.Max(0d, Math.Min(previewMilliseconds, highValue));
            RefreshPreviewImage();
        }

        private void RefreshPreviewImage()
        {
            SpriteAnimationAsset asset = (SpriteAnimationAsset)target;
            int frameIndex = FrameIndexAtMilliseconds(asset, previewMilliseconds);
            long totalMilliseconds = TotalMilliseconds(asset);
            if (frameIndex < 0)
            {
                previewImage.sprite = null;
                previewStatus.text = "No frames";
                previewSlider.SetValueWithoutNotify(0);
                previewSlider.SetEnabled(false);
                SetPreviewControlsEnabled(false);
                return;
            }

            previewImage.sprite = asset.Frames[frameIndex].Sprite;
            previewSlider.SetEnabled(true);
            SetPreviewControlsEnabled(true);
            int sliderMilliseconds = (int)Math.Min(Math.Floor(previewMilliseconds), int.MaxValue);
            previewSlider.SetValueWithoutNotify(sliderMilliseconds);
            previewStatus.text =
                $"Frame {frameIndex + 1}/{asset.Frames.Count}   " +
                $"{sliderMilliseconds}/{totalMilliseconds} ms";
        }

        private void SetPreviewControlsEnabled(bool enabled)
        {
            previousFrameButton?.SetEnabled(enabled);
            playPauseButton?.SetEnabled(enabled);
            nextFrameButton?.SetEnabled(enabled);
        }

        private static long TotalMilliseconds(SpriteAnimationAsset asset)
        {
            if (asset?.Frames == null)
            {
                return 0;
            }

            long total = 0;
            foreach (SpriteAnimationFrame frame in asset.Frames)
            {
                if (frame != null && frame.DurationMilliseconds > 0)
                {
                    total += frame.DurationMilliseconds;
                }
            }

            return total;
        }

        private static int FrameIndexAtMilliseconds(
            SpriteAnimationAsset asset,
            double milliseconds)
        {
            if (asset?.Frames == null || asset.Frames.Count == 0)
            {
                return -1;
            }

            long start = 0;
            for (var index = 0; index < asset.Frames.Count; index++)
            {
                SpriteAnimationFrame frame = asset.Frames[index];
                int duration = frame != null ? Math.Max(frame.DurationMilliseconds, 1) : 1;
                if (milliseconds < start + duration)
                {
                    return index;
                }

                start += duration;
            }

            return asset.Frames.Count - 1;
        }

        private static long StartMillisecondsOfFrame(SpriteAnimationAsset asset, int frameIndex)
        {
            long milliseconds = 0;
            for (var index = 0; index < frameIndex; index++)
            {
                SpriteAnimationFrame frame = asset.Frames[index];
                milliseconds += frame != null ? Math.Max(frame.DurationMilliseconds, 1) : 1;
            }

            return milliseconds;
        }

        private readonly struct FrameValue
        {
            public FrameValue(Sprite sprite, int duration, int originalIndex)
            {
                Sprite = sprite;
                Duration = duration;
                OriginalIndex = originalIndex;
            }

            public Sprite Sprite { get; }

            public int Duration { get; }

            public int OriginalIndex { get; }
        }

        private sealed class FrameRow : VisualElement
        {
            private readonly Image thumbnail;
            private readonly ObjectField spriteField;
            private readonly IntegerField durationField;
            private readonly Action onValueChanged;

            public FrameRow(Action onValueChanged)
            {
                this.onValueChanged = onValueChanged;
                AddToClassList("sprite-animation-frame-row");

                thumbnail = new Image { scaleMode = ScaleMode.ScaleToFit };
                thumbnail.AddToClassList("sprite-animation-frame-thumbnail");
                Add(thumbnail);

                spriteField = new ObjectField
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false,
                };
                spriteField.AddToClassList("sprite-animation-frame-sprite-field");
                spriteField.RegisterValueChangedCallback(evt =>
                {
                    thumbnail.sprite = evt.newValue as Sprite;
                    this.onValueChanged();
                });
                Add(spriteField);

                durationField = new IntegerField
                {
                    isDelayed = true,
                    tooltip = "Duration (ms)",
                };
                durationField.AddToClassList("sprite-animation-frame-duration-field");
                durationField.RegisterValueChangedCallback(_ => this.onValueChanged());
                Add(durationField);
            }

            public void Bind(SerializedProperty frame)
            {
                spriteField.Unbind();
                durationField.Unbind();
                SerializedProperty sprite = frame.FindPropertyRelative("sprite");
                SerializedProperty duration = frame.FindPropertyRelative("durationMilliseconds");
                thumbnail.sprite = sprite.objectReferenceValue as Sprite;
                spriteField.BindProperty(sprite);
                durationField.BindProperty(duration);
            }

            public void Unbind()
            {
                spriteField.Unbind();
                durationField.Unbind();
                thumbnail.sprite = null;
            }
        }
    }
}
