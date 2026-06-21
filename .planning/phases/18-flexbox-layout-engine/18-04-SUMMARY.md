---
phase: 18-flexbox-layout-engine
plan: 04
subsystem: pdf-layout-flexbox
tags: [flexbox, golden-tests, regression-guard, FLEX-07, FLEX-08]
requires: [18-03]
provides:
  - FlexLayoutTests (operand-value flex position assertions)
  - FlexLayout golden corpus group + 9 committed baselines
  - FlexLayoutGoldenTests (AllowModernLayout=true render path)
  - FlexRegressionGuardTests (default-path corpus-count + flex-exclusion guard)
  - GoldenPdf flag-aware RenderAsync/VerifyAsync overloads
affects:
  - tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs
  - tests/Muonroi.Pdf.Tests/Golden/GoldenCorpus.cs
tech-stack:
  added: []
  patterns: [operand-value-assertion, flag-aware-golden-render, structural-byte-compare]
key-files:
  created:
    - tests/Muonroi.Pdf.Tests/Layout/FlexLayoutTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/FlexLayoutGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/FlexRegressionGuardTests.cs
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/flex-*.pdf (9 baselines)
  modified:
    - tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs
    - tests/Muonroi.Pdf.Tests/Golden/GoldenCorpus.cs
decisions:
  - "Count guard asserts AllCasesData().Count() == 84 (measured corpus count), not 81. 81 = committed .pdf baseline FILES; 84 = registered AllCases entries; the 3-case gap is canary-only w7-* cases with no committed baseline. Asserting the corpus count is the stable T-18-08 guard."
  - "FlexLayout group kept OUT of AllCases; ByName extended to AllCases.Concat(FlexLayout); flex goldens render only via the AllowModernLayout=true path."
metrics:
  duration: ~25m
  completed: 2026-06-21
---

# Phase 18 Plan 04: Flexbox Layout Engine — Verification & Opt-in Safety Summary

Proved the flexbox engine by operand values and proved the modern-layout opt-in did not perturb the default render path: 12 operand-value unit tests, a 9-case `FlexLayout` golden corpus rendered with `AllowModernLayout=true` (baselines committed), and a byte-identical default-path regression guard. Both per-project suites green; .NET 8/9 build validated.

## What Was Built

### Task 1 — FlexLayoutTests (FLEX-07)
`tests/Muonroi.Pdf.Tests/Layout/FlexLayoutTests.cs` — 12 tests asserting `PositionedElement.Position` X/Y/W/H by OPERAND VALUE (not non-throwing renders):
`RowDistribution_PositionsItemsLeftToRight`, `RowContentBasis_MeasuresIntrinsicWidth`, `FlexGrow_DistributesPositiveFreeSpace`, `FlexShrink_ShrinksOnOverflow`, `JustifyContent_SpaceBetween`, `JustifyContent_Center_And_SpaceEvenly`, `AlignItems_Stretch_SetsCrossSize`, `FlexWrap_BreaksToSecondLine`, `Gap_AddsMainAxisSpacing`, `ColumnDirection_StacksVertically`, `Order_ReordersVisually`, `NestedFlex_Composes`.

Box trees built via `BoxTreeBuilder.Build(parent, null, allowModernLayout: true)` (the flex node wrapped in a plain block parent — `Build()` materializes the ROOT directly as a BlockBox and only consults `display` on CHILD nodes); engine wired explicitly (`be.FlexEngine = new FlexLayoutEngine(be); be.TableEngine = new TableLayoutEngine(be, be.InlineEngine)`) and driven via `be.FlexEngine.Layout(container, ...)` for clean originX=0 positions with `PdfMargins.Zero`.

`RowContentBasis_MeasuresIntrinsicWidth` (two text children, no explicit width) passes with REAL intrinsic-width measurement — each `Width > 0` and `second.X ≈ first.X + first.Width`. The Plan-03 content-basis path is fully implemented (NOT the deferred basis:0 fallback), so no deferral needed documenting.

