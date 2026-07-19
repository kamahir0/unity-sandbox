using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpriteAnimationEditor
{
    [CustomEditor(typeof(SpriteAnimationGroupAsset))]
    public sealed class SpriteAnimationGroupAssetEditor : UnityEditor.Editor
    {
        private HelpBox statusBox;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = SpriteAnimationUiResources.Clone(
                "SpriteAnimationGroupAssetEditor.uxml",
                "SpriteAnimationEditor.uss");
            VisualElement fields = root.Q<VisualElement>("group-fields");
            fields.Add(new PropertyField(serializedObject.FindProperty("animations")));
            fields.Add(new PropertyField(serializedObject.FindProperty("outputFolder")));
            fields.Add(new PropertyField(serializedObject.FindProperty("bindingPath")));

            Button generateButton = root.Q<Button>("generate-group-button");
            statusBox = root.Q<HelpBox>("generation-status-box");
            generateButton.clicked += GenerateGroup;
            root.Bind(serializedObject);
            statusBox.text = "Generation validates the complete group before changing any AnimationClip.";
            statusBox.messageType = HelpBoxMessageType.None;
            return root;
        }

        private void GenerateGroup()
        {
            serializedObject.ApplyModifiedProperties();
            var group = (SpriteAnimationGroupAsset)target;
            SpriteAnimationGenerationReport report = SpriteAnimationClipGenerator.Generate(group);

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
            statusBox.text = string.Join("\n", errors.Take(6).Select(error => error.Text));
            statusBox.messageType = HelpBoxMessageType.Error;
            foreach (SpriteAnimationGenerationMessage error in errors)
            {
                Object context = error.Source != null ? error.Source : error.Group;
                Debug.LogError(error.Text, context);
            }
        }
    }
}
