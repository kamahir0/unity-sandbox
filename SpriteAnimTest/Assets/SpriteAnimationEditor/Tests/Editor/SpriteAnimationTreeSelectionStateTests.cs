using System.Linq;
using NUnit.Framework;

namespace SpriteAnimationEditor.Tests
{
    public sealed class SpriteAnimationTreeSelectionStateTests
    {
        [Test]
        public void FolderToggleAndChildToggleFollowSpecifiedBooleanRules()
        {
            var state = new SpriteAnimationTreeSelectionState();
            state.Restore(null, null, false);
            state.Refresh(new[] { "a", "b" });
            SpriteAnimationTreeNode folder = Folder("a", "b");
            SpriteAnimationTreeNode childB = Group("b");

            Assert.That(state.IsSelected(folder), Is.True);

            state.SetSelected(childB, false);
            Assert.That(state.IsSelected(childB), Is.False);
            Assert.That(state.IsSelected(folder), Is.False);

            state.SetSelected(folder, true);
            Assert.That(state.IsSelected(folder), Is.True);

            state.SetSelected(folder, false);
            Assert.That(state.Selected, Is.Empty);
        }

        [Test]
        public void RefreshPreservesExistingChoicesAndSelectsNewGroups()
        {
            var state = new SpriteAnimationTreeSelectionState();
            state.Restore(null, null, false);
            state.Refresh(new[] { "a", "b" });
            state.SetSelected(Group("b"), false);

            state.Refresh(new[] { "a", "b", "c" });

            Assert.That(state.Selected.OrderBy(value => value), Is.EqualTo(new[] { "a", "c" }));

            state.Refresh(new[] { "b", "c" });
            Assert.That(state.Selected, Is.EquivalentTo(new[] { "c" }));
        }

        [Test]
        public void NaturalComparerOrdersNumericSuffixesNaturally()
        {
            string[] values = { "Group10", "Group2", "Group1" };

            string[] sorted = values.OrderBy(value => value, NaturalStringComparer.Instance).ToArray();

            Assert.That(sorted, Is.EqualTo(new[] { "Group1", "Group2", "Group10" }));
        }

        private static SpriteAnimationTreeNode Folder(params string[] descendants)
        {
            return new SpriteAnimationTreeNode(
                SpriteAnimationTreeNodeKind.Folder,
                "Folder",
                "Assets/Folder",
                null,
                null,
                descendants);
        }

        private static SpriteAnimationTreeNode Group(string guid)
        {
            return new SpriteAnimationTreeNode(
                SpriteAnimationTreeNodeKind.Group,
                guid,
                "Assets/" + guid + ".asset",
                guid,
                null,
                new[] { guid });
        }
    }
}
