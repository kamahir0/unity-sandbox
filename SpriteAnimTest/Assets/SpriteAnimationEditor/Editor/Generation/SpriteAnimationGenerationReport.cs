using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpriteAnimationEditor
{
    public enum SpriteAnimationGenerationMessageSeverity
    {
        Info,
        Warning,
        Error,
    }

    public enum SpriteAnimationGenerationAction
    {
        Created,
        Updated,
        MovedAndUpdated,
    }

    public sealed class SpriteAnimationGenerationMessage
    {
        internal SpriteAnimationGenerationMessage(
            SpriteAnimationGenerationMessageSeverity severity,
            string text,
            SpriteAnimationGroupAsset group,
            SpriteAnimationAsset source,
            string outputPath)
        {
            Severity = severity;
            Text = text;
            Group = group;
            Source = source;
            OutputPath = outputPath;
        }

        public SpriteAnimationGenerationMessageSeverity Severity { get; }

        public string Text { get; }

        public SpriteAnimationGroupAsset Group { get; }

        public SpriteAnimationAsset Source { get; }

        public string OutputPath { get; }
    }

    public sealed class SpriteAnimationGenerationResult
    {
        internal SpriteAnimationGenerationResult(
            SpriteAnimationGenerationAction action,
            SpriteAnimationGroupAsset group,
            SpriteAnimationAsset source,
            AnimationClip clip,
            string outputPath)
        {
            Action = action;
            Group = group;
            Source = source;
            Clip = clip;
            OutputPath = outputPath;
        }

        public SpriteAnimationGenerationAction Action { get; }

        public SpriteAnimationGroupAsset Group { get; }

        public SpriteAnimationAsset Source { get; }

        public AnimationClip Clip { get; }

        public string OutputPath { get; }
    }

    public sealed class SpriteAnimationGenerationReport
    {
        private readonly List<SpriteAnimationGenerationMessage> messages =
            new List<SpriteAnimationGenerationMessage>();

        private readonly List<SpriteAnimationGenerationResult> results =
            new List<SpriteAnimationGenerationResult>();

        public bool Succeeded => messages.All(message =>
            message.Severity != SpriteAnimationGenerationMessageSeverity.Error);

        public bool Generated { get; internal set; }

        public IReadOnlyList<SpriteAnimationGenerationMessage> Messages => messages;

        public IReadOnlyList<SpriteAnimationGenerationResult> Results => results;

        public IReadOnlyList<SpriteAnimationGenerationResult> Created => results
            .Where(result => result.Action == SpriteAnimationGenerationAction.Created)
            .ToArray();

        public IReadOnlyList<SpriteAnimationGenerationResult> Updated => results
            .Where(result => result.Action == SpriteAnimationGenerationAction.Updated)
            .ToArray();

        public IReadOnlyList<SpriteAnimationGenerationResult> Moved => results
            .Where(result => result.Action == SpriteAnimationGenerationAction.MovedAndUpdated)
            .ToArray();

        internal void AddMessage(
            SpriteAnimationGenerationMessageSeverity severity,
            string text,
            SpriteAnimationGroupAsset group = null,
            SpriteAnimationAsset source = null,
            string outputPath = null)
        {
            messages.Add(new SpriteAnimationGenerationMessage(
                severity,
                text,
                group,
                source,
                outputPath));
        }

        internal void AddResult(
            SpriteAnimationGenerationAction action,
            SpriteAnimationGroupAsset group,
            SpriteAnimationAsset source,
            AnimationClip clip,
            string outputPath)
        {
            results.Add(new SpriteAnimationGenerationResult(
                action,
                group,
                source,
                clip,
                outputPath));
        }
    }
}
