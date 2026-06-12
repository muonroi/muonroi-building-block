# Phase 8.10 — ExcludedShapes Float Refactor (PLAN)

> **Split note:** Phase 8.10 is the ExcludedShapes float refactor — split out of original 8.8 plan
> on 2026-05-28 to keep phases single-theme. Predecessors: 8.8 (HSLA_E float-child fix), 8.9
> (visual primitives — table border, checkbox, form underline). 8.10 is a byte-identical
> algorithmic refactor with no visual change.

> Design date: 2026-05-28. No code is modified here — research + plan only.
> Target algorithm: WeasyPrint `avoid_collisions` / `excluded_shapes` list model.
> Spec: CSS 2.1 §9.5 https://www.w3.org/TR/CSS21/visuren.html#floats

---

## 1. Current Code Map — Cursor Read/Write Sites

All sites that touch `LeftFloatRight`, `RightFloatLeft`, `LeftFloatBottom`, `RightFloatBottom`.
`ContentOriginX` is NOT in scope — it has a different purpose (cell column X) and is kept as-is.

| # | File | Line(s) | Field(s) | Operation | Notes |
|---|------|---------|----------|-----------|-------|
| W1 | `LayoutContext.cs` | 42 | `LeftFloatRight` | Declare | Scalar cursor — right edge of last left-float |
| W2 | `LayoutContext.cs` | 44 | `RightFloatLeft` | Declare | Scalar cursor — left edge of last right-float |
| W3 | `LayoutContext.cs` | 46 | `LeftFloatBottom` | Declare | Scalar cursor — bottom of last left-float |
| W4 | `LayoutContext.cs` | 48 | `RightFloatBottom` | Declare | Scalar cursor — bottom of last right-float |
| W5 | `BlockLayoutEngine.cs` | 82–86 | All four | Reset to 0f | BFC root entry — resets float state |
| W6 | `BlockLayoutEngine.cs` | 89–93 | All four | Copy from parent ctx | Non-BFC propagation into child ctx |
| W7 | `BlockLayoutEngine.cs` | 114–117 | `LeftFloatBottom`, `RightFloatBottom` | Read | `clear:left/right/both` advance |
| W8 | `BlockLayoutEngine.cs` | 129–130 | `LeftFloatBottom`, `RightFloatBottom` | Read | Float container height contribution |
| W9 | `BlockLayoutEngine.cs` | 212–213 | `LeftFloatRight` | Read + Write | Left-float X origin; update cursor after place |
| W10 | `BlockLayoutEngine.cs` | 220–221 | `LeftFloatRight`, `LeftFloatBottom` | Write | Post-place cursor update for left-float |
| W11 | `BlockLayoutEngine.cs` | 226–228 | `RightFloatLeft` | Read + Write | Right-float X origin; update cursor after place |
| W12 | `BlockLayoutEngine.cs` | 236–237 | `RightFloatLeft`, `RightFloatBottom` | Write | Post-place cursor update for right-float |
| W13 | `BlockLayoutEngine.cs` | 269–272 | `LeftFloatRight` | Read | Normal-flow block child X origin (post-float offset) |
| W14 | `InlineLayoutEngine.cs` | 19–23 | `LeftFloatRight`, `RightFloatLeft` | Read | Line-box X/width derived from float cursors |

---

## 2. New Types — C# Signatures

### 2a. `FloatSide` enum

```csharp
// src/Muonroi.Pdf/Internal/Layout/FloatSide.cs
namespace Muonroi.Pdf.Internal.Layout;

internal enum FloatSide { Left, Right }
```

### 2b. `FloatExclusion` record (immutable)

```csharp
// src/Muonroi.Pdf/Internal/Layout/FloatExclusion.cs
namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// An immutable bounding rect for a placed float, stored in the BFC exclusion list.
/// Mirrors WeasyPrint's placed float record used in avoid_collisions (float.py ~line 43).
/// All coordinates are in points, absolute page space within the current BFC.
/// </summary>
internal readonly record struct FloatExclusion(
    float Left,
    float Top,
    float Right,
    float Bottom,
    FloatSide Side
);
```

### 2c. `ContainingBlock` value type (passed to solver)

```csharp
// src/Muonroi.Pdf/Internal/Layout/FloatPlacementSolver.cs (nested or separate)
namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// Minimal containing-block info needed by the float solver.
/// Matches WeasyPrint's cb.content_box_x / cb.width parameters.
/// </summary>
internal readonly record struct ContainingBlock(float X, float Width);
```

### 2d. `FloatPlacementSolver` static helper

