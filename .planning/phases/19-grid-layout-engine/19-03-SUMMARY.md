---
phase: 19-grid-layout-engine
plan: 03
subsystem: pdf-layout
tags: [css-grid, track-sizing, fr-distribution, minmax, named-areas, auto-flow, layout-engine, dotnet]

# Dependency graph
requires:
  - phase: 19-02
    provides: GridContainerBox + GridTrack model + BoxNode grid-item props + gated display:grid mapping
  - phase: 18-flex-layout-engine
    provides: FlexLayoutEngine scaffold (MeasureContent max-content pass, EmitItem save/restore + recursion) + DispatchLayout FlexContainerBox case pattern + LayoutEngine ctor post-construction wiring
provides:
  - GridLayoutEngine.Layout(GridContainerBox, LayoutContext, output, pageIndex) → float (2-D track sizing + 3 placement modes + cell positioning)
  - BlockLayoutEngine.DispatchLayout case GridContainerBox (emits container element + advances CurrentY)
  - BlockLayoutEngine.GridEngine property + LayoutEngine ctor wiring
affects: [19 plan 04 (golden baselines + full position-value assertions)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "GridLayoutEngine mirrors FlexLayoutEngine: non-block engine driven from DispatchLayout, recurses items via _blockEngine.Layout (no layout reimplementation)"
    - "ResolveTrackSizes shared by both axes: fixed/percent first → auto/content measured via max-content pass → fr distributes remaining free space ∝ Fraction, minmax clamps honored"
    - "Implicit-track creation bounded by item count via Min(maxTrack, explicit+itemCount) (T-19-06)"
    - "WidthRaw/Width/Height save-restore around _blockEngine.Layout (T-19-07, identical to FlexLayoutEngine.EmitItem)"

key-files:
  created:
    - src/Muonroi.Pdf/Internal/Layout/GridLayoutEngine.cs
    - tests/Muonroi.Pdf.Tests/Layout/GridLayoutEngineSmokeTests.cs
  modified:
    - src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs
    - src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs

key-decisions:
  - "fr floor: a minmax(min, <fr>) track seeds sizes[i] with the min floor, then participates in fr distribution capped by max — keeps the floor without double-counting"
  - "MeasureTrack only single-track spanners contribute to an auto/content track's intrinsic size (multi-track distribution deferred to Plan 04)"
  - "justify/align-content applies only a LEADING group offset (space-* approximated as center; space-between as start) — inter-track distribution deferred to Plan 04"
  - "A definite start on one axis with the other auto anchors the auto axis at track 0 (first-cut)"

metrics:
  duration: ~25m
  completed: 2026-06-21
  tasks: 2
  files-created: 2
  files-modified: 2
  tests: 638 passed / 0 failed (per-project Muonroi.Pdf.Tests)
---

# Phase 19 Plan 03: GridLayoutEngine (track sizing + placement) Summary

CSS Grid track-sizing + placement engine — per-axis fixed/percent/auto/fr/minmax track sizing, explicit-line / named-area / sparse auto-flow placement, and cell-rect positioning with justify/align items/self/content — mirroring `FlexLayoutEngine` and recursing each item through the existing `BlockLayoutEngine` dispatch so nested grid/flex/block/table compose.

## What shipped

### Task 1 — GridLayoutEngine (GRID-05)
`src/Muonroi.Pdf/Internal/Layout/GridLayoutEngine.cs` — `internal sealed class GridLayoutEngine`, ctor `(BlockLayoutEngine)` with `MGuard.NotNull`, single public `Layout(GridContainerBox, LayoutContext, List<PositionedElement>, int) → float`.

**Method / helper structure**
- `Layout` — orchestrates the 7 steps: origin/start, `PlaceItems`, build effective tracks, `ResolveTrackSizes` (columns then rows), `ApplyContentAlignment`, `EmitItem` per item, return container height + advance `CurrentY`.
- `PlaceItems` → `List<GridPlacement>` + out `colCount`/`rowCount`. Pass A resolves explicit + named-area placements (`ResolveExplicit`); Pass B sparse auto-flows the rest over a `HashSet<long>` occupancy grid (cell key = `(row<<32)|col`).
- `ResolveExplicit` / `TryResolveLineSpec` / `ResolveAxisLines` / `TryParseSpan` / `TryParseLine` / `ResolveSpanOnly` — placement token parsing (line numbers, `A / B`, `span K`, negative-from-end, `grid-area` 4-value shorthand + named area). All `TryParse`; malformed → auto-place (never throws).
- `BuildAreaIndex` — `grid-template-areas` → name→bounding-rect (`Min`/`Max` over labelled cells).
- `BuildEffectiveTracks` — explicit template + implicit tracks (sized by `AutoColumns`/`AutoRows`, default auto) up to the placement count.
- `ResolveTrackSizes` (shared both axes) — Step 1+2 resolve non-fr tracks (Length, Percent×axis, Auto/content via `MeasureTrack`, MinMax via `ResolveMinMax`); Step 3 distributes remaining free space across fr tracks ∝ `Fraction` honoring `frMax` clamps; clamps NaN/negative → 0.
- `ResolveMinMax` — fr-max → flexible track floored by min; fixed/auto-max → content clamped into `[min, max]`.
- `MeasureTrack` / `MeasureContentMain` — max-content pass borrowed from `FlexLayoutEngine.MeasureContent` (max emitted right-edge − origin for width; `Layout` return for height); WidthRaw save/restore.
- `CumulativeOffsets` / `TracksExtent` / `SpanSize` — cell geometry (offsets, total extent, spanned size incl. interior gaps).
- `ApplyContentAlignment` — leading group offset per justify/align-content.
- `EmitItem` — cell rect → justify-self/align-self within cell (`AxisOffset`; stretch fills) → save/restore Width/WidthRaw/Height → `_blockEngine.Layout` recursion → emit item PositionedElement + AddRange child output. Mirrors `FlexLayoutEngine.EmitItem` exactly (T-19-07).

**How track sizing was implemented** — Single `ResolveTrackSizes` for both axes. Columns sized against the definite container width; rows against explicit `Height` else content-driven (`rowsExtent`). For rows, `colSizesForRowMeasure` threads the resolved column sizes so a row's auto-height is measured at its item's actual column width. fr distribution: `free = available − Σ(non-fr + fr floors)`, each fr track gets `free × (fr/Σfr)` then clamped by its minmax max. Verified by the fr canary (300px / 1fr 2fr → 100px / 200px cells).

**Three placement modes** — (1) explicit: `TryResolveLineSpec`/`ResolveAxisLines` resolve 1-based lines (negative from end), `span K`, and the `grid-area` 4-value shorthand into 0-based start+span; (2) named-area: `BuildAreaIndex` bounding rect from `TemplateAreas`; (3) sparse auto-flow: cursor walks cells in `grid-auto-flow` order (row = fill columns then next row; column = fill rows then next column), skipping occupied cells, wrapping the cross axis, creating implicit tracks bounded by item count (T-19-06).

### Task 2 — Dispatch + ctor wiring (GRID-05)
- `BlockLayoutEngine.cs`: added `internal GridLayoutEngine? GridEngine { get; set; }` next to `FlexEngine`; added `case GridContainerBox gridChild:` before `default` in `DispatchLayout`, mirroring the `FlexContainerBox` case exactly (GridEngine emits per-item elements; the case emits the container element + advances `CurrentY`).
- `LayoutEngine.cs` ctor: `var gridEngine = new GridLayoutEngine(_blockEngine); _blockEngine.GridEngine = gridEngine;` (post-construction, breaks the ctor cycle — same pattern as Flex/Table). Runs for both `this` and any `new LayoutEngine(realMetrics)` in `LayoutAsync`.

## Smoke gate results (WAVE-3, both assertions)
`tests/Muonroi.Pdf.Tests/Layout/GridLayoutEngineSmokeTests.cs` — engine wired explicitly, `Build(root, null, allowModernLayout:true)`, `PdfMargins.Zero`. Asserted by operand value:
- **(a) Column-placement**: `grid-template-columns:100px 100px 100px`, 3 children → `pe2.X ≈ pe1.X + 100px*PxToPt` and `pe3.X ≈ pe1.X + 200px*PxToPt`. PASS.
- **(b) fr-distribution canary**: width:300px `grid-template-columns:1fr 2fr`, 2 children → first cell `Width ≈ 100px*PxToPt` (75pt), second `Width ≈ 200px*PxToPt` (150pt). PASS.

Both pass (`Failed: 0, Passed: 2`).

## D-05 / D-01 deferrals (documented as code comments + here)
Encoded as `// D-01:` comments in `GridLayoutEngine.cs`:
- auto-fill/auto-fit not supported (repeat() expanded with fixed count at parse time, Plan 02).
- grid-auto-flow `dense` not supported — sparse packing only (`dense` token stripped at parse time).
- subgrid / masonry not supported.
- baseline alignment approximated as start (`AxisOffset` "baseline" → 0).
- percentage track/gap against an indefinite container treated as auto/content.
- tall grid container is atomic for pagination (no mid-container page split).
- inline-grid (`IsInlineGrid`) laid out identically as an atomic block-level box.
- (Plan-04 refinements) justify/align-content space-* approximated as leading offset; multi-track spanners do not contribute to a track's intrinsic auto size.

## Test results
Per-project `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj -c Debug`: **638 passed / 0 failed / 0 skipped** (29s). No existing baseline regressed — grid code is dormant unless a `GridContainerBox` is present.

## Deviations from Plan
None functional. Two MSTD-compliance adjustments during implementation:
- **[Rule 3 — Blocking] MSTD0002 null-forgiving operator forbidden.** Initial draft used `child.GridColumnRaw!` / `child.GridRowRaw!` after a null-check; the `Muonroi.Pdf.Internal.Layout` analyzer forbids `!`. Restructured to capture nullable locals (`colRaw`/`rowRaw`) and branch on `!= null` instead. Files: `GridLayoutEngine.cs`. Commit: 59811e8f.
- Removed a malformed leftover expression in the `explicitCols` initializer before first build (drafting artifact, never compiled). Commit: 59811e8f.

## TDD Gate Compliance
- RED: `test(19-03)` commit b365c71c — smoke test added, failed to compile (GridLayoutEngine/GridEngine absent).
- GREEN: `feat(19-03)` commit 59811e8f — engine + wiring, smoke gate passes, full suite green.
- REFACTOR: none required.

## Self-Check: PASSED
- GridLayoutEngine.cs, GridLayoutEngineSmokeTests.cs, 19-03-SUMMARY.md all present
- commits b365c71c (RED) + 59811e8f (GREEN) in git log
