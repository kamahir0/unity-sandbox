# Unity Editor extension validation

## Validation sequence

1. Read the active Unity version and confirm version-specific APIs compile.
2. Compile in the running Editor.
3. Run focused EditMode tests while iterating.
4. Run the complete relevant EditMode assembly before handoff.
5. Exercise the UI in a real attached Inspector or EditorWindow.
6. Inspect the running Editor visually when the request concerns appearance or interaction.
7. Run `git diff --check` and review only the intended files.

## UniCli

When UniCli is installed and connected, prefer it for fast feedback:

```sh
unicli status
unicli exec Compile '{}'
unicli exec TestRunner.RunEditMode '{"assemblies":["My.Editor.Tests"]}'
```

Use `unicli commands` to discover the exact commands and parameters exposed by the installed server. Require compilation to report `0 errors, 0 warnings`.

If Test Runner is blocked by a modified-scene prompt, choose Cancel. Do not save or discard the user's scene. If the runner remains busy, validate from a temporary project copy containing `Assets`, `Packages`, and `ProjectSettings` instead of mutating the original Editor state.

## BatchMode fallback

Run EditMode tests without `-quit`; Unity Test Framework exits when the run completes:

```sh
/path/to/Unity \
  -batchmode \
  -projectPath /tmp/project-copy \
  -runTests \
  -testPlatform EditMode \
  -testResults /tmp/project-copy/test-results.xml \
  -logFile /tmp/project-copy/unity-test.log
```

Avoid `-nographics` for tests that open an `EditorWindow` or initialize a view; it can produce `No graphic device is available to initialize the view` even when the UI code is correct. Use `-nographics` only for suites that do not need attached graphics-backed UI.

## Behavioral tests

Cover the serialized contract:

- add, remove, resize, reorder, and sort;
- correct initialization of inserted rows;
- direct input and delayed input commit;
- mixed-value and disabled states;
- Undo and Redo for single actions and multi-digit editing;
- domain reload or editor reconstruction where relevant;
- migration of old serialized assets when the schema changed.

Test callbacks through the public UI path when practical. If a detached UI Toolkit element does not dispatch focus, scheduling, or delayed-input behavior realistically, attach it or invoke the narrow internal operation only for data-level coverage; do not infer attached UI behavior from the detached tree.

## Attached layout tests

Create an `EditorWindow`, add an actual `InspectorElement(editor)`, show the window, and yield at least one update before reading geometry. Test:

- minimum, normal, and wide Inspector widths;
- empty, one-item, typical, and large collections;
- input-region alignment within one physical pixel where appropriate;
- every child field within row bounds;
- header actions and collection size field without overlap;
- reorder-handle center relative to the full row;
- collapsed content with zero residual height;
- outer Inspector scrolling and no unintended horizontal scrolling;
- preview at minimum, middle, and maximum heights.

Use public class-name constants and stable element names in queries. Geometry and behavior assertions are more valuable than tests that only assert a USS class exists.

## Visual review

Compare against the same control in the current Unity Editor at 100% scale. Inspect both Light and Dark themes when theme-related styles changed. Check hover, pressed, focused, selected, mixed-value, disabled, empty, and error states—not only the resting screenshot.

When operating the live Editor, avoid changing or closing user content outside the requested scope. A screenshot is evidence, not a substitute for serialization and layout tests.

## Final evidence

Report:

- Unity version used;
- compilation error/warning count;
- focused and full test totals;
- visual/layout checks performed and any environment limitation;
- `git diff --check` result;
- any intentional deviation from native Unity behavior.