```csharp
// src/Muonroi.Pdf/Internal/Layout/FloatPlacementSolver.cs
namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// Clean-room derivation of WeasyPrint's avoid_collisions algorithm (float.py ~lines 116-189).
/// Operates on an immutable snapshot of FloatExclusion; never mutates the list.
/// All coordinate arithmetic is in points.
/// </summary>
internal static class FloatPlacementSolver
{
    /// <summary>
    /// Computes the final (x, y, availableWidth) for a float box to be placed,
    /// advancing candidateY until the float fits horizontally.
    /// Returns the resolved placement; caller appends a new FloatExclusion to the list.
    /// </summary>
    /// <param name="candidateY">Starting Y (top of current line / normal-flow cursor).</param>
    /// <param name="boxWidth">Used width of the float (including margins).</param>
    /// <param name="boxHeight">Used height of the float (pre-computed).</param>
    /// <param name="side">Left or Right float.</param>
    /// <param name="cb">Containing block X and width for this BFC.</param>
    /// <param name="exclusions">Current list of placed floats in this BFC.</param>
    /// <returns>Resolved (x, y, availableWidth) for the float's content-box left edge.</returns>
    public static (float X, float Y, float AvailableWidth) AvoidCollisions(
        float candidateY,
        float boxWidth,
        float boxHeight,
        FloatSide side,
        ContainingBlock cb,
        IReadOnlyList<FloatExclusion> exclusions);

    /// <summary>
    /// Returns (startX, availableWidth) for a line box at the given Y with the given height.
    /// Equivalent to WeasyPrint inline.py's call to avoid_collisions for linebox width.
    /// </summary>
    /// <param name="lineY">Top Y of the line box.</param>
    /// <param name="lineHeight">Height of the line box (typically one line-height unit).</param>
    /// <param name="cb">Containing block.</param>
    /// <param name="exclusions">Current exclusion list.</param>
    /// <returns>(startX, availableWidth) — the usable horizontal band for this line.</returns>
    public static (float StartX, float AvailableWidth) AvailableWidthAtY(
        float lineY,
        float lineHeight,
        ContainingBlock cb,
        IReadOnlyList<FloatExclusion> exclusions);

    /// <summary>
    /// Returns the Y below which all exclusions on the given side have ended —
    /// used to implement clear:left / clear:right / clear:both.
    /// </summary>
    public static float ClearY(FloatSide? side, IReadOnlyList<FloatExclusion> exclusions);
}
```

### 2e. `LayoutContext` additions

```csharp
// Replace four cursor fields with one list; keep ContentOriginX unchanged.

/// <summary>
/// Placed floats in the current BFC. Populated by BlockLayoutEngine float placement;
/// queried by FloatPlacementSolver for every subsequent float or line box.
/// Lifecycle: cleared when entering a BFC root (same reset point as the four old cursor fields).
/// Per-phase scope: single BFC per RunLayout call — nested BFC stacks deferred to Phase 8.9.
/// </summary>
public List<FloatExclusion> Exclusions { get; set; } = new();

// REMOVED (replaced by Exclusions):
// public float LeftFloatRight { get; set; }
// public float RightFloatLeft { get; set; }
// public float LeftFloatBottom { get; set; }
// public float RightFloatBottom { get; set; }
```

---

## 3. Migration Steps — 6 Atomic Commits

Each step must compile and all existing template regression tests must pass before proceeding.

### Step 1 — Add new types, no behavior change (Feature-flag isolated)

- Add `FloatSide.cs`, `FloatExclusion.cs`.
- Add `FloatPlacementSolver.cs` with method stubs that `throw new NotImplementedException()`.
- Add `Exclusions` property to `LayoutContext` alongside the four existing cursor fields (both coexist).
- No call sites changed. Build passes; all tests pass.
- **Commit message:** `feat(layout): add FloatExclusion + FloatPlacementSolver stubs (8.10 step 1)`

### Step 2 — Implement FloatPlacementSolver; unit test with synthetic lists

- Implement `AvoidCollisions`, `AvailableWidthAtY`, `ClearY` in `FloatPlacementSolver`.
- Inner loop mirrors WeasyPrint pseudocode from RESEARCH-OSS-REFS.md §1:
  - Collect `left_bounds` = right edges of left-floats overlapping `[candidateY, candidateY+boxHeight)`.
  - Collect `right_bounds` = left edges of right-floats in the same Y band.
  - `maxLeft = max(cb.X, max(left_bounds))`, `minRight = min(cb.X + cb.Width, min(right_bounds))`.
  - If `minRight - maxLeft >= boxWidth` → done. Else `candidateY = min(bottom of colliding shapes)` and retry.
  - Cap loop iterations at `exclusions.Count + 1` to prevent infinite loop on degenerate input.
