using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpriteAnimationEditor
{
    public static class SpriteAnimationClipGenerator
    {
        public const string GeneratedLabel = "SpriteAnimationEditor.Generated";
        public const string SourceGuidLabelPrefix = "SpriteAnimationEditor.SourceGuid.";
        public const string GroupGuidLabelPrefix = "SpriteAnimationEditor.GroupGuid.";

        private const string SpritePropertyName = "m_Sprite";
        private const float ClipFrameRate = 1000f;
        private const float MillisecondsPerSecond = 1000f;

        public static SpriteAnimationGenerationReport Validate(
            IReadOnlyList<SpriteAnimationGroupAsset> groups)
        {
            BuildPlan(groups, out SpriteAnimationGenerationReport report);
            return report;
        }

        public static SpriteAnimationGenerationReport Generate(SpriteAnimationGroupAsset group)
        {
            return Generate(new[] { group });
        }

        public static SpriteAnimationGenerationReport Generate(
            IReadOnlyList<SpriteAnimationGroupAsset> groups)
        {
            List<GenerationOperation> operations = BuildPlan(groups, out SpriteAnimationGenerationReport report);
            LogSkippedAnimations(report);
            if (!report.Succeeded)
            {
                return report;
            }

            foreach (GenerationOperation operation in operations)
            {
                try
                {
                    Execute(operation, report);
                }
                catch (Exception exception)
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        $"Unexpected generation failure: {exception.Message}",
                        operation.Group,
                        operation.Source,
                        operation.TargetPath);
                    Debug.LogException(exception);
                    break;
                }
            }

            AssetDatabase.SaveAssets();
            report.Generated = report.Succeeded;
            return report;
        }

        internal static void ApplyToClip(
            SpriteAnimationAsset source,
            AnimationClip clip,
            string bindingPath)
        {
            foreach (EditorCurveBinding curveBinding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, curveBinding, null);
            }

            foreach (EditorCurveBinding curveBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, null);
            }

            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            IReadOnlyList<SpriteAnimationFrame> frames = source.Frames;
            var keyframes = new List<ObjectReferenceKeyframe>(frames.Count + 1);
            long elapsedMilliseconds = 0;
            long lastFrameStartMilliseconds = 0;

            for (var index = 0; index < frames.Count; index++)
            {
                SpriteAnimationFrame frame = frames[index];
                lastFrameStartMilliseconds = elapsedMilliseconds;
                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = elapsedMilliseconds / MillisecondsPerSecond,
                    value = frame.Sprite,
                });
                elapsedMilliseconds += frame.DurationMilliseconds;
            }

            // Unity includes one sample after the last object-reference key in
            // AnimationClip.length. Anchor the last visible sample instead of adding a key
            // at the cycle boundary, otherwise every generated clip gains one extra millisecond.
            long finalSampleMillisecond = elapsedMilliseconds - 1;
            if (finalSampleMillisecond > lastFrameStartMilliseconds)
            {
                keyframes.Add(new ObjectReferenceKeyframe
                {
                    time = finalSampleMillisecond / MillisecondsPerSecond,
                    value = frames[frames.Count - 1].Sprite,
                });
            }

            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
                bindingPath ?? string.Empty,
                typeof(SpriteRenderer),
                SpritePropertyName);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());

            clip.name = source.name;
            clip.frameRate = ClipFrameRate;
            clip.legacy = false;
            clip.wrapMode = WrapMode.Default;

            var settings = new AnimationClipSettings
            {
                startTime = 0f,
                stopTime = elapsedMilliseconds / MillisecondsPerSecond,
                loopTime = source.Loop,
                loopBlend = false,
            };
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static List<GenerationOperation> BuildPlan(
            IReadOnlyList<SpriteAnimationGroupAsset> groups,
            out SpriteAnimationGenerationReport report)
        {
            report = new SpriteAnimationGenerationReport();
            var operations = new List<GenerationOperation>();
            if (groups == null || groups.Count == 0)
            {
                report.AddMessage(
                    SpriteAnimationGenerationMessageSeverity.Error,
                    "No SpriteAnimationGroupAsset was supplied.");
                return operations;
            }

            Dictionary<OwnerKey, List<OwnedClip>> ownedClips = FindOwnedClips();
            var seenGroupGuids = new HashSet<string>(StringComparer.Ordinal);
            var outputOwners = new Dictionary<string, GenerationOperation>(StringComparer.OrdinalIgnoreCase);

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                SpriteAnimationGroupAsset group = groups[groupIndex];
                if (group == null)
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        $"Group at index {groupIndex} is null.");
                    continue;
                }

                string groupPath = AssetDatabase.GetAssetPath(group);
                string groupGuid = AssetDatabase.AssetPathToGUID(groupPath);
                if (string.IsNullOrEmpty(groupGuid) || !IsAssetsPath(groupPath))
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        "The group must be a saved asset under Assets.",
                        group);
                    continue;
                }

                if (!seenGroupGuids.Add(groupGuid))
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        "The same group was supplied more than once.",
                        group);
                    continue;
                }

                string outputFolderPath = AssetDatabase.GetAssetPath(group.OutputFolder);
                bool outputFolderValid = IsAssetsPath(outputFolderPath) &&
                    AssetDatabase.IsValidFolder(outputFolderPath);
                if (!outputFolderValid)
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        "Output Folder must reference a valid folder under Assets.",
                        group);
                }

                if (!IsValidBindingPath(group.BindingPath, out string bindingPathError))
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        bindingPathError,
                        group);
                }

                IReadOnlyList<SpriteAnimationAsset> animations = group.Animations;
                if (animations == null || animations.Count == 0)
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        "The group contains no SpriteAnimationAsset references.",
                        group);
                    continue;
                }

                var seenSourceGuids = new HashSet<string>(StringComparer.Ordinal);
                for (var animationIndex = 0; animationIndex < animations.Count; animationIndex++)
                {
                    SpriteAnimationAsset source = animations[animationIndex];
                    if (source == null)
                    {
                        report.AddMessage(
                            SpriteAnimationGenerationMessageSeverity.Error,
                            $"Animation reference at index {animationIndex} is null.",
                            group);
                        continue;
                    }

                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                    if (string.IsNullOrEmpty(sourceGuid) || !IsAssetsPath(sourcePath))
                    {
                        report.AddMessage(
                            SpriteAnimationGenerationMessageSeverity.Error,
                            "The animation must be a saved asset under Assets.",
                            group,
                            source);
                        continue;
                    }

                    if (!seenSourceGuids.Add(sourceGuid))
                    {
                        report.AddMessage(
                            SpriteAnimationGenerationMessageSeverity.Error,
                            "The group contains the same animation more than once.",
                            group,
                            source);
                        continue;
                    }

                    if (!TryValidateSource(source, out string skipReason))
                    {
                        report.AddMessage(
                            SpriteAnimationGenerationMessageSeverity.Warning,
                            $"Skipped animation '{source.name}': {skipReason}",
                            group,
                            source);
                        continue;
                    }

                    if (!outputFolderValid)
                    {
                        continue;
                    }

                    string clipName = Path.GetFileNameWithoutExtension(sourcePath);
                    string targetPath = $"{outputFolderPath}/{clipName}.anim";
                    var ownerKey = new OwnerKey(groupGuid, sourceGuid);
                    var operation = new GenerationOperation(
                        group,
                        source,
                        ownerKey,
                        targetPath,
                        group.BindingPath ?? string.Empty);

                    if (outputOwners.TryGetValue(targetPath, out GenerationOperation otherOperation))
                    {
                        report.AddMessage(
                            SpriteAnimationGenerationMessageSeverity.Error,
                            $"Output path conflicts with {otherOperation.Group.name}/{otherOperation.Source.name}: {targetPath}",
                            group,
                            source,
                            targetPath);
                        continue;
                    }

                    outputOwners.Add(targetPath, operation);
                    ResolveExistingClip(operation, ownedClips, report);
                    operations.Add(operation);
                }
            }

            return operations;
        }

        private static bool TryValidateSource(
            SpriteAnimationAsset source,
            out string reason)
        {
            IReadOnlyList<SpriteAnimationFrame> frames = source.Frames;
            if (frames == null || frames.Count == 0)
            {
                reason = "The animation contains no frames.";
                return false;
            }

            var errors = new List<string>();
            long totalMilliseconds = 0;
            for (var frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                SpriteAnimationFrame frame = frames[frameIndex];
                if (frame == null)
                {
                    errors.Add($"Frame {frameIndex} is null.");
                    continue;
                }

                if (frame.Sprite == null)
                {
                    errors.Add($"Frame {frameIndex} has no Sprite.");
                }

                if (frame.DurationMilliseconds < 1)
                {
                    errors.Add($"Frame {frameIndex} Duration Milliseconds must be at least 1.");
                }
                else
                {
                    totalMilliseconds += frame.DurationMilliseconds;
                }
            }

            if (!float.IsFinite(totalMilliseconds / MillisecondsPerSecond) ||
                totalMilliseconds <= 0)
            {
                errors.Add("The animation duration is invalid or too large.");
            }

            reason = string.Join(" ", errors);
            return errors.Count == 0;
        }

        private static void LogSkippedAnimations(SpriteAnimationGenerationReport report)
        {
            foreach (SpriteAnimationGenerationMessage message in report.Messages.Where(message =>
                         message.Severity == SpriteAnimationGenerationMessageSeverity.Warning))
            {
                UnityEngine.Object context = message.Source != null ? message.Source : message.Group;
                Debug.LogWarning(message.Text, context);
            }
        }

        private static bool IsValidBindingPath(string path, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(path) ||
                path != path.Trim() ||
                path.Contains('\\') ||
                path.StartsWith("/", StringComparison.Ordinal) ||
                path.EndsWith("/", StringComparison.Ordinal) ||
                path.Contains("//", StringComparison.Ordinal))
            {
                error = "Binding Path must be an empty string or a normalized relative Transform path using '/'.";
                return false;
            }

            string[] segments = path.Split('/');
            if (segments.Any(segment => segment == "." || segment == ".."))
            {
                error = "Binding Path cannot contain '.' or '..' segments.";
                return false;
            }

            return true;
        }

        private static void ResolveExistingClip(
            GenerationOperation operation,
            IReadOnlyDictionary<OwnerKey, List<OwnedClip>> ownedClips,
            SpriteAnimationGenerationReport report)
        {
            ownedClips.TryGetValue(operation.OwnerKey, out List<OwnedClip> ownerMatches);
            ownerMatches ??= new List<OwnedClip>();

            if (ownerMatches.Count > 1)
            {
                report.AddMessage(
                    SpriteAnimationGenerationMessageSeverity.Error,
                    "Multiple generated clips claim the same Group and Source ownership.",
                    operation.Group,
                    operation.Source,
                    operation.TargetPath);
                return;
            }

            UnityEngine.Object targetAsset = AssetDatabase.LoadMainAssetAtPath(operation.TargetPath);
            if (ownerMatches.Count == 0)
            {
                if (targetAsset != null)
                {
                    report.AddMessage(
                        SpriteAnimationGenerationMessageSeverity.Error,
                        "The output path is occupied by an asset not owned by this Group and Source.",
                        operation.Group,
                        operation.Source,
                        operation.TargetPath);
                    return;
                }

                operation.Action = PlannedAction.Create;
                return;
            }

            OwnedClip ownedClip = ownerMatches[0];
            operation.ExistingClip = ownedClip.Clip;
            operation.ExistingPath = ownedClip.Path;

            if (string.Equals(ownedClip.Path, operation.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                operation.Action = PlannedAction.Update;
                return;
            }

            if (targetAsset != null)
            {
                report.AddMessage(
                    SpriteAnimationGenerationMessageSeverity.Error,
                    "The new output path is occupied, so the owned clip cannot be moved.",
                    operation.Group,
                    operation.Source,
                    operation.TargetPath);
                return;
            }

            operation.Action = PlannedAction.MoveAndUpdate;
        }

        private static void Execute(
            GenerationOperation operation,
            SpriteAnimationGenerationReport report)
        {
            AnimationClip clip;
            SpriteAnimationGenerationAction resultAction;

            switch (operation.Action)
            {
                case PlannedAction.Create:
                    clip = new AnimationClip();
                    ApplyToClip(operation.Source, clip, operation.BindingPath);
                    AssetDatabase.CreateAsset(clip, operation.TargetPath);
                    resultAction = SpriteAnimationGenerationAction.Created;
                    break;

                case PlannedAction.Update:
                    clip = operation.ExistingClip;
                    ApplyToClip(operation.Source, clip, operation.BindingPath);
                    resultAction = SpriteAnimationGenerationAction.Updated;
                    break;

                case PlannedAction.MoveAndUpdate:
                    string moveError = AssetDatabase.MoveAsset(operation.ExistingPath, operation.TargetPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        throw new InvalidOperationException(moveError);
                    }

                    clip = operation.ExistingClip;
                    ApplyToClip(operation.Source, clip, operation.BindingPath);
                    resultAction = SpriteAnimationGenerationAction.MovedAndUpdated;
                    break;

                default:
                    throw new InvalidOperationException("The generation operation was not fully validated.");
            }

            ApplyOwnershipLabels(clip, operation.OwnerKey);
            report.AddResult(resultAction, operation.Group, operation.Source, clip, operation.TargetPath);
        }

        private static Dictionary<OwnerKey, List<OwnedClip>> FindOwnedClips()
        {
            var result = new Dictionary<OwnerKey, List<OwnedClip>>();
            string[] guids = AssetDatabase.FindAssets(
                $"l:{GeneratedLabel} t:AnimationClip",
                new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || !TryGetOwner(clip, out OwnerKey ownerKey))
                {
                    continue;
                }

                if (!result.TryGetValue(ownerKey, out List<OwnedClip> clips))
                {
                    clips = new List<OwnedClip>();
                    result.Add(ownerKey, clips);
                }

                clips.Add(new OwnedClip(clip, path));
            }

            return result;
        }

        private static bool TryGetOwner(AnimationClip clip, out OwnerKey ownerKey)
        {
            ownerKey = default;
            string sourceGuid = null;
            string groupGuid = null;
            string[] labels = AssetDatabase.GetLabels(clip);

            foreach (string label in labels)
            {
                if (label.StartsWith(SourceGuidLabelPrefix, StringComparison.Ordinal))
                {
                    sourceGuid = label.Substring(SourceGuidLabelPrefix.Length);
                }
                else if (label.StartsWith(GroupGuidLabelPrefix, StringComparison.Ordinal))
                {
                    groupGuid = label.Substring(GroupGuidLabelPrefix.Length);
                }
            }

            if (string.IsNullOrEmpty(sourceGuid) || string.IsNullOrEmpty(groupGuid))
            {
                return false;
            }

            ownerKey = new OwnerKey(groupGuid, sourceGuid);
            return true;
        }

        private static void ApplyOwnershipLabels(AnimationClip clip, OwnerKey ownerKey)
        {
            var labels = new HashSet<string>(
                AssetDatabase.GetLabels(clip).Where(label =>
                    !label.StartsWith(SourceGuidLabelPrefix, StringComparison.Ordinal) &&
                    !label.StartsWith(GroupGuidLabelPrefix, StringComparison.Ordinal)),
                StringComparer.Ordinal)
            {
                GeneratedLabel,
                SourceGuidLabelPrefix + ownerKey.SourceGuid,
                GroupGuidLabelPrefix + ownerKey.GroupGuid,
            };
            AssetDatabase.SetLabels(clip, labels.OrderBy(label => label, StringComparer.Ordinal).ToArray());
        }

        private static bool IsAssetsPath(string path)
        {
            return path == "Assets" ||
                (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal));
        }

        private enum PlannedAction
        {
            Invalid,
            Create,
            Update,
            MoveAndUpdate,
        }

        private readonly struct OwnerKey : IEquatable<OwnerKey>
        {
            public OwnerKey(string groupGuid, string sourceGuid)
            {
                GroupGuid = groupGuid;
                SourceGuid = sourceGuid;
            }

            public string GroupGuid { get; }

            public string SourceGuid { get; }

            public bool Equals(OwnerKey other)
            {
                return string.Equals(GroupGuid, other.GroupGuid, StringComparison.Ordinal) &&
                    string.Equals(SourceGuid, other.SourceGuid, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is OwnerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((GroupGuid != null ? GroupGuid.GetHashCode() : 0) * 397) ^
                        (SourceGuid != null ? SourceGuid.GetHashCode() : 0);
                }
            }
        }

        private sealed class OwnedClip
        {
            public OwnedClip(AnimationClip clip, string path)
            {
                Clip = clip;
                Path = path;
            }

            public AnimationClip Clip { get; }

            public string Path { get; }
        }

        private sealed class GenerationOperation
        {
            public GenerationOperation(
                SpriteAnimationGroupAsset group,
                SpriteAnimationAsset source,
                OwnerKey ownerKey,
                string targetPath,
                string bindingPath)
            {
                Group = group;
                Source = source;
                OwnerKey = ownerKey;
                TargetPath = targetPath;
                BindingPath = bindingPath;
            }

            public SpriteAnimationGroupAsset Group { get; }

            public SpriteAnimationAsset Source { get; }

            public OwnerKey OwnerKey { get; }

            public string TargetPath { get; }

            public string BindingPath { get; }

            public PlannedAction Action { get; set; }

            public AnimationClip ExistingClip { get; set; }

            public string ExistingPath { get; set; }
        }
    }
}