### Task 2 — FlexLayout golden corpus + flag-aware render path + baselines (FLEX-07)
- `GoldenPdf.cs`: added flag-aware `RenderAsync(html, options, bool allowModernLayout, ct)` that builds the provider with `PdfConfigs:Policy:AllowModernLayout`, and a `VerifyAsync(..., bool allowModernLayout)` overload (delegating to a shared `VerifyCoreAsync`). The existing flag-less overloads are byte-for-byte unchanged.
- `GoldenCorpus.cs`: added standalone `internal static readonly IReadOnlyList<GoldenCase> FlexLayout` (9 cases: `flex-row-basic`, `flex-grow-distribute`, `flex-justify-space-between`, `flex-align-items-stretch`, `flex-wrap-two-line`, `flex-gap`, `flex-column`, `flex-order`, `flex-nested`) + `FlexCasesData()`. `FlexLayout` is NOT in `AllCases`; `ByName` extended to `AllCases.Concat(FlexLayout)`.
- `FlexLayoutGoldenTests.cs`: `[Theory]` over `FlexCasesData` → `VerifyAsync(..., allowModernLayout: true)`.
- 9 baselines generated with `MUONROI_UPDATE_SNAPSHOTS=1` (flex-only run), then re-run WITHOUT the env var → all 9 match structurally. Committed under `TestResources/Golden/` (glob `TestResources/**` picks them up as embedded resources).

### Task 3 — Byte-identical default-path regression guard (FLEX-08)
`FlexRegressionGuardTests.cs`:
- `DefaultPath_Baseline_Count_Unchanged` asserts `GoldenCorpus.AllCasesData().Count() == 84`.
- `FlexCases_AreExcludedFromDefaultPath` proves no flex case name appears in `AllCases`.

The default-path golden theories (BlockLayout/Inline/Tables/… via flag-less `VerifyAsync`) ran green with NO `MUONROI_UPDATE_SNAPSHOTS` → existing 81 default baselines byte-identical.

## Baseline Count Reconciliation (Evidence-First)

The plan locked the count guard to "81" believing it equalled `AllCasesData().Count()`. Measured at execution (2026-06-21):
- `ls TestResources/Golden/*.pdf | wc -l` = **81** before this phase (90 after, − 9 new flex = 81 default).
- `GoldenCorpus.AllCasesData().Count()` = **84** (runtime-measured; FluentAssertions reported "found 84").
- The 3-case gap: `w7-rgb-background-color`, `w7-transparent-background-no-fill`, `w7-float-left-inline-beside` are registered in `AllCases` and exercised by `DeterminismCanaryTests` but ship WITHOUT a committed `.pdf` baseline file.

So the corpus count (84) was always 3 higher than the on-disk baseline-FILE count (81); the upstream "82" conflated the two. The locked invariant is "default-path corpus unchanged from before this phase" — that quantity is `AllCasesData().Count()` (84), which is exactly what guards against flex leaking into `AllCases` (T-18-08) and what the flag-less canary iterates. I added 0 cases to `AllCases`, so 84 is unchanged pre/post phase. Asserted 84, documented in the test comment and here per the plan's escape hatch ("assert THAT number, documenting it").

## Test Results (per-project)

- `Muonroi.Pdf.Tests`: **Passed 618 / Failed 0 / Skipped 0** (595 pre-phase + 12 FlexLayoutTests + 9 FlexLayoutGoldenTests + 2 guard tests).
- `Muonroi.Pdf.Governance.Tests`: **Passed 11 / Failed 0 / Skipped 0**.
- .NET 8/9: `dotnet build -p:TargetFramework=net8.0` (under installed SDK 9.0.311 via roll-forward) — Build succeeded, 0 warnings, 0 errors. Test project TFM is net8.0; local SDK 10.0.201 compiles/runs it.

## Existing Baselines Untouched

`git diff --diff-filter=M --name-only HEAD~3 HEAD -- TestResources/Golden/` = **0 modified**; 9 added (`Bin 0 -> N bytes`). The 81 existing baselines are byte-identical.

## Deviations from Plan

**1. [Rule 1 — Count correction] Count guard asserts 84, not 81**
- **Found during:** Task 3 (first full-suite run reddened the guard: "found 84").
- **Issue:** Plan locked `AllCasesData().Count() == 81`, conflating committed baseline FILES (81) with registered `AllCases` entries (84).
- **Fix:** Asserted the measured corpus count (84) — the plan's own escape hatch instructs re-measuring and documenting. Added a full reconciliation comment in the test.
- **Files modified:** tests/Muonroi.Pdf.Tests/Golden/FlexRegressionGuardTests.cs
- **Commit:** 48c0339d

No other deviations. Engine code in `FlexLayoutEngine.cs` was NOT modified — all operand-value assertions passed against the Plan-03 implementation as written.

## Known Stubs

None. The content-basis measurement path is fully implemented and verified by `RowContentBasis_MeasuresIntrinsicWidth`.

## Self-Check: PASSED

- FlexLayoutTests.cs, FlexLayoutGoldenTests.cs, FlexRegressionGuardTests.cs, 9 flex-*.pdf — all present.
- Commits 31732c1d, (Task 2), 48c0339d present in git log.