- New unit tests in `Muonroi.Pdf.Tests/Layout/FloatPlacementSolverTests.cs`:
  - Single left-float, second left-float stacks horizontally.
  - Tall left-float A forces short left-float B below A when widths don't fit on same row.
  - Mixed left+right floats reduce available width.
  - `ClearY` returns correct bottom for each side.
  - Empty list → `(cb.X, cb.Width)` for `AvailableWidthAtY`.
- **Commit message:** `feat(layout): implement FloatPlacementSolver with unit tests (8.10 step 2)`

### Step 3 — Mirror cursor writes into Exclusions list (cursor still authoritative)

- In `BlockLayoutEngine` float placement (W9/W10 for left; W11/W12 for right):
  - After existing cursor writes, also do:
    ```csharp
    ctx.Exclusions.Add(new FloatExclusion(floatX, floatY, floatX + floatWidth, floatY + floatHeight, FloatSide.Left/Right));
    ```
- In BFC root reset (W5): also reset `ctx.Exclusions = new List<FloatExclusion>()`.
- In BFC propagation (W6): also propagate `ctx.Exclusions = context.Exclusions` (same reference — shared within BFC).
- All reads still use cursor fields. Both systems run in parallel; no behavior change.
- Run all 17 template regression snapshots. Diff must be empty.
- **Commit message:** `feat(layout): mirror float placements into Exclusions list alongside cursors (8.10 step 3)`

### Step 4 — Flip reads to Exclusions; cursors become write-only

- In `BlockLayoutEngine`:
  - `clear:` handling (W7): replace `childContext.LeftFloatBottom` / `childContext.RightFloatBottom` reads with `FloatPlacementSolver.ClearY(FloatSide.Left/Right/null, ctx.Exclusions)`.
  - Float container height (W8): replace with `ctx.Exclusions.Count > 0 ? ctx.Exclusions.Max(e => e.Bottom) : childY` (or a dedicated `MaxBottom` helper).
  - Left-float X origin (W9): replace `ctx.LeftFloatRight` read inside `AvoidCollisions` call.
  - Right-float X origin (W11): replace `ctx.RightFloatLeft` read.
  - Normal-flow block X (W13): replace `ctx.LeftFloatRight` read with `FloatPlacementSolver.AvailableWidthAtY(startY, 0f, cb, ctx.Exclusions).StartX`.
- In `InlineLayoutEngine` (W14): replace both `context.LeftFloatRight` and `context.RightFloatLeft` reads with a single `FloatPlacementSolver.AvailableWidthAtY(lineY, lineHeight, cb, context.Exclusions)` call.
  - Note: `lineHeight` is now needed at the call site; use the dominant box line height (already computed in `CommitLine`). For the outer loop pre-check, use a single-line estimate (first box in pending list or fallback `12pt`).
- Cursor fields are still written but never read. All tests must still pass.
- **Commit message:** `feat(layout): flip all float reads to FloatPlacementSolver (8.10 step 4)`

### Step 5 — Remove cursor fields; Exclusions is sole source of truth

- Delete `LeftFloatRight`, `RightFloatLeft`, `LeftFloatBottom`, `RightFloatBottom` from `LayoutContext`.
- Remove all writes to the deleted fields (W5/W6 partial, W10, W12).
- Build must succeed with zero warnings.
- Run full regression suite; snapshot diff must be empty.
- **Commit message:** `refactor(layout): remove cursor fields; Exclusions list is sole float state (8.10 step 5)`

### Step 6 — Add `clear:` support via ClearY + test

- `clear:left/right/both` at W7 already calls `ClearY` from Step 4 — this step adds test coverage.
- New tests in `FloatPlacementSolverTests`:
  - `clear:left` with two left-floats at different heights — `candidateY` advances to max left-float bottom.
  - `clear:both` with one left + one right float at different heights — advances to max of both.
  - `clear:right` with no right floats — no advance.
- If `ClearValue` property not yet parsed (check `BoxNode.ClearValue`): add CSS parser support for `clear` property in `BoxTreeBuilder.ResolveCssProperties` if missing.
- **Commit message:** `feat(layout): add clear:left/right/both tests and verify ClearY behavior (8.10 step 6)`

---

## 4. Test Strategy

### Unit tests (new — `FloatPlacementSolverTests.cs`)

