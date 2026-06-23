---
phase: 19-grid-layout-engine
plan: 04
subsystem: pdf-layout
tags: [css-grid, golden-baselines, operand-value-tests, regression-guard, layout-engine, dotnet]

# Dependency graph
requires:
  - phase: 19-03
    provides: GridLayoutEngine.Layout (2-D track sizing + 3 placement modes + cell positioning) wired into DispatchLayout
  - phase: 18-flex-layout-engine
    provides: flag-aware GoldenPdf.VerifyAsync(..., allowModernLayout) overload + standalone FlexLayout corpus group pattern + FlexRegressionGuardTests (count=84 guard)
provides:
  - GridLayoutTests (12 operand-value position/track-size facts, GRID-06)
  - GridLayout standalone golden corpus group + GridCasesData() (10 cases, GRID-07)
  - GridLayoutGoldenTests rendered with AllowModernLayout=true + 10 committed baselines (GRID-07)
  - FlexRegressionGuardTests.GridCases_AreExcludedFromDefaultPath (GRID-08)
affects: [phase 19 complete — grid layout engine verifiable + opt-in-safe]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Operand-value assertions catch real layout bugs a non-throwing render hides (memory pdf_phase15_radial_affine): the auto-track-collapse bug was invisible to the Plan-03 smoke tests (which used 100px Length tracks) and surfaced only when AutoPlacementColumn drove an Auto implicit column"
    - "Grid golden group is standalone, NEVER in AllCases (identical to flex): AllCasesData() drives the flag-less canary + byte-equality theory; a display:grid case there throws PdfPolicyException forbidden.display.grid"
    - "ByName resolves AllCases.Concat(FlexLayout).Concat(GridLayout) so grid cases are name-addressable without polluting AllCases"
    - "Baselines auto-embed via the csproj TestResources/** glob — generate with MUONROI_UPDATE_SNAPSHOTS=1 (writes to source via CallerFilePath), then a second dotnet test rebuilds + embeds + structurally verifies"

key-files:
  created:
    - tests/Muonroi.Pdf.Tests/Layout/GridLayoutTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/GridLayoutGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-fixed-tracks.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-fr-distribute.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-minmax.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-repeat.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-gap.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-explicit-placement.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-span.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-auto-flow-row.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-named-areas.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/grid-nested.pdf
  modified:
    - src/Muonroi.Pdf/Internal/Layout/GridLayoutEngine.cs
    - tests/Muonroi.Pdf.Tests/Golden/GoldenCorpus.cs
    - tests/Muonroi.Pdf.Tests/Golden/FlexRegressionGuardTests.cs

key-decisions:
  - "Auto/content track sizing now honors a definite-width item: MeasureContentMain clamps intrinsic width to box.Width when set. An empty fixed-width box emits no in-flow children, so the prior max-emitted-right-edge math collapsed the auto track to 0 — max-content of a definite-width box is its specified width."
  - "DefaultPathCorpusCount stays 84 (grid adds 0 to AllCases) — the opt-in did not perturb the default corpus size."
  - "10 grid baselines authored to exercise the GRID-07 surface (fixed/fr/minmax/repeat/gap/explicit/span/auto-flow-row/named-areas/nested)."

metrics:
  duration: ~30m
  completed: 2026-06-21
  tasks: 3
---

# Phase 19 Plan 04: Grid Layout Verification + Golden Corpus Summary

Closed GRID-06/07/08 by proving the grid engine works by OPERAND VALUES and proving the modern-layout opt-in did not perturb the default path or the Phase-18 flex path. Added a 12-fact `GridLayoutTests` operand-value suite, a standalone `GridLayout` golden corpus group + `GridLayoutGoldenTests` (rendered with `AllowModernLayout=true` via the existing flag-aware `VerifyAsync` overload) with 10 committed baselines, and a `GridCases_AreExcludedFromDefaultPath` regression guard. The operand-value suite caught a real auto-track-collapse bug that the Plan-03 smoke tests (which used fixed `100px` Length tracks) had hidden.

