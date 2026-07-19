using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace SpriteAnimationEditor
{
    public enum SpriteAnimationTreeNodeKind
    {
        Folder,
        Group,
    }

    public sealed class SpriteAnimationTreeNode
    {
        internal SpriteAnimationTreeNode(
            SpriteAnimationTreeNodeKind kind,
            string name,
            string assetPath,
            string groupGuid,
            SpriteAnimationGroupAsset group,
            IReadOnlyList<string> descendantGroupGuids)
        {
            Kind = kind;
            Name = name;
            AssetPath = assetPath;
            GroupGuid = groupGuid;
            Group = group;
            DescendantGroupGuids = descendantGroupGuids;
        }

        public SpriteAnimationTreeNodeKind Kind { get; }

        public string Name { get; }

        public string AssetPath { get; }

        public string GroupGuid { get; }

        public SpriteAnimationGroupAsset Group { get; }

        public IReadOnlyList<string> DescendantGroupGuids { get; }
    }

    internal sealed class SpriteAnimationTreeData
    {
        public SpriteAnimationTreeData(
            List<TreeViewItemData<SpriteAnimationTreeNode>> rootItems,
            IReadOnlyList<string> groupGuids,
            IReadOnlyDictionary<string, SpriteAnimationGroupAsset> groupsByGuid)
        {
            RootItems = rootItems;
            GroupGuids = groupGuids;
            GroupsByGuid = groupsByGuid;
        }

        public List<TreeViewItemData<SpriteAnimationTreeNode>> RootItems { get; }

        public IReadOnlyList<string> GroupGuids { get; }

        public IReadOnlyDictionary<string, SpriteAnimationGroupAsset> GroupsByGuid { get; }
    }

    internal static class SpriteAnimationTreeDataBuilder
    {
        public static SpriteAnimationTreeData Build()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:SpriteAnimationGroupAsset",
                new[] { "Assets" });
            var root = new FolderBuilder("Assets", "Assets");
            var groupsByGuid = new Dictionary<string, SpriteAnimationGroupAsset>(StringComparer.Ordinal);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                SpriteAnimationGroupAsset group =
                    AssetDatabase.LoadAssetAtPath<SpriteAnimationGroupAsset>(assetPath);
                if (group == null)
                {
                    continue;
                }

                string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
                FolderBuilder folder = EnsureFolder(root, directory);
                folder.Groups.Add(new GroupBuilder(
                    Path.GetFileNameWithoutExtension(assetPath),
                    assetPath,
                    guid,
                    group));
                groupsByGuid[guid] = group;
            }

            var nextId = 1;
            TreeViewItemData<SpriteAnimationTreeNode> rootItem = BuildFolderItem(root, ref nextId);
            string[] sortedGuids = groupsByGuid
                .OrderBy(pair => AssetDatabase.GetAssetPath(pair.Value), NaturalStringComparer.Instance)
                .Select(pair => pair.Key)
                .ToArray();

            return new SpriteAnimationTreeData(
                new List<TreeViewItemData<SpriteAnimationTreeNode>> { rootItem },
                sortedGuids,
                groupsByGuid);
        }

        private static FolderBuilder EnsureFolder(FolderBuilder root, string directory)
        {
            if (directory == "Assets")
            {
                return root;
            }

            string relative = directory.StartsWith("Assets/", StringComparison.Ordinal)
                ? directory.Substring("Assets/".Length)
                : directory;
            string currentPath = "Assets";
            FolderBuilder current = root;

            foreach (string segment in relative.Split('/'))
            {
                currentPath += "/" + segment;
                if (!current.Folders.TryGetValue(segment, out FolderBuilder child))
                {
                    child = new FolderBuilder(segment, currentPath);
                    current.Folders.Add(segment, child);
                }

                current = child;
            }

            return current;
        }

        private static TreeViewItemData<SpriteAnimationTreeNode> BuildFolderItem(
            FolderBuilder folder,
            ref int nextId)
        {
            var children = new List<TreeViewItemData<SpriteAnimationTreeNode>>();
            var descendantGuids = new List<string>();

            foreach (FolderBuilder childFolder in folder.Folders.Values
                         .OrderBy(item => item.Name, NaturalStringComparer.Instance))
            {
                TreeViewItemData<SpriteAnimationTreeNode> childItem =
                    BuildFolderItem(childFolder, ref nextId);
                children.Add(childItem);
                descendantGuids.AddRange(childItem.data.DescendantGroupGuids);
            }

            foreach (GroupBuilder group in folder.Groups
                         .OrderBy(item => item.Name, NaturalStringComparer.Instance))
            {
                var node = new SpriteAnimationTreeNode(
                    SpriteAnimationTreeNodeKind.Group,
                    group.Name,
                    group.AssetPath,
                    group.Guid,
                    group.Group,
                    new[] { group.Guid });
                children.Add(new TreeViewItemData<SpriteAnimationTreeNode>(nextId++, node));
                descendantGuids.Add(group.Guid);
            }

            var folderNode = new SpriteAnimationTreeNode(
                SpriteAnimationTreeNodeKind.Folder,
                folder.Name,
                folder.AssetPath,
                null,
                null,
                descendantGuids);
            return new TreeViewItemData<SpriteAnimationTreeNode>(nextId++, folderNode, children);
        }

        private sealed class FolderBuilder
        {
            public FolderBuilder(string name, string assetPath)
            {
                Name = name;
                AssetPath = assetPath;
            }

            public string Name { get; }

            public string AssetPath { get; }

            public Dictionary<string, FolderBuilder> Folders { get; } =
                new Dictionary<string, FolderBuilder>(StringComparer.Ordinal);

            public List<GroupBuilder> Groups { get; } = new List<GroupBuilder>();
        }

        private sealed class GroupBuilder
        {
            public GroupBuilder(
                string name,
                string assetPath,
                string guid,
                SpriteAnimationGroupAsset group)
            {
                Name = name;
                AssetPath = assetPath;
                Guid = guid;
                Group = group;
            }

            public string Name { get; }

            public string AssetPath { get; }

            public string Guid { get; }

            public SpriteAnimationGroupAsset Group { get; }
        }
    }

    internal sealed class SpriteAnimationTreeSelectionState
    {
        private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> known = new HashSet<string>(StringComparer.Ordinal);
        private bool initialized;

        public void Restore(
            IEnumerable<string> selectedGuids,
            IEnumerable<string> knownGuids,
            bool wasInitialized)
        {
            selected.Clear();
            known.Clear();
            selected.UnionWith(selectedGuids ?? Array.Empty<string>());
            known.UnionWith(knownGuids ?? Array.Empty<string>());
            initialized = wasInitialized;
        }

        public void Refresh(IEnumerable<string> currentGuids)
        {
            var current = new HashSet<string>(currentGuids ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (!initialized)
            {
                selected.Clear();
                selected.UnionWith(current);
                initialized = true;
            }
            else
            {
                selected.IntersectWith(current);
                foreach (string newGuid in current.Except(known))
                {
                    selected.Add(newGuid);
                }
            }

            known.Clear();
            known.UnionWith(current);
        }

        public bool IsSelected(SpriteAnimationTreeNode node)
        {
            return node.DescendantGroupGuids.Count > 0 &&
                node.DescendantGroupGuids.All(selected.Contains);
        }

        public void SetSelected(SpriteAnimationTreeNode node, bool value)
        {
            foreach (string guid in node.DescendantGroupGuids)
            {
                if (value)
                {
                    selected.Add(guid);
                }
                else
                {
                    selected.Remove(guid);
                }
            }
        }

        public void SelectAll()
        {
            selected.Clear();
            selected.UnionWith(known);
        }

        public void Clear()
        {
            selected.Clear();
        }

        public IReadOnlyCollection<string> Selected => selected;

        public IReadOnlyCollection<string> Known => known;

        public bool Initialized => initialized;
    }

    internal sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        public int Compare(string left, string right)
        {
            return EditorUtility.NaturalCompare(left ?? string.Empty, right ?? string.Empty);
        }
    }
}
