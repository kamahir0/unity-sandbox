# Native Unity UI patterns

Use these patterns as starting points, then verify them against the project's exact Unity version. Public APIs and built-in hierarchy details can change between releases.

## Contents

- Field alignment
- Serialized collections and collection size fields
- IMGUI fallback
- Placeholder and optional-value fields
- Responsive row layout
- Native Inspector preview
- Visual fidelity investigation

## Field alignment

- Prefer `PropertyField` for a single serialized property.
- Add `BaseField<T>.alignedFieldUssClassName` (`unity-base-field__aligned`) to custom fields that belong in an Inspector.
- Build a composite field by deriving from `BaseField<T>` and placing controls inside its `visualInput` container. Do not place an independent `Label`, `Toggle`, and input in a flex row and manually guess the Inspector label width.
- Mark reusable UI Toolkit elements with `[UxmlElement]` in supported Unity versions so UXML remains declarative.
- Let the input container grow and shrink; set `min-width: 0` on nested flex containers that otherwise force horizontal overflow.
- Use stock one-line field sizing and margins. Add fixed height only for intentionally larger rows such as thumbnail rows.

Example composite structure:

```csharp
[UxmlElement]
internal partial class OptionalIntField : BaseField<int>
{
    public Toggle EnabledToggle { get; }
    public IntegerField IntegerField { get; }

    public OptionalIntField() : this(null) { }

    public OptionalIntField(string label)
        : base(label, new VisualElement())
    {
        AddToClassList(alignedFieldUssClassName);
        visualInput.style.flexDirection = FlexDirection.Row;
        EnabledToggle = new Toggle();
        IntegerField = new IntegerField { isDelayed = false };
        visualInput.Add(EnabledToggle);
        visualInput.Add(IntegerField);
    }
}
```

Keep the composite value semantics explicit; a custom `BaseField<T>` does not automatically serialize multiple child properties.

## Serialized collections

Start with a default `PropertyField` for the array or list. Use a custom `ListView` only when rows, reorder behavior, external drops, menus, or derived controls require it.

For a native custom `ListView`, normally configure:

```csharp
list.showFoldoutHeader = true;
list.showBorder = true;
list.showAddRemoveFooter = true;
list.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
list.reorderable = true;
list.reorderMode = ListViewReorderMode.Animated;
```

Additional guidance:

- Use Unity's Foldout header instead of placing a separate title above the list.
- Put secondary actions in a `_Menu` `ToolbarMenu` at the header's right side. Stop pointer/click propagation so opening the menu does not toggle the Foldout.
- Use the standard `+/-` footer. Disable remove for an empty list.
- Use outer Inspector scrolling for a collection intended to behave like a serialized array. Add an internal height limit only when the product explicitly calls for a nested scrolling region.
- Persist expansion state through `SerializedProperty.isExpanded`, `SessionState`, or another intentional state owner.
- On add or growth, explicitly clear object references and initialize booleans, numeric defaults, and nested values.
- On reorder and sort, move or copy the whole logical row, not only the visually dominant property.
- Refresh item-source indices after every structural mutation.

### Custom collection size field

If a custom list cannot safely use `showBoundCollectionSize`, reproduce the public standard size-field contract rather than inventing a new header control:

```csharp
var sizeField = new TextField
{
    name = BaseListView.arraySizeFieldUssClassName,
    isDelayed = true,
};
sizeField.AddToClassList(BaseListView.arraySizeFieldUssClassName);
sizeField.AddToClassList(BaseListView.arraySizeFieldWithHeaderUssClassName);
sizeField.AddToClassList(BaseListView.arraySizeFieldWithFooterUssClassName);
list.hierarchy.Add(sizeField);
```

Register a value callback that parses a nonnegative count, updates the serialized array, initializes new elements, applies changes, rebuilds the list, and refreshes the displayed count after Undo/Redo. Verify the direct-child hierarchy and class names in the target Unity version.

## IMGUI fallback

Keep an established IMGUI extension in IMGUI unless conversion is part of the request or materially simplifies the feature. Preserve native behavior with:

