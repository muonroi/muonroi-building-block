---
phase: 19-grid-layout-engine
plan: 02
subsystem: pdf-layout
tags: [css-grid, grid-template-columns, repeat, minmax, grid-template-areas, box-tree, dotnet]

# Dependency graph
requires:
  - phase: 19-01
    provides: AllowModernLayout flag gated in LegacyPrintPolicy
  - phase: 18-flex-layout-engine
    provides: FlexContainerBox + allowModernLayout threading (MPdfService→LayoutAsync→RunLayout→Build) + AlignSelf grid-item prop + gated mapping pattern
provides:
  - GridContainerBox box type carrying resolved grid container props
  - GridTrack track-size model (Length/Percent/Fraction/Auto/MinMax) with repeat()/minmax() parsing + DoS clamp
  - BoxNode grid-item props (GridColumnRaw/GridRowRaw/GridAreaRaw/JustifySelf; AlignSelf reused)
  - Gated display:grid/inline-grid → GridContainerBox mapping (flag-off degrades to BlockBox)
  - ResolveGridProperties resolving track lists, auto-flow/rows/cols, gaps, justify/align, template-areas, placement shorthands
affects: [19-grid-layout-engine plan 03 (GridLayoutEngine), 19 plan 04 (golden baselines)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Track-list parser mirrors the flex parser style (TryParse + fall back to CSS default, never throws — T-19-04)"
    - "Gated modern-layout mapping: grid cases use `when _allowModernLayout`, else fall through to BlockBox (degrade path byte-identical — T-19-03)"
    - "repeat(N,...) count clamped to MaxRepeatCount=1000 to bound allocation (T-19-04 DoS)"
    - "grid-template-areas rejects ragged/empty grids → empty (T-19-05 out-of-bounds guard)"

key-files:
  created:
    - src/Muonroi.Pdf/Internal/Layout/Boxes/GridTrack.cs
    - src/Muonroi.Pdf/Internal/Layout/Boxes/GridContainerBox.cs
    - tests/Muonroi.Pdf.Tests/Layout/GridBoxTreeTests.cs
  modified:
    - src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs
    - src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs

key-decisions:
  - "GridTrack is a class (not struct) so the MinMax kind can carry nested Min/Max sub-tracks"
  - "GridTrack reuses BoxTreeBuilder.ParseLength via a new internal ParseLengthPublic accessor — no second length parser (per plan)"
  - "Percent tracks stored as a 0..1 fraction (50% → 0.5); resolved against container axis at layout time (Plan 03)"
  - "auto-fill/auto-fit repeat (non-integer first arg) skipped entirely (D-01 out of scope)"
  - "dense in grid-auto-flow stripped (sparse-only, D-01)"

patterns-established:
  - "Pattern: grid box-tree construction mirrors the Phase 18 flex pattern verbatim (gated CreateBox case, BuildNode recursion case, BuildChildren overload, Resolve*Properties)"

requirements-completed: [GRID-04]

# Metrics
duration: ~20min
completed: 2026-06-21
---

# Phase 19 Plan 02: CSS Grid Box Tree Summary

**GridContainerBox + GridTrack track model (fr/%/length/auto/minmax + repeat() with DoS clamp) and BoxNode grid-item props, with display:grid gated to GridContainerBox only when AllowModernLayout is on (else byte-identical BlockBox degrade).**

## Performance

- **Duration:** ~20 min
- **Tasks:** 2 (Task 2 via TDD: RED → GREEN)
- **Files modified:** 5 (3 created, 2 modified)

## Accomplishments
- `GridTrack` model: kinds Length/Percent/Fraction/Auto/MinMax; `ParseTrackList` expands `repeat(N, list)` (count clamped to 1000) and parses `minmax(min,max)` with nested-paren tokenization; `ParseSingleTrack` for single tracks. Never throws — malformed tokens degrade to `Auto`.
- `GridContainerBox : BoxNode` with all resolved container props (template cols/rows, auto-flow/cols/rows, row/col gap, justify/align items+content, `string[][]` TemplateAreas, IsInlineGrid).
- `BoxNode` extended with nullable grid-item props (`GridColumnRaw`, `GridRowRaw`, `GridAreaRaw`, `JustifySelf`); `AlignSelf` reused from Phase 18.
- `BoxTreeBuilder` maps `grid`/`inline-grid` → `GridContainerBox` ONLY when `_allowModernLayout` is true (no new threading); flag off keeps the BlockBox degrade path. Added BuildNode recursion case + BuildChildren overload + `ResolveGridProperties`.
- 14 box-tree/parser unit tests, all green; full Muonroi.Pdf.Tests project stays green (636 passed, 0 failed) — degrade + flex baselines unchanged.

## Task Commits

1. **Task 1: GridTrack + GridContainerBox + BoxNode grid-item props** - `dee0fb32` (feat)
2. **Task 2 (RED): failing box-tree tests** - `f26bc99e` (test)
3. **Task 2 (GREEN): gate grid + ResolveGridProperties** - `167e406a` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf/Internal/Layout/Boxes/GridTrack.cs` - track-size model + track-list parser (repeat/minmax, DoS clamp)
- `src/Muonroi.Pdf/Internal/Layout/Boxes/GridContainerBox.cs` - grid container box + resolved container props
- `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` - added nullable grid-item props (GridColumnRaw/GridRowRaw/GridAreaRaw/JustifySelf)
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` - gated grid mapping, BuildNode case, BuildChildren overload, ResolveGridProperties + ParseTemplateAreas + ParseLengthPublic accessor
- `tests/Muonroi.Pdf.Tests/Layout/GridBoxTreeTests.cs` - 14 tests (gating, track-list/gap, repeat/minmax, auto-flow/rows, dense strip, template-areas + ragged fallback, item placement, DoS clamp, auto-fill skip, malformed degrade)

## Key Field Sets

- **GridTrack:** `Kind`, `Length` (pt), `Percent` (0..1), `Fraction` (fr count), `Min`, `Max` (MinMax sub-tracks) + `MaxRepeatCount=1000`, `ParseTrackList`, `ParseSingleTrack`.
- **GridContainerBox:** `TemplateColumns`, `TemplateRows` (List<GridTrack>), `AutoColumns`, `AutoRows` (GridTrack?), `AutoFlow`, `RowGap`, `ColumnGap`, `JustifyItems`, `AlignItems`, `JustifyContent`, `AlignContent`, `TemplateAreas` (string[][]), `IsInlineGrid`.
- **BoxNode grid-item:** `GridColumnRaw`, `GridRowRaw`, `GridAreaRaw`, `JustifySelf` (all nullable); `AlignSelf` reused.

## Parser repeat/minmax handling + DoS clamp
- `repeat(N, <track-list>)`: integer N parsed; **clamped to `MaxRepeatCount=1000`** before expansion so `repeat(99999999, 1fr)` allocates at most 1000 tracks (asserted in `ParseTrackList_RepeatHostileCount_ClampedToMax`). Non-integer first arg (`auto-fill`/`auto-fit`) skips the repeat (D-01).
- `minmax(min, max)`: parsed via balanced-paren top-level comma split into two non-MinMax sub-tracks (`50px` → Length 37.5pt, `1fr` → Fraction 1).
- Tokenization is paren-depth aware so `repeat()` may contain `minmax()`.
- Ragged/empty `grid-template-areas` rejected → empty `string[][]` (T-19-05).

## Decisions Made
- See key-decisions frontmatter. Notably: `GridTrack` is a class (MinMax nesting), Percent stored as 0..1 fraction, reused single length parser via `ParseLengthPublic`.

## Deviations from Plan
None - plan executed exactly as written. The only addition beyond the literal task text was `BoxTreeBuilder.ParseLengthPublic` (a thin internal accessor) so `GridTrack` could reuse the existing single length parser instead of adding a second one — this is exactly what the plan mandates ("do NOT add another length parser").

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 03 (GridLayoutEngine) has a fully-typed, fully-parsed grid box tree to size + place.
- `BoxTreeBuilder.cs` signatures unchanged for the gated mapping path; Plan 03 consumes `GridContainerBox`/`GridTrack`/grid-item props as-is.
- Degrade + flex goldens unchanged (flag defaults false) — Plan 04 byte-identical verification unaffected.

## Self-Check: PASSED

- FOUND: src/Muonroi.Pdf/Internal/Layout/Boxes/GridTrack.cs
- FOUND: src/Muonroi.Pdf/Internal/Layout/Boxes/GridContainerBox.cs
- FOUND: tests/Muonroi.Pdf.Tests/Layout/GridBoxTreeTests.cs
- FOUND commit: dee0fb32 (Task 1)
- FOUND commit: f26bc99e (Task 2 RED)
- FOUND commit: 167e406a (Task 2 GREEN)

---
*Phase: 19-grid-layout-engine*
*Completed: 2026-06-21*