## What Was Built

- **GridLayoutTests (GRID-06):** 12 facts asserting `PositionedElement.Position` X/Y/W/H and resolved track sizes by value — fixed tracks, fr distribution, minmax (floor-wins + max-wins), repeat, column-gap, explicit line placement (`2 / 3`), span (`1 / span 2`), auto-placement row (wrap to row 2) and column (wrap to column 2), named areas (head spans 2 cols / main at col2-row2), justify-self center, nested grid (grandchild offset by the outer cell X via dispatch recursion).
- **GridLayout golden group + GridLayoutGoldenTests (GRID-07):** 10 standalone cases (NOT in `AllCases`), `GridCasesData()`, `ByName` extended to `.Concat(GridLayout)`; rendered through the reused flag-aware `VerifyAsync(..., allowModernLayout: true)`; 10 deterministic baselines committed.
- **GridCases_AreExcludedFromDefaultPath (GRID-08):** asserts the grid group is non-empty AND no grid case name appears in the flag-less `AllCasesData()` corpus.

## Verification Evidence

- **Muonroi.Pdf.Tests: 661/661 passed** (was 638 pre-plan; +12 GridLayoutTests, +10 GridLayoutGoldenTests, +1 GridCases guard = +23). net8.0 TFM compiled clean (no CS1574/CS1587).
- **Muonroi.Pdf.Governance.Tests: 11/11 passed.**
- **Regression-guard count = 84** (unchanged). `FlexRegressionGuardTests`: 3/3 (count + FlexCases excluded + GridCases excluded).
- **Existing baselines byte-identical** — git evidence across the two test commits: `git diff --name-status HEAD~2 HEAD -- TestResources/Golden/` reports **10 A, 0 M** (10 grid-*.pdf added, zero existing baselines modified). The flag-less default-path byte-equality theory + the 9 Phase-18 flex baselines passed green with NO `MUONROI_UPDATE_SNAPSHOTS` → byte-identical proof; flex still renders with the flag on.
- Baselines re-verified deterministically: generated with `MUONROI_UPDATE_SNAPSHOTS=1`, then a second `dotnet test` (no env var) confirmed structural match.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Auto-sized grid track collapsed to 0 for a definite-width item**
- **Found during:** Task 1 (`AutoPlacementColumn_WrapsToNextColumn` failed — child3.X = 0 instead of wrapping to column 2).
- **Issue:** `GridLayoutEngine.MeasureContentMain` computed an item's max-content width solely from its emitted in-flow children. An empty `<div>` with `width:100px` (no text/children) emitted nothing, so an `auto` (implicit) column holding only such items resolved to 0 px — the wrapped item landed at X = 0 with width 0. Plan-03 smoke tests masked this because they used fixed `100px` Length tracks, which never invoke `MeasureTrack`.
- **Fix:** After the throwaway-layout intrinsic-width pass, clamp `intrinsicWidth = max(intrinsicWidth, box.Width)` when `box.Width > 0`. Max-content of a definite-width box is its specified width.
- **Files modified:** `src/Muonroi.Pdf/Internal/Layout/GridLayoutEngine.cs`
- **Commit:** c32d7601
- Verified no regression on the existing grid smoke + box-tree tests (16/16) and the full suites (661 + 11).

## Commits

- `c32d7601` test(19-04): GridLayoutTests operand-value GRID-06 suite + fix auto-track collapse
- `32065a70` test(19-04): GridLayout golden corpus + GridLayoutGoldenTests + baselines (GRID-07)
- `2f086596` test(19-04): GridCases_AreExcludedFromDefaultPath regression guard (GRID-08)

## Known Stubs

None.

## Self-Check: PASSED

- Created files verified on disk: GridLayoutTests.cs, GridLayoutGoldenTests.cs, 10 grid-*.pdf baselines (spot-checked grid-nested.pdf) — all FOUND.
- Commits verified via `git cat-file -e`: c32d7601, 32065a70, 2f086596 — all FOUND.