- `EditorGUILayout.PropertyField` or `EditorGUI.PropertyField` for serialized values and collections;
- `EditorGUI.BeginProperty`/`EndProperty` in `PropertyDrawer` implementations;
- `EditorGUIUtility.singleLineHeight` and `EditorGUIUtility.standardVerticalSpacing` instead of guessed row sizes;
- `EditorGUILayout.GetControlRect` and `EditorGUI.PrefixLabel` for custom aligned rows;
- `EditorGUI.indentLevel` and `EditorGUI.showMixedValue` for nesting and multi-object state;
- `serializedObject.Update`/`ApplyModifiedProperties` for persistence and Undo.

Use `UnityEditorInternal.ReorderableList` only when the project already accepts the version-coupling risk of `UnityEditorInternal`, or when the target Unity version has been fixed and tested. Prefer default serialized collection drawing or a UI Toolkit `ListView` for new code.

For legacy previews, use `OnPreviewGUI(Rect, GUIStyle)` and `OnPreviewSettings()`. Prefer `CreatePreview` only after confirming it exists and behaves correctly in the project's Unity version.

## Placeholder and optional-value fields

Do not implement placeholder text by assigning a fixed gray color. Use `TextInputBaseField<T>.textInputUssName`, `placeholderUssClassName`, and `textEdition.placeholder` so Unity supplies theme styling.

Be careful when clearing the inner `TextElement` while retaining an `IntegerField.value`. A later `SetValueWithoutNotify` with the same numeric value can short-circuit, leaving UI Toolkit's placeholder class active even though the text now represents a real value. This can look like a Unity theme bug but is stale internal field state.

Prefer redesigning the state flow so the normal field formatter owns the text. If inherited display requires manually clearing the inner text, force a genuine value transition before restoring the stored value when switching to an actual value, then synchronize the placeholder class explicitly:

```csharp
int temporary = value == int.MaxValue ? value - 1 : value + 1;
field.textEdition.placeholder = string.Empty;
field.SetValueWithoutNotify(temporary);
field.SetValueWithoutNotify(value);
textInput.EnableInClassList(
    TextInputBaseField<int>.placeholderUssClassName,
    false);
```

Cover both transitions with a regression test: inherited to actual without focusing the field, and actual back to inherited.

## Responsive row layout

- Keep a fixed-height list row's children inside its world bounds.
- Remove accidental vertical `flex-grow` from one-line fields.
- Reserve thumbnail size intentionally and let the field region flex.
- Compare `worldBound.x` of input containers, not label text, when testing alignment.
- In animated reorder mode, inspect the built-in handle bars. Center the handle on the complete custom row when a multi-line row makes the default placement look top-aligned.
- Test at the project's practical minimum Inspector width and at two wider widths. Check `xMax`, row bounds, and absence of horizontal scrolling.

## Native Inspector preview

Use the Editor preview surface instead of drawing a preview-like box inside the Inspector:

- Override `HasPreviewGUI()` and `GetPreviewTitle()`.
- For UI Toolkit, implement `CreatePreview(VisualElement inspectorPreviewWindow)` when supported.
- Query Unity's preview `toolbar` and `content-container`; add play, pause, and frame-step buttons to the toolbar.
- Add controls with built-in icon names such as `PlayButton`, `PauseButton`, `Animation.PrevKey`, and `Animation.NextKey`.
- Set preview content to `flex-grow: 1` and `min-height: 0`.
- Set the image/viewport to grow, and keep the slider/status footer `flex-shrink: 0`, so resizing never creates unexplained empty space or hides controls.
- Pause scheduled playback on detach and disable transport controls when no valid frame exists.

## Visual fidelity investigation

When a control looks subtly wrong:

1. Place the target extension beside Unity's standard equivalent.
2. Inspect visual hierarchy, class lists, resolved styles, and `worldBound` values.
3. Search installed package sources and Unity's public class-name constants before adding custom USS.
4. Determine whether the difference comes from structure, built-in state, flex sizing, margin, or theme—not just color.
5. Fix the highest-level cause and add a layout or state-transition test.
