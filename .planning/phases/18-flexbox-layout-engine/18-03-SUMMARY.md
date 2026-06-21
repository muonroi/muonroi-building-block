---
phase: 18-flexbox-layout-engine
plan: 03
subsystem: pdf-layout
tags: [flexbox, layout-engine, css, FLEX-06]
requires:
  - "18-02: FlexContainerBox + flex-item props on BoxNode + BoxTreeBuilder flag wiring"
provides:
  - "FlexLayoutEngine — CSS Flexbox resolution algorithm (FLEX-06)"
  - "BlockLayoutEngine.DispatchLayout case FlexContainerBox"
  - "LayoutEngine ctor wiring of FlexEngine"
affects:
  - "src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs"
  - "src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs"
tech-stack:
  added: []
  patterns:
    - "non-block engine driven from DispatchLayout, emitting PositionedElements + recursing via _blockEngine.Layout (mirrors TableLayoutEngine)"
    - "post-construction property wiring to break ctor cycle (FlexEngine like TableEngine)"
    - "max-content measurement pass for intrinsic main size"
key-files:
  created:
    - "src/Muonroi.Pdf/Internal/Layout/FlexLayoutEngine.cs"
    - "tests/Muonroi.Pdf.Tests/Layout/FlexLayoutEngineSmokeTests.cs"
  modified:
    - "src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs"
    - "src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs"
decisions:
  - "ROW intrinsic main width resolved via a CONCRETE max-content pass (max emitted right-edge X − originX), NOT deferred to basis:0"
  - "Container explicit Width (row) / Height (column) is used as the main-axis size, falling back to context.AvailableWidth / RemainingHeight"
metrics:
  duration: "~6 min"
  completed: "2026-06-21"
  tasks: 2
  files: 4
---

# Phase 18 Plan 03: Flexbox Layout Engine Summary

Implemented `FlexLayoutEngine` — the real CSS Flexbox resolution algorithm (basis, line wrapping, frozen-item grow/shrink, justify-content incl. space-*, align-items/self/content incl. stretch, gap, order, column + reverse directions) as a non-block engine driven from `BlockLayoutEngine.DispatchLayout`, recursing each item through the existing dispatch so nested flex/block/inline/table compose; wired via a `case FlexContainerBox` and `LayoutEngine` ctor post-construction.

## What Was Built

### Task 1 — FlexLayoutEngine (commit a4ca358b)
`src/Muonroi.Pdf/Internal/Layout/FlexLayoutEngine.cs` — `internal sealed class FlexLayoutEngine`, ctor `FlexLayoutEngine(BlockLayoutEngine)`.

Public entry:
- `public float Layout(FlexContainerBox container, LayoutContext context, List<PositionedElement> output, int pageIndex)` — mirrors `TableLayoutEngine.Layout` signature; returns the container cross extent and advances `context.CurrentY = startY + totalHeight`.

Private helper / step structure (all deterministic, unit-testable for Plan 04):
1. Axis flags (`isRow`, `reverseMain`, `wrap`, `reverseCross`) from `FlexDirection`/`FlexWrap`.
2. `containerMain` = explicit container `Width` (row) / `Height` (column) else `AvailableWidth` / `RemainingHeight`.
3. **order** — `Children` sorted by `(Order ?? 0)` with a stable original-index tiebreak.
4. `ResolveItem` → `ResolveBasis` → `MeasureContent` — per-item flex-basis + cross size.
5. `BuildLines` — nowrap = single line; wrap = new line when running main size + gap exceeds container main.
6. `ResolveFlexibleLengths` — frozen-item iteration (bounded to item count, T-18-05); free>0 grow by `flex-grow`, free<0 shrink by `flex-shrink × basis`, min clamp at 0 freezes the item.
7. `ApplyAlignContent` — multi-line cross distribution (flex-start/end/center/space-between/around/stretch); single-line stretch fills the container cross.
8. `MainAxisPositions` — justify-content (flex-start/end/center/space-between/around/evenly) + gap; reverse mirrors within `containerMain`.
9. `CrossAxisOffset` — align-items/align-self (flex-start/end/center/stretch); stretch grows a no-explicit-cross item to the line cross size.
10. `EmitItem` — maps main/cross → (X,Y,W,H), sets solver sizes on the box (WidthRaw save/restore, T-18-06), builds the item `LayoutContext` (`ContentOriginX = itemMainX`, `CurrentY = itemCrossY`), recurses via `_blockEngine.Layout`, then emits the item container `PositionedElement` + the recursed output.
11. `ParseMainLength` — px/pt/mm/cm/in/% main-axis length parser (% against `containerMain`).

