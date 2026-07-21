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
        private SerializedProperty defaultDurationProperty;
        private SerializedProperty framesProperty;
        private ListView frameList;
        private TextField frameCountField;
        private ToolbarMenu frameOptionsMenu;
        private Foldout frameFoldout;
        private DurationOverrideField defaultDurationControl;
        private Toggle defaultDurationToggle;
        private IntegerField defaultDurationField;
        private Image previewImage;
        private Button previousFrameButton;
        private Button playPauseButton;
        private Button nextFrameButton;
        private SliderInt previewSlider;
        private Label previewStatus;
        private IVisualElementScheduledItem previewSchedule;
        private bool isRebuildingFrames;
        private bool isRefreshingFrameCountField;
        private bool isRefreshingDurationControls;
        private int defaultDurationEditUndoGroup = -1;
        private bool isPlaying;
        private double lastPreviewTime;
        private double previewMilliseconds;

        private void OnEnable()
        {
            SpriteAnimationAssetMigration.EnsureUpToDate(target as SpriteAnimationAsset);
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SetPlaying(false);
        }

        public override VisualElement CreateInspectorGUI()
        {
            SpriteAnimationAssetMigration.EnsureUpToDate(target as SpriteAnimationAsset);
            serializedObject.Update();
            loopProperty = serializedObject.FindProperty("loop");
            defaultDurationProperty = serializedObject.FindProperty("defaultDurationMilliseconds");
            framesProperty = serializedObject.FindProperty("frames");

            VisualElement root = SpriteAnimationUiResources.Clone(
                "SpriteAnimationAssetEditor.uxml",
                "SpriteAnimationEditor.uss");
            PropertyField loopField = root.Q<PropertyField>("loop-field");
            if (loopField == null)
            {
                loopField = new PropertyField(loopProperty) { name = "loop-field" };
                root.Insert(0, loopField);
            }

            loopField.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnFrameValueChanged());

            BuildDefaultDurationControls(root);
            BuildFrameList(root.Q<VisualElement>("frame-list-container"));

            root.Bind(serializedObject);
            RebuildFrameList();
            RefreshDefaultDurationControls();
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

            // Unity's standard preview content container does not grow by default
            // for a custom UI Toolkit preview. Let it consume all space below the
            // header so the preview scales with the Inspector split view.
            previewPane.style.flexGrow = 1f;
            previewPane.style.minHeight = 0f;

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

        private void BuildDefaultDurationControls(VisualElement root)
        {
            defaultDurationControl =
                root.Q<DurationOverrideField>("default-duration-control");
            if (defaultDurationControl == null)
            {
                defaultDurationControl = new DurationOverrideField("Default Duration (ms)")
                {
                    name = "default-duration-control",
                };
                root.Insert(Math.Min(1, root.childCount), defaultDurationControl);
            }

            defaultDurationToggle = defaultDurationControl.OverrideToggle;
            defaultDurationField = defaultDurationControl.IntegerField;
            defaultDurationToggle.tooltip = "Use the default duration for every frame";
            defaultDurationField.tooltip =
                "Duration inherited by frames without an override";
            defaultDurationField.isDelayed = false;
            defaultDurationField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (defaultDurationEditUndoGroup >= 0)
                {
                    return;
                }

                defaultDurationEditUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Change Default Duration");
            });
            defaultDurationField.RegisterCallback<FocusOutEvent>(_ =>
                defaultDurationEditUndoGroup = -1);
            defaultDurationToggle.RegisterValueChangedCallback(evt =>
            {
                if (!isRefreshingDurationControls)
                {
                    SetAllDurationOverrides(!evt.newValue);
                }
            });
            defaultDurationField.RegisterValueChangedCallback(evt =>
            {
                if (isRefreshingDurationControls)
                {
                    return;
                }

                int duration = Mathf.Max(1, evt.newValue);
                if (duration != evt.newValue)
                {
                    defaultDurationField.SetValueWithoutNotify(duration);
                }

                serializedObject.Update();
                defaultDurationProperty.intValue = duration;
                serializedObject.ApplyModifiedProperties();
                if (defaultDurationEditUndoGroup >= 0)
                {
                    Undo.CollapseUndoOperations(defaultDurationEditUndoGroup);
                }

                frameList?.RefreshItems();
                OnFrameValueChanged();
            });
        }

        private ToolbarMenu CreateFrameListOptionsMenu()
        {
            frameOptionsMenu = new ToolbarMenu
            {
                name = "frame-list-options",
                tooltip = "Frame list options",
            };
            frameOptionsMenu.AddToClassList(
                "sprite-animation-frame-list-header__menu");
            frameOptionsMenu.style.backgroundImage =
                EditorGUIUtility.IconContent("_Menu").image as Texture2D;
            frameOptionsMenu.menu.AppendAction(
                "Sort by Sprite Name",
                _ => SortFramesByName(),
                _ => framesProperty != null && framesProperty.arraySize > 1
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            frameOptionsMenu.RegisterCallback<PointerDownEvent>(evt =>
                evt.StopPropagation());
            frameOptionsMenu.RegisterCallback<ClickEvent>(evt =>
                evt.StopPropagation());
            return frameOptionsMenu;
        }

        private void AddFrameCountField()
        {
            // Match the size field used by Unity's serialized array ListView while
            // keeping this list's custom SerializedProperty update behavior.
            frameCountField = new TextField
            {
                name = BaseListView.arraySizeFieldUssClassName,
                isDelayed = true,
            };
            frameCountField.AddToClassList(BaseListView.arraySizeFieldUssClassName);
            frameCountField.AddToClassList(
                BaseListView.arraySizeFieldWithFooterUssClassName);
            frameCountField.AddToClassList(
                BaseListView.arraySizeFieldWithHeaderUssClassName);
            frameCountField.RegisterValueChangedCallback(evt =>
            {
                if (isRefreshingFrameCountField)
                {
                    return;
                }

                if (!int.TryParse(evt.newValue, out int frameCount))
                {
                    RefreshFrameCountField();
                    return;
                }

                ResizeFrameList(Mathf.Max(0, frameCount));
            });
            frameList.hierarchy.Add(frameCountField);
        }

        private void RefreshDefaultDurationControls()
        {
            if (defaultDurationToggle == null || defaultDurationField == null ||
                framesProperty == null || defaultDurationProperty == null)
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            int frameCount = framesProperty.arraySize;
            int overrideCount = 0;
            for (var index = 0; index < frameCount; index++)
            {
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                if (frame.FindPropertyRelative("overrideDuration").boolValue)
                {
                    overrideCount++;
                }
            }

            isRefreshingDurationControls = true;
            bool allUseDefault = overrideCount == 0;
            bool mixed = overrideCount > 0 && overrideCount < frameCount;
            defaultDurationToggle.showMixedValue = mixed;
            defaultDurationToggle.SetValueWithoutNotify(allUseDefault);
            defaultDurationField.SetValueWithoutNotify(
                Mathf.Max(1, defaultDurationProperty.intValue));
            defaultDurationField.SetEnabled(frameCount == 0 || overrideCount < frameCount);
            isRefreshingDurationControls = false;
        }

        private void SetAllDurationOverrides(bool enableOverrides)
        {
            serializedObject.Update();
            int defaultDuration = Mathf.Max(1, defaultDurationProperty.intValue);
            for (var index = 0; index < framesProperty.arraySize; index++)
            {
                SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
                SerializedProperty overrideProperty =
                    frame.FindPropertyRelative("overrideDuration");
                if (enableOverrides && !overrideProperty.boolValue)
                {
                    frame.FindPropertyRelative("durationMilliseconds").intValue =
                        defaultDuration;
                }

                overrideProperty.boolValue = enableOverrides;
            }

            serializedObject.ApplyModifiedProperties();
            frameList?.RefreshItems();
            RefreshDefaultDurationControls();
            OnFrameValueChanged();
        }

        private void SetFrameDurationOverride(
            int frameIndex,
            bool overrideDuration,
            int durationMilliseconds)
        {
            if (frameIndex < 0 || frameIndex >= framesProperty.arraySize)
            {
                return;
            }

            serializedObject.Update();
            SerializedProperty frame = framesProperty.GetArrayElementAtIndex(frameIndex);
            frame.FindPropertyRelative("overrideDuration").boolValue = overrideDuration;
            if (overrideDuration)
            {
                frame.FindPropertyRelative("durationMilliseconds").intValue =
                    Mathf.Max(1, durationMilliseconds);
            }

            serializedObject.ApplyModifiedProperties();
            RefreshDefaultDurationControls();
            OnFrameValueChanged();
        }

        private void BuildFrameList(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            frameList = new ListView
            {
                fixedItemHeight = 48f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Multiple,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showAddRemoveFooter = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                showBoundCollectionSize = false,
                showBorder = true,
                horizontalScrollingEnabled = false,
                itemsSource = frameIndices,
                makeItem = () => new FrameRow(
                    SetFrameDurationOverride,
                    OnFrameValueChanged),
                bindItem = (element, index) =>
                {
                    serializedObject.UpdateIfRequiredOrScript();
                    int frameIndex = frameIndices[index];
                    SerializedProperty frame = framesProperty.GetArrayElementAtIndex(frameIndex);
                    ((FrameRow)element).Bind(
                        frame,
                        defaultDurationProperty,
                        frameIndex);
                },
                unbindItem = (element, _) => ((FrameRow)element).Unbind(),
            };
            frameList.headerTitle = "Frames";
            frameList.showFoldoutHeader = true;

            frameList.itemIndexChanged += OnFrameMoved;
            frameList.onAdd = _ => AddEmptyFrame();
            frameList.onRemove = _ => RemoveSelectedFrames();
            frameList.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            frameList.RegisterCallback<DragPerformEvent>(OnDragPerform);
            frameList.AddToClassList("sprite-animation-frame-list");
            container.Add(frameList);
            AddFrameCountField();

            frameFoldout = frameList.Q<Foldout>();
            if (frameFoldout != null)
            {
                Toggle headerToggle = frameFoldout.Q<Toggle>();
                VisualElement headerInput = headerToggle?.Q<VisualElement>(
                    className: Toggle.inputUssClassName);
                headerInput?.Add(CreateFrameListOptionsMenu());

                bool expanded = SessionState.GetBool(FrameFoldoutSessionKey(), true);
                framesProperty.isExpanded = expanded;
                frameFoldout.SetValueWithoutNotify(expanded);
                frameFoldout.RegisterValueChangedCallback(evt =>
                {
                    framesProperty.isExpanded = evt.newValue;
                    SessionState.SetBool(FrameFoldoutSessionKey(), evt.newValue);
                });
            }
        }

        private void ResizeFrameList(int frameCount)
        {
            serializedObject.Update();
            int previousCount = framesProperty.arraySize;
            if (frameCount == previousCount)
            {
                RefreshFrameCountField();
                return;
            }

            Undo.SetCurrentGroupName("Resize Sprite Animation Frames");
            framesProperty.arraySize = frameCount;
            if (frameCount > previousCount)
            {
                int defaultDuration = Mathf.Max(1, defaultDurationProperty.intValue);
                for (int index = previousCount; index < frameCount; index++)
                {
                    SerializedProperty frame =
                        framesProperty.GetArrayElementAtIndex(index);
                    frame.FindPropertyRelative("sprite").objectReferenceValue = null;
                    frame.FindPropertyRelative("overrideDuration").boolValue = false;
                    frame.FindPropertyRelative("durationMilliseconds").intValue =
                        defaultDuration;
                }
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private string FrameFoldoutSessionKey()
        {
            GlobalObjectId objectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return $"SpriteAnimationEditor.FramesExpanded.{objectId}";
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
            if (frameFoldout != null)
            {
                frameFoldout.text = $"Frames ({framesProperty.arraySize})";
            }

            RefreshFrameCountField();
            frameOptionsMenu?.SetEnabled(framesProperty.arraySize > 1);
            frameList.allowRemove = framesProperty.arraySize > 0;
            RefreshDefaultDurationControls();
        }

        private void RefreshFrameCountField()
        {
            if (frameCountField == null || framesProperty == null)
            {
                return;
            }

            isRefreshingFrameCountField = true;
            frameCountField.SetValueWithoutNotify(framesProperty.arraySize.ToString());
            isRefreshingFrameCountField = false;
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
                frame.FindPropertyRelative("overrideDuration").boolValue = false;
                frame.FindPropertyRelative("durationMilliseconds").intValue =
                    Mathf.Max(1, defaultDurationProperty.intValue);
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void AddEmptyFrame()
        {
            serializedObject.Update();
            int index = framesProperty.arraySize;
            framesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty frame = framesProperty.GetArrayElementAtIndex(index);
            frame.FindPropertyRelative("sprite").objectReferenceValue = null;
            frame.FindPropertyRelative("overrideDuration").boolValue = false;
            frame.FindPropertyRelative("durationMilliseconds").intValue =
                Mathf.Max(1, defaultDurationProperty.intValue);

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            frameList.SetSelection(index);
            frameList.ScrollToItem(index);
            OnFrameValueChanged();
        }

        private void RemoveSelectedFrames()
        {
            int[] selected = frameList.selectedIndices.OrderByDescending(index => index).ToArray();
            if (selected.Length == 0)
            {
                if (framesProperty.arraySize == 0)
                {
                    return;
                }

                selected = new[] { framesProperty.arraySize - 1 };
            }

            serializedObject.Update();
            int nextSelection = selected.Min();
            foreach (int index in selected)
            {
                framesProperty.DeleteArrayElementAtIndex(index);
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            if (framesProperty.arraySize > 0)
            {
                frameList.SetSelection(Math.Min(nextSelection, framesProperty.arraySize - 1));
            }

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
                    frame.FindPropertyRelative("overrideDuration").boolValue,
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
                frame.FindPropertyRelative("overrideDuration").boolValue =
                    sorted[index].OverrideDuration;
                frame.FindPropertyRelative("durationMilliseconds").intValue = sorted[index].Duration;
            }

            serializedObject.ApplyModifiedProperties();
            RebuildFrameList();
            OnFrameValueChanged();
        }

        private void OnFrameValueChanged()
        {
            RefreshPreview();
        }

        private void OnUndoRedo()
        {
            if (target == null || serializedObject == null)
            {
                return;
            }

            defaultDurationEditUndoGroup = -1;
            serializedObject.UpdateIfRequiredOrScript();
            RebuildFrameList();
            RefreshDefaultDurationControls();
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
            for (var index = 0; index < asset.Frames.Count; index++)
            {
                SpriteAnimationFrame frame = asset.Frames[index];
                int duration = frame != null ? asset.GetDurationMilliseconds(index) : 0;
                if (duration > 0)
                {
                    total += duration;
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
                int duration = frame != null
                    ? Math.Max(asset.GetDurationMilliseconds(index), 1)
                    : 1;
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
                milliseconds += frame != null
                    ? Math.Max(asset.GetDurationMilliseconds(index), 1)
                    : 1;
            }

            return milliseconds;
        }

        private readonly struct FrameValue
        {
            public FrameValue(
                Sprite sprite,
                bool overrideDuration,
                int duration,
                int originalIndex)
            {
                Sprite = sprite;
                OverrideDuration = overrideDuration;
                Duration = duration;
                OriginalIndex = originalIndex;
            }

            public Sprite Sprite { get; }

            public bool OverrideDuration { get; }

            public int Duration { get; }

            public int OriginalIndex { get; }
        }

        private sealed class FrameRow : VisualElement
        {
            private readonly Image thumbnail;
            private readonly ObjectField spriteField;
            private readonly Toggle durationOverrideToggle;
            private readonly IntegerField durationField;
            private readonly VisualElement durationTextInput;
            private readonly TextElement durationTextElement;
            private readonly Action<int, bool, int> onDurationChanged;
            private readonly Action onSpriteChanged;

            private int frameIndex = -1;
            private int defaultDuration = 100;
            private int storedDuration = 100;
            private bool overrideDuration;
            private bool isRefreshing;
            private int durationEditUndoGroup = -1;

            public FrameRow(
                Action<int, bool, int> onDurationChanged,
                Action onSpriteChanged)
            {
                this.onDurationChanged = onDurationChanged;
                this.onSpriteChanged = onSpriteChanged;
                AddToClassList("sprite-animation-frame-row-host");
                SpriteAnimationUiResources.CloneInto(this, "SpriteAnimationFrameRow.uxml");

                Image rowThumbnail = this.Q<Image>("thumbnail");
                ObjectField rowSpriteField = this.Q<ObjectField>("sprite-field");
                DurationOverrideField rowDurationControl =
                    this.Q<DurationOverrideField>("duration-control");
                if (rowThumbnail == null || rowSpriteField == null ||
                    rowDurationControl == null)
                {
                    Clear();
                    BuildFallbackLayout(
                        out rowThumbnail,
                        out rowSpriteField,
                        out rowDurationControl);
                }

                thumbnail = rowThumbnail;
                thumbnail.scaleMode = ScaleMode.ScaleToFit;
                spriteField = rowSpriteField;
                spriteField.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
                spriteField.objectType = typeof(Sprite);
                spriteField.allowSceneObjects = false;
                durationOverrideToggle = rowDurationControl.OverrideToggle;
                durationField = rowDurationControl.IntegerField;
                durationOverrideToggle.tooltip = "Override Default Duration";
                durationField.tooltip = "Leave empty to inherit Default Duration";
                durationField.isDelayed = false;
                durationField.textEdition.hidePlaceholderOnFocus = true;
                durationTextInput = durationField.Q<VisualElement>(
                    TextInputBaseField<int>.textInputUssName);
                durationTextElement = durationField.Q<TextElement>();

                spriteField.RegisterValueChangedCallback(evt =>
                {
                    thumbnail.sprite = evt.newValue as Sprite;
                    this.onSpriteChanged();
                });
                durationOverrideToggle.RegisterValueChangedCallback(evt =>
                {
                    if (isRefreshing || frameIndex < 0)
                    {
                        return;
                    }

                    overrideDuration = evt.newValue;
                    if (overrideDuration)
                    {
                        storedDuration = defaultDuration;
                    }

                    RefreshDurationDisplay();
                    this.onDurationChanged(
                        frameIndex,
                        overrideDuration,
                        storedDuration);
                    if (overrideDuration)
                    {
                        durationField.schedule.Execute(() =>
                        {
                            durationField.Focus();
                            durationField.SelectAll();
                        });
                    }
                });
                durationField.RegisterCallback<InputEvent>(evt =>
                    HandleDurationTextInput(evt.newData));
                durationField.RegisterCallback<FocusOutEvent>(_ =>
                    EndDurationTextInput());
            }

            public void Bind(
                SerializedProperty frame,
                SerializedProperty defaultDurationProperty,
                int index)
            {
                spriteField.Unbind();
                durationEditUndoGroup = -1;
                SerializedProperty sprite = frame.FindPropertyRelative("sprite");
                frameIndex = index;
                defaultDuration = Mathf.Max(1, defaultDurationProperty.intValue);
                overrideDuration =
                    frame.FindPropertyRelative("overrideDuration").boolValue;
                storedDuration = Mathf.Max(
                    1,
                    frame.FindPropertyRelative("durationMilliseconds").intValue);
                thumbnail.sprite = sprite.objectReferenceValue as Sprite;
                spriteField.BindProperty(sprite);
                RefreshDurationDisplay();
            }

            public void Unbind()
            {
                spriteField.Unbind();
                thumbnail.sprite = null;
                frameIndex = -1;
                durationEditUndoGroup = -1;
            }

            private void HandleDurationTextInput(string text)
            {
                if (isRefreshing || frameIndex < 0)
                {
                    return;
                }

                if (durationEditUndoGroup < 0)
                {
                    durationEditUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Change Frame Duration");
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!overrideDuration)
                    {
                        return;
                    }

                    overrideDuration = false;
                    durationOverrideToggle.SetValueWithoutNotify(false);
                    onDurationChanged(frameIndex, false, storedDuration);
                    Undo.CollapseUndoOperations(durationEditUndoGroup);
                    return;
                }

                if (!int.TryParse(text, out int duration) || duration < 1)
                {
                    return;
                }

                if (overrideDuration && storedDuration == duration)
                {
                    return;
                }

                overrideDuration = true;
                storedDuration = duration;
                durationOverrideToggle.SetValueWithoutNotify(true);
                onDurationChanged(frameIndex, true, duration);
                Undo.CollapseUndoOperations(durationEditUndoGroup);
            }

            private void EndDurationTextInput()
            {
                durationEditUndoGroup = -1;
                RefreshDurationDisplay();
            }

            private void RefreshDurationDisplay()
            {
                if (durationTextElement == null)
                {
                    return;
                }

                isRefreshing = true;
                durationOverrideToggle.SetValueWithoutNotify(overrideDuration);
                if (overrideDuration)
                {
                    durationField.textEdition.placeholder = string.Empty;

                    // The inherited state clears the inner TextElement without
                    // changing IntegerField.value. Force the normal formatter to
                    // run so UI Toolkit also leaves its internal placeholder state.
                    int temporaryValue = storedDuration == int.MaxValue
                        ? storedDuration - 1
                        : storedDuration + 1;
                    durationField.SetValueWithoutNotify(temporaryValue);
                    durationField.SetValueWithoutNotify(storedDuration);
                }
                else
                {
                    durationField.SetValueWithoutNotify(storedDuration);
                    durationTextElement.text = string.Empty;
                    durationField.textEdition.placeholder = defaultDuration.ToString();
                }

                durationTextInput?.EnableInClassList(
                    TextInputBaseField<int>.placeholderUssClassName,
                    !overrideDuration);
                isRefreshing = false;
            }

            private void BuildFallbackLayout(
                out Image rowThumbnail,
                out ObjectField rowSpriteField,
                out DurationOverrideField rowDurationControl)
            {
                var layout = new VisualElement();
                layout.AddToClassList("sprite-animation-frame-row");
                Add(layout);

                rowThumbnail = new Image { name = "thumbnail" };
                rowThumbnail.AddToClassList("sprite-animation-frame-thumbnail");
                layout.Add(rowThumbnail);

                var fields = new VisualElement();
                fields.AddToClassList("sprite-animation-frame-fields");
                layout.Add(fields);

                rowSpriteField = new ObjectField("Sprite") { name = "sprite-field" };
                rowSpriteField.AddToClassList("sprite-animation-frame-sprite-field");
                rowSpriteField.AddToClassList(
                    BaseField<UnityEngine.Object>.alignedFieldUssClassName);
                fields.Add(rowSpriteField);

                rowDurationControl = new DurationOverrideField("Duration (ms)")
                {
                    name = "duration-control",
                };
                fields.Add(rowDurationControl);
            }
        }
    }
}
