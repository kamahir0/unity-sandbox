using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpriteAnimationEditor
{
    public sealed class SpriteAnimationEditorWindow : EditorWindow
    {
        [SerializeField]
        private List<string> selectedGroupGuids = new List<string>();

        [SerializeField]
        private List<string> knownGroupGuids = new List<string>();

        [SerializeField]
        private bool selectionInitialized;

        private SpriteAnimationTreeSelectionState selectionState;
        private SpriteAnimationTreeData treeData;
        private TreeView treeView;
        private Button generateButton;
        private HelpBox statusBox;
        private IVisualElementScheduledItem pendingRefresh;

        [MenuItem("Window/Sprite Animation Editor")]
        public static void Open()
        {
            var window = GetWindow<SpriteAnimationEditorWindow>();
            window.titleContent = new GUIContent("Sprite Animation Editor");
            window.minSize = new Vector2(340f, 240f);
            window.Show();
        }

        private void OnEnable()
        {
            selectionState = new SpriteAnimationTreeSelectionState();
            selectionState.Restore(selectedGroupGuids, knownGroupGuids, selectionInitialized);
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            CaptureSelectionState();
            EditorApplication.projectChanged -= OnProjectChanged;
            pendingRefresh?.Pause();
            pendingRefresh = null;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualElement content = SpriteAnimationUiResources.Clone(
                "SpriteAnimationEditorWindow.uxml",
                "SpriteAnimationEditor.uss");
            rootVisualElement.Add(content);

            treeView = content.Q<TreeView>("group-tree");
            Button refreshButton = content.Q<Button>("refresh-button");
            Button selectAllButton = content.Q<Button>("select-all-button");
            Button clearButton = content.Q<Button>("clear-button");
            generateButton = content.Q<Button>("generate-button");
            statusBox = content.Q<HelpBox>("status-box");

            ConfigureTreeView();
            refreshButton.clicked += RefreshTree;
            selectAllButton.clicked += SelectAll;
            clearButton.clicked += ClearSelection;
            generateButton.clicked += GenerateSelected;
            RefreshTree();
        }

        private void ConfigureTreeView()
        {
            treeView.fixedItemHeight = 22f;
            treeView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            treeView.selectionType = SelectionType.Single;
            treeView.makeItem = () => new TreeRow(OnRowToggleChanged);
            treeView.bindItem = (element, index) =>
            {
                SpriteAnimationTreeNode node =
                    treeView.GetItemDataForIndex<SpriteAnimationTreeNode>(index);
                ((TreeRow)element).Bind(node, selectionState.IsSelected(node));
            };
            treeView.unbindItem = (element, _) => ((TreeRow)element).Unbind();
            treeView.itemsChosen += OnItemsChosen;
        }

        private void RefreshTree()
        {
            if (treeView == null)
            {
                return;
            }

            treeData = SpriteAnimationTreeDataBuilder.Build();
            selectionState.Refresh(treeData.GroupGuids);
            CaptureSelectionState();
            treeView.SetRootItems(treeData.RootItems);
            treeView.Rebuild();
            treeView.ExpandAll();
            statusBox.text = treeData.GroupGuids.Count == 0
                ? "No SpriteAnimationGroupAsset exists under Assets."
                : $"{selectionState.Selected.Count} of {treeData.GroupGuids.Count} groups selected.";
            statusBox.messageType = HelpBoxMessageType.None;
            UpdateGenerateButton();
        }

        private void OnProjectChanged()
        {
            if (rootVisualElement?.panel == null)
            {
                return;
            }

            pendingRefresh?.Pause();
            pendingRefresh = rootVisualElement.schedule.Execute(RefreshTree);
        }

        private void OnRowToggleChanged(SpriteAnimationTreeNode node, bool selected)
        {
            selectionState.SetSelected(node, selected);
            CaptureSelectionState();
            treeView.RefreshItems();
            statusBox.text = $"{selectionState.Selected.Count} of {treeData.GroupGuids.Count} groups selected.";
            statusBox.messageType = HelpBoxMessageType.None;
            UpdateGenerateButton();
        }

        private void SelectAll()
        {
            selectionState.SelectAll();
            CaptureSelectionState();
            treeView.RefreshItems();
            statusBox.text = $"{selectionState.Selected.Count} of {treeData.GroupGuids.Count} groups selected.";
            statusBox.messageType = HelpBoxMessageType.None;
            UpdateGenerateButton();
        }

        private void ClearSelection()
        {
            selectionState.Clear();
            CaptureSelectionState();
            treeView.RefreshItems();
            statusBox.text = "No groups selected.";
            statusBox.messageType = HelpBoxMessageType.None;
            UpdateGenerateButton();
        }

        private void GenerateSelected()
        {
            SpriteAnimationGroupAsset[] groups = selectionState.Selected
                .Where(guid => treeData.GroupsByGuid.ContainsKey(guid))
                .Select(guid => treeData.GroupsByGuid[guid])
                .OrderBy(group => AssetDatabase.GetAssetPath(group), NaturalStringComparer.Instance)
                .ToArray();
            SpriteAnimationGenerationReport report = SpriteAnimationClipGenerator.Generate(groups);
            ShowReport(report);
        }

        private void ShowReport(SpriteAnimationGenerationReport report)
        {
            if (report.Succeeded)
            {
                statusBox.text = report.Results.Count == 0
                    ? "Validation succeeded; no clips required changes."
                    : $"Generated {report.Results.Count} clips: " +
                      $"{report.Created.Count} created, {report.Updated.Count} updated, {report.Moved.Count} moved.";
                statusBox.messageType = HelpBoxMessageType.Info;
                return;
            }

            SpriteAnimationGenerationMessage[] errors = report.Messages
                .Where(message => message.Severity == SpriteAnimationGenerationMessageSeverity.Error)
                .ToArray();
            statusBox.text = $"Generation stopped with {errors.Length} validation error(s).\n" +
                string.Join("\n", errors.Take(5).Select(error => error.Text));
            statusBox.messageType = HelpBoxMessageType.Error;

            foreach (SpriteAnimationGenerationMessage error in errors)
            {
                UnityEngine.Object context = error.Source != null ? error.Source : error.Group;
                Debug.LogError(error.Text, context);
            }
        }

        private void OnItemsChosen(IEnumerable<object> items)
        {
            SpriteAnimationTreeNode node = items.OfType<SpriteAnimationTreeNode>().FirstOrDefault();
            if (node?.Group == null)
            {
                return;
            }

            Selection.activeObject = node.Group;
            EditorGUIUtility.PingObject(node.Group);
        }

        private void CaptureSelectionState()
        {
            if (selectionState == null)
            {
                return;
            }

            selectedGroupGuids = selectionState.Selected.OrderBy(value => value).ToList();
            knownGroupGuids = selectionState.Known.OrderBy(value => value).ToList();
            selectionInitialized = selectionState.Initialized;
        }

        private void UpdateGenerateButton()
        {
            generateButton?.SetEnabled(selectionState != null && selectionState.Selected.Count > 0);
        }

        private sealed class TreeRow : VisualElement
        {
            private readonly Toggle toggle;
            private readonly Image icon;
            private readonly Label label;
            private readonly Action<SpriteAnimationTreeNode, bool> onToggleChanged;
            private SpriteAnimationTreeNode node;

            public TreeRow(Action<SpriteAnimationTreeNode, bool> onToggleChanged)
            {
                this.onToggleChanged = onToggleChanged;
                AddToClassList("sprite-animation-tree-row");

                toggle = new Toggle();
                toggle.AddToClassList("sprite-animation-tree-toggle");
                toggle.RegisterValueChangedCallback(OnToggleChanged);
                Add(toggle);

                icon = new Image { scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("sprite-animation-tree-icon");
                Add(icon);

                label = new Label();
                label.AddToClassList("sprite-animation-tree-label");
                Add(label);
            }

            public void Bind(SpriteAnimationTreeNode value, bool selected)
            {
                node = value;
                toggle.SetValueWithoutNotify(selected);
                label.text = value.Name;
                icon.image = value.Kind == SpriteAnimationTreeNodeKind.Folder
                    ? EditorGUIUtility.IconContent("Folder Icon").image
                    : AssetPreview.GetMiniThumbnail(value.Group);
                tooltip = value.AssetPath;
            }

            public void Unbind()
            {
                node = null;
                icon.image = null;
                label.text = string.Empty;
                tooltip = null;
            }

            private void OnToggleChanged(ChangeEvent<bool> changeEvent)
            {
                if (node != null)
                {
                    onToggleChanged(node, changeEvent.newValue);
                }
            }
        }
    }
}
