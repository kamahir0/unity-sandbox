# Diagnose Unity Editor UI fidelity

Use this workflow when an extension should match Unity's built-in UI or when repeated visual tweaks are not converging.

## Contents

- Establish the reference
- Classify the mismatch
- Model geometry once
- Interpret Unity style metrics
- Reproduce exact states
- Test invariants
- Avoid weak fixes

## Establish the reference

1. Confirm the exact Unity version and theme.
2. Put the extension beside the built-in control it should resemble.
3. Compare hierarchy, control type, class lists, resolved style, bounds, focus, hover, pressed, disabled, and empty states.
4. Search public Unity APIs, installed package source, public class-name constants, built-in icon names, and named `GUIStyle` values before writing USS or drawing custom chrome.
5. Treat screenshots as evidence of a symptom, not as a complete layout specification.

Prefer copying a public structural pattern over copying its pixels. If Unity's exact internal control is private, compose public controls and preserve the observable contract.

## Classify the mismatch

Fix the highest-level cause first:

| Symptom | Inspect first | Preferred correction |
| --- | --- | --- |
| Looks like the wrong kind of control | Hierarchy and stock control choice | Replace it with the native primitive or hybrid structure |
| Blue focus remains after clicking | Focusability and control ID type | Use passive focus and disable retained focus where appropriate |
| Icons look blurred or oversized | Icon source and image placement | Use built-in icons at their native size |
| Fields drift or overflow | Flex structure and resolved bounds | Use `BaseField`, aligned classes, and bounded flex containers |
| Drawn and clickable regions disagree | Independent rectangle calculations | Share one geometry result |
| Only boundary values look wrong | Center/range semantics and clipping | Define explicit endpoint invariants |
| Theme colors look subtly wrong | Hard-coded colors or stale classes | Use Unity theme styling and synchronize state classes |

## Model geometry once

For a custom-drawn composite control, calculate an immutable layout value containing every rectangle and range used by the control. Use it for:

- background and content drawing;
- button placement;
- hit testing and hot-control capture;
- pointer-to-value conversion;
- indicator placement and clipping;
- geometry tests.

Clamp all regions to the available row. Define containment and adjacency directly between complete sibling rectangles. Do not test only a scrubber rectangle that was already calculated from the same faulty constants.

For a centered indicator of width `w`, inset its valid center range by `w / 2` at both ends. Clamp normalized values and clip drawing to the visible region. This guarantees that zero and end positions remain fully visible.

## Interpret Unity style metrics

Read dimensions from the active style instead of copying screenshot measurements:

- use `fixedWidth` or `fixedHeight` when positive;
- otherwise use `CalcSize` for the actual icon or content;
- clone a fixed-width style and set `fixedWidth = 0` when a compact content-sized variant is required;
- treat `border` and `padding` as space inside the control rectangle;
- treat `margin` as layout space outside it;
- treat `overflow` as drawing outside it.

Do not create a gutter from border, padding, or indicator width. Native controls that visually meet should use only genuine external style space, normally margin or overflow. Clipping—not a guessed gap—must prevent custom drawing from entering the neighboring control.

Named IMGUI skin styles may not resolve correctly outside an `OnGUI` event. Acquire and visually validate real style metrics in the attached Editor. For pure geometry tests, inject explicit representative metrics instead of treating a test-time fallback skin as authoritative.

## Reproduce exact states

Visual bugs often exist only at a boundary. Inspect at least:

- zero and a small nonzero value;
- midpoint and final value;
- minimum practical width and two wider widths;
- normal, hover, pressed, focused, and disabled states when relevant;
- empty and populated data.

Set the state deterministically through the extension's API, serialized data, or an Editor evaluation command. Then capture the real attached UI. This is more reliable than trying to land a pointer at an exact value by eye.

When the true rectangle is unclear, temporarily fill only that rectangle with a conspicuous diagnostic color. Use the overlay to identify geometry ownership, keep it separate from interaction logic, and remove it after verification.

## Test invariants

Assert outcomes rather than implementation trivia:

- all complete sibling rectangles stay inside the row;
- adjacent controls do not overlap and intentional adjacency has no unexplained gap;
- drawing and hit-testing use the same region;
- pointer values below and above the range clamp to the endpoints;
- indicators at negative, zero, near-zero, middle, near-one, one, and greater-than-one inputs remain inside the visible region;
- layout remains valid at narrow, normal, and wide widths;
- focus, placeholder, disabled, and Undo/Redo state remains synchronized.

Combine pure geometry tests with at least one attached layout test and real Editor inspection. Screenshots alone do not prove hit regions or serialization, while self-referential geometry tests do not prove visual integration.

## Avoid weak fixes

- Do not repeatedly nudge an unexplained offset until one screenshot looks correct.
- Do not add spacing to hide overlap when containment or clipping can make overlap impossible.
- Do not hard-code dimensions already exposed by a Unity control, style, or resolved bound.
- Do not assume a subtle rendering issue is an unavoidable Unity bug before checking stale UI state and structure.
- Do not preserve the wrong stock control merely because it provides the required value semantics; its hierarchy and interaction states may make fidelity impossible.
- Do not leave diagnostic colors, reflection-only test hooks, or screenshot-specific constants in production code.

Finish only when the structural cause is understood, invariant tests pass, the real Editor matches in boundary states, and no unexplained offset remains.
