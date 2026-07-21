---
name: build-unity-editor-extensions
description: Build, modify, diagnose, or review Unity Editor extensions with native Unity appearance, interaction, serialization, Undo/Redo, and tests. Use for custom Inspectors, EditorWindows, PropertyDrawers, UI Toolkit UXML/USS, IMGUI, serialized arrays or lists, reorderable controls, custom previews, editor tooling, and any request to make an extension feel like Unity's default UI.
---

# Build Unity Editor Extensions

## Objective

Treat Unity-default appearance and behavior as the baseline. Prefer composing Unity's public Editor and UI Toolkit controls over recreating them visually. Preserve serialization, selection, keyboard navigation, focus, drag-and-drop, Undo/Redo, theme support, and Inspector resizing.

## Start with evidence

1. Read `ProjectSettings/ProjectVersion.txt` and relevant assembly definitions before choosing APIs.
2. Inspect the existing editor code, UXML, USS, tests, and serialized model. Preserve public APIs and stored data unless the request explicitly changes them.
3. Inspect the same control in the running Unity version when visual or behavioral fidelity matters. Use the current Inspector, installed Unity package source, public USS constants, UI hierarchy, and resolved bounds as evidence.
4. Check the worktree before editing. Preserve unrelated changes and never save or discard an open modified scene merely to unblock tests.

## Choose the most native implementation

Use this order of preference:

1. Draw a `SerializedProperty` with `PropertyField` or the default Inspector.
2. Configure a stock UI Toolkit control such as `ListView`, `TreeView`, `Toolbar`, `Foldout`, `ObjectField`, or numeric field.
3. Compose stock controls inside a custom `BaseField<T>` so label alignment and input layout remain native.
4. Add minimal USS for layout only.
5. Use custom visuals or IMGUI drawing only when the required behavior cannot be expressed with the preceding options or the surrounding code is intentionally IMGUI.

Do not imitate Unity UI with arbitrary pixels, unicode symbols, fixed theme colors, or home-grown selection and focus behavior when a public Unity primitive exists. Use built-in icons through `EditorGUIUtility.IconContent` and public USS class-name constants where available.

For concrete implementation patterns, read [references/native-ui-patterns.md](references/native-ui-patterns.md) whenever the task involves UI Toolkit, custom fields, collections, previews, placeholder text, or responsive Inspector layout.

When success depends on matching Unity's appearance or interaction, or when a visual fix starts turning into pixel nudging, read [references/visual-fidelity-debugging.md](references/visual-fidelity-debugging.md) before editing.

## Solve fidelity problems structurally

1. Classify the mismatch as structure, stock-control choice, skin/style state, focus, layout geometry, clipping, or serialized state.
2. Define measurable invariants such as containment, adjacency, alignment, focus behavior, and state transitions before changing offsets.
3. Use one authoritative layout result for drawing, hit testing, value conversion, and tests. Derive dimensions from the active Unity control or style.
4. Validate exact boundary states in the real Editor: empty, disabled, focused, zero, near-zero, end, narrow, and wide as applicable.
5. Add a pixel constant only when it represents an intentional design dimension that cannot be obtained from a public control, style, or resolved bound.

## Implement around serialized state

- Use `SerializedObject` and `SerializedProperty` for Inspector data whenever possible.
- Bind declarative UXML once, then use `SetValueWithoutNotify` and refresh guards for derived or multi-property controls.
- Initialize every serialized member of newly inserted collection elements; Unity may clone the previous element during array growth.
- Keep UI state derived from serialized state. Rebuild or refresh after Undo/Redo, reorder, sort, add, remove, migration, and external drops.
- Subscribe to `Undo.undoRedoPerformed` in `OnEnable` and unsubscribe in `OnDisable` when custom UI state needs refreshing.
- Group continuous text editing into one named Undo operation. Avoid an Undo step per digit.
- Use `Undo.RecordObject` before direct object mutation; prefer `SerializedProperty.ApplyModifiedProperties` when already operating through serialized properties.

## Preserve native interaction

Verify the extension behaves correctly with:

- mouse, keyboard, focus traversal, and delayed field commit;
- narrow and wide Inspector layouts;
- Light and Dark themes without custom color assumptions;
- empty, small, and large collections;
- multi-selection when supported;
- add, remove, reorder, sort, object picker, and external drag-and-drop;
- domain reload, asset reselection, Undo, and Redo;
- collapsed sections without residual content height;
- previews at minimum, intermediate, and maximum heights.

When exact native behavior conflicts with a requested workflow, retain native visuals and interaction where possible, document the necessary deviation, and cover it with tests.

## Validate in the real Editor

Compile early after introducing UXML elements or version-specific APIs. Attach important UI to an actual `InspectorElement` or `EditorWindow`; detached visual trees do not reproduce every focus, delayed-input, style, layout, or scheduler behavior.

Before completing implementation, read and follow [references/validation.md](references/validation.md). Scale validation to the change, but do not skip compilation, relevant EditMode tests, and `git diff --check`.

## Review checklist

Before handing off, confirm:

- The implementation uses the highest available native abstraction.
- Labels and value regions align with neighboring Unity fields.
- Controls do not overflow their row or overlap at supported widths.
- Visual fixes are backed by structural or geometric invariants rather than screenshot-specific offsets.
- Styling uses Unity classes or theme-derived styles rather than Dark-theme constants.
- UI callbacks cannot recursively mutate serialized state.
- Undo/Redo restores both data and displayed UI.
- New or resized collection elements receive intentional default values.
- Tests exercise behavior and attached layout, not only static class names.
- Compilation reports zero errors and warnings.