### Task 2 — Wiring (commit b7483fdd)
- `BlockLayoutEngine.cs`: added `internal FlexLayoutEngine? FlexEngine { get; set; }` next to `TableEngine`; added `case FlexContainerBox flexChild:` in `DispatchLayout` before `default`, mirroring the `TableBox` case exactly (delegates to `FlexEngine.Layout`, emits the container `PositionedElement`, advances `CurrentY`).
- `LayoutEngine.cs`: ctor now constructs `new FlexLayoutEngine(_blockEngine)` after `_blockEngine` exists and sets `_blockEngine.FlexEngine` (same post-construction pattern as `TableEngine`; applies to every `LayoutEngine` instance because each builds + wires its own `_blockEngine`).

## How ROW content-width was resolved

CONCRETE **max-content pass** (option a — NOT deferred). For a row item with no explicit `Width` and `FlexBasisRaw` in {null,"auto","content"}, `MeasureContent` lays the item out at a generous `AvailableWidth` into a throwaway output list and takes `max(e.Position.X + e.Position.Width) − originX` across the emitted `PositionedElement`s as the intrinsic main width. For a column the main size is the measured content height (the `_blockEngine.Layout` return value). The `// D-05 deferred: row content-width = 0 fallback` branch was NOT needed.

## DispatchLayout + ctor wiring

```
// BlockLayoutEngine.DispatchLayout
case FlexContainerBox flexChild:
    float h = FlexEngine != null ? FlexEngine.Layout(flexChild, ctx, output, pageIndex)
                                 : (flexChild.Height > 0f ? flexChild.Height : 0f);
    float flexOriginX = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt;
    output.Add(new PositionedElement { Source = flexChild,
        Position = new Rect(flexOriginX + flexChild.MarginLeft, startY, childWidth, h), PageIndex = pageIndex });
    ctx.CurrentY = startY + h; return h;

// LayoutEngine ctor
var flexEngine = new FlexLayoutEngine(_blockEngine);
_blockEngine.FlexEngine = flexEngine;
```

## Smoke-test result

`tests/Muonroi.Pdf.Tests/Layout/FlexLayoutEngineSmokeTests.cs` — `FlexRow_TwoFiftyPxChildren_NoGap_PacksLeftToRight`: flex row width:300px, two width:50px children, no gap → asserts `item2.Position.X ≈ item1.Position.X + 50px*Units.PxToPt` (37.5pt). **PASSED** — proves left-to-right packing by operand value (engine is not stacked-at-X=0 / logic-broken).

## Test count

Per-project `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj -c Debug`: **Passed 595, Failed 0, Skipped 0** (594 prior baselines byte-identical + 1 new smoke test). No regression — flex code is dormant unless a `FlexContainerBox` is present (flag defaults false).

## D-05 first-cut deferrals (documented in code + here)

- **inline-flex atomic** — `IsInlineFlex` laid out identically to flex; container participates as a block-level box in its parent, no inline integration this phase. (`// D-05: inline-flex atomic first cut` near class header.)
- **baseline ≈ flex-start** — `CrossAxisOffset` `case "baseline"` returns 0 (flex-start). (`// D-05: baseline alignment approximated as flex-start (deferred)`.)
- **tall container atomic for pagination** — no mid-container page split this phase; the container is emitted as one unit. (`// D-05: tall flex container is atomic for pagination` near the return.)

## Deviations from Plan

**1. [Rule 2 — correctness] Container explicit size used as main-axis size.** The plan's interface note seeds main size from `context.AvailableWidth` (row). Added: when the `FlexContainerBox` has an explicit `Width` (row) / `Height` (column), that value is the main-axis size (falls back to `AvailableWidth`/`RemainingHeight`). Without this a `width:300px` flex container would size against the full page content width, mis-distributing free space. Does not affect the smoke assertion (flex-start packing is container-size-independent here). Files: `FlexLayoutEngine.cs`. Commit: a4ca358b.

No other deviations. MSTD: `MGuard.NotNull` used (no raw throws); one `raw!` null-forgiving operator flagged by MSTD0002 was refactored to `raw is { } rawLen` pattern.

## Known Stubs

None. No hardcoded empty values flow to rendering; the engine computes positions from input box sizes.

## Self-Check: PASSED

- FOUND: src/Muonroi.Pdf/Internal/Layout/FlexLayoutEngine.cs
- FOUND: tests/Muonroi.Pdf.Tests/Layout/FlexLayoutEngineSmokeTests.cs
- FOUND: commit a4ca358b (Task 1)
- FOUND: commit b7483fdd (Task 2)
- `case FlexContainerBox` present in BlockLayoutEngine.cs; `new FlexLayoutEngine` present in LayoutEngine.cs.