| Test | Scenario |
|------|----------|
| `LeftFloat_Single` | Single left-float placed at cb.X |
| `LeftFloat_Stack_Horizontal` | Two left-floats same row — second starts at right edge of first |
| `LeftFloat_DropToNextRow` | Two left-floats too wide for row — second drops below first |
| `RightFloat_Single` | Single right-float placed at cb.X + cb.Width - width |
| `RightFloat_Stack` | Two right-floats — second stacks left of first |
| `MixedFloats_AvailWidth` | One left + one right — available width for line reduced |
| `TallLeft_ShortRight_DifferentBands` | Short right-float placed below tall left-float bottom |
| `AvailableWidthAtY_NoExclusions` | Returns full cb width when list empty |
| `AvailableWidthAtY_WithFloats` | Correct narrowing for a given Y band |
| `ClearY_Left` | Max bottom of left-floats returned |
| `ClearY_Right` | Max bottom of right-floats returned |
| `ClearY_Both` | Max of all float bottoms returned |
| `ClearY_EmptyList` | Returns 0f |
| `AvoidCollisions_InfiniteLoopGuard` | Degenerate: all space taken; loop capped; returns something |

### Regression baseline (17 templates + HSLA_E)

- Before Step 3: capture snapshot of all 17 existing templates as PDF byte-equivalent or coordinate JSON.
- Each subsequent step: assert no coordinate delta > 0.5pt on any element.
- HSLA_E specifically: assert three float columns are non-overlapping and their X coordinates match `originX`, `originX + col1Width`, `originX + col1Width + col2Width`.
- HSLA_F (table-based): assert no regression — table layout does not use float cursors so should be unaffected.

---

## 5. Risk Register

| Rank | Risk | Likelihood | Impact | Mitigation |
|------|------|-----------|--------|------------|
| R1 | **Line height unknown at outer InlineLayoutEngine loop entry** — `AvailableWidthAtY` needs `lineHeight` but it is not known until `CommitLine`. | High | Medium | Use 0f for height in initial width query (conservative — only current-Y snapshot matters for non-overlapping floats). Re-query at `CommitLine` time if width changes mid-line. Accept minor over-constraint on first token of each line; adjust in Step 4 follow-on. |
| R2 | **O(n) scan performance** — exclusion list grows with every float; scan is O(n) per line box. For pathological docs with many floats (n > 200) this degrades quadratically. | Low for v1 templates (n ≤ 10) | Low for v1 | Bound n: document that the solver is O(n×lines) and add a guard cap of 512 exclusions (evict oldest when exceeded, safe because old floats are above the current page Y). Add a perf unit test asserting <1ms for n=100. |
| R3 | **Nested BFC stacks not supported** — per scope cut below, one shared `Exclusions` list per `RunLayout`. Floats inside inline-block or overflow:hidden children leak into sibling BFCs. | Medium | Medium | At Step 3, add a `TODO(8.9)` comment at the BFC-root reset site. The reset already clears the list (same as the cursor reset), which is correct behavior for the root BFC. Nested BFC isolation requires pushing/popping a list stack — deferred. |
| R4 | **Page-break float state** — floats placed on page N must not constrain line boxes on page N+1. Current cursor model has no page-break awareness either. | Medium | Low for current templates (no float spans page break) | Accept same limitation as cursor model for 8.8. Add `TODO(8.9)`: clear exclusions at each page break event in `PaginationEngine`. |
| R5 | **`clear:` + Exclusions disagreement during Step 3** — both systems run in parallel; if `ClearY` returns a different value than `LeftFloatBottom` cursor, `clear:` behavior will be inconsistent until Step 4 flips the read. | High during Step 3 | Low (Step 3 is a mirror step; reads not yet flipped) | Do not flip `clear:` reads in Step 3. Keep all reads on cursor through Step 3 so the two systems diverge silently in Step 3 but Step 4 is the single flip point. Regression suite catches regressions. |

---

## 6. Scope Cut — Explicitly OUT of Phase 8.10

The following are deferred and must NOT be implemented in 8.8 commits:

1. **Nested BFC list stacks** — `overflow:hidden`, `inline-block`, and nested `position:relative` children each establish a new BFC with their own float scope. 8.8 uses one shared list per `RunLayout`. Full stack support deferred to **Phase 8.9**.

2. **`position:absolute` float interaction** — abs-pos elements are already handled as a deferred post-pass; they do not interact with the exclusion list. No change. Deferred to **Phase 8.9** if interaction is needed.

3. **Page-break-inside floats** — a float that straddles a page break requires exclusion list persistence (or split) across page boundaries. Not supported; deferred to **Phase 8.9**.

4. **Shrink-to-fit float width with auto** — `width:auto` floats require min-content width pre-computation before `AvoidCollisions` is called (chicken-and-egg). Current code uses `ResolveWidth` which already handles percentage and explicit widths. True shrink-to-fit for `auto` floats deferred to **Phase 8.9** (requires inline measurement pass).

5. **Float interaction with multi-column layout (CSS `column-count`)** — not in scope for Muonroi.Pdf v1 Legacy Print-HTML Profile. Deferred indefinitely.
