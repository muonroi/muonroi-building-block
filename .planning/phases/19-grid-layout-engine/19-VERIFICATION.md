---
phase: 19-grid-layout-engine
verified: 2026-06-21T00:00:00Z
status: passed
score: 13/13 must-haves verified (5/5 SC + 8/8 GRID)
re_verification:
  previous_status: none
  note: initial verification
---

# Phase 19: CSS Grid Layout Engine Verification Report

**Phase Goal:** Implement a real CSS Grid layout algorithm in the OSS `Muonroi.Pdf` engine (`GridContainerBox` + `GridLayoutEngine`), unlocked by the EXISTING `PdfPolicySettings.AllowModernLayout` flag (no new flag); strict-by-default preserved; existing + Phase-18 flex baselines byte-identical; only new grid goldens added. Direct sibling of Phase 18.
**Verified:** 2026-06-21
**Status:** PASSED
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (Roadmap Success Criteria)

| # | Truth (SC) | Status | Evidence |
|---|-----------|--------|----------|
| SC1 | `display:grid` renders via a real Grid algorithm with track sizing (px/%/fr/auto/minmax/repeat), gaps, explicit/named-area/auto-flow placement, alignment; asserted by `PositionedElement.Position` operand VALUES; wired into dispatch + ctor | ✓ VERIFIED | `GridLayoutEngine.cs` real `ResolveTrackSizes` (fr distribution `:540-551`, minmax floor/clamp `:505-519`,`:590-594`, percent/auto/length `:489-525`); `GridLayoutTests.cs` 12 operand-value facts (fr `:149-150`, minmax `:165-174`, repeat `:190-193`, named-areas `:316-321`, span `:240`, auto-flow row `:261-268`+col `:290-295`, gap `:213`, explicit `:226`, justify-self `:334`, nested `:366-371`); wired `BlockLayoutEngine.cs:504` `case GridContainerBox`, `LayoutEngine.cs:31-32` ctor |
| SC2 | Flag OFF → grid byte-identical: strict emits `forbidden.display.grid`, soft-degrade warns+blocks; `LegacyPrintPolicyTests` grid expectations unchanged | ✓ VERIFIED | `LegacyPrintPolicy.cs:264` gate `(display is "grid" or "inline-grid") && !allowModernLayout`; sub-prop branch `:295` `!gridSubPropSeen && !allowModernLayout`; flag-off control tests `LegacyPrintPolicyAllowModernLayoutTests.cs:122` (strict, both policies), `:140` (soft-degrade warns+degrades) |
| SC3 | Existing baselines (default-path + 9 Phase-18 flex) byte-identical; new grid goldens standalone (outside `AllCases`) | ✓ VERIFIED | git `abf376d3..4599fce1 -- TestResources/Golden/` = **10 A, 0 M** (10 grid-*.pdf added, 0 modified); `GoldenCorpus.cs:126` standalone `GridLayout` group NOT in `AllCases`; `:771` `ByName = AllCases.Concat(FlexLayout).Concat(GridLayout)`; regression guard count **84** `FlexRegressionGuardTests.cs:33` + `GridCases_AreExcludedFromDefaultPath :56` |
| SC4 | Flex unaffected — flex still renders with flag on; flag unlocks BOTH | ✓ VERIFIED | `LegacyPrintPolicyAllowModernLayoutTests.cs:104` `FlexAndGrid_FlagOn_BothAccepted`; flex gate `:252` untouched; Phase-18 grid-blocked tests FLIPPED not deleted (`Grid_FlagOn_StrictBase_Accepted :63`, `Grid_FlagOn_SoftDegrade_Accepted_NoGridWarning :76`) |
| SC5 | Gated on EXISTING `AllowModernLayout` (no new flag); both suites green (per-project); `DefaultStrictPolicy` unchanged; build on .NET 8 | ✓ VERIFIED | No new config key (reuses `PdfPolicySettings.AllowModernLayout`); `DefaultStrictPolicy.cs` git-unchanged across phase; Pdf.Tests **661/0**, Governance.Tests **11/0**, both ran on net8.0 TFM (verifier reproduced) |

**Score: 5/5 SC verified**

### Requirements Coverage

| Requirement | Status | Evidence |
|-------------|--------|----------|
| GRID-01 (gate display+sub-props on existing flag, no new key) | ✓ SATISFIED | `LegacyPrintPolicy.cs:264`,`:295`; no new config key |
| GRID-02 (flag on → accepted, sub-props kept; flipped Phase-18 test; DefaultStrict always-strict) | ✓ SATISFIED | Tests `:63`,`:76`,`:88`,`:104`; `DefaultStrictPolicy` unchanged |
| GRID-03 (flag off → unchanged strict/soft-degrade) | ✓ SATISFIED | Tests `:122`,`:140`; gate `&& !allowModernLayout` |
| GRID-04 (`GridContainerBox` + gated mapping + prop resolution: track lists/repeat/minmax/areas/placement/gaps) | ✓ SATISFIED | `GridContainerBox.cs`, `GridTrack.cs` (MaxRepeatCount=1000 DoS clamp `:36`,`:160`), `BoxTreeBuilder.cs:234-235` gated mapping, `:684` `ResolveGridProperties`; 14 `GridBoxTreeTests` |
| GRID-05 (`GridLayoutEngine`: track sizing + 3 placement modes + align; recurses; wired) | ✓ SATISFIED | `GridLayoutEngine.cs` full engine; `BlockLayoutEngine.cs:504` case; `LayoutEngine.cs:31-32` ctor |
| GRID-06 (operand-value unit tests) | ✓ SATISFIED | `GridLayoutTests.cs` 12 facts (see SC1) |
| GRID-07 (standalone grid golden corpus + flag-aware tests + committed baselines) | ✓ SATISFIED | `GoldenCorpus.cs:126` 10 cases; `GridLayoutGoldenTests.cs:24` `VerifyAsync(...allowModernLayout:true)`; 10 baselines on disk |
| GRID-08 (existing baselines byte-identical; flex renders with flag; both suites green; .NET 8/9) | ✓ SATISFIED | git 10 A/0 M; guard count 84 + GridCases excluded; 661/0 + 11/0 on net8.0 |

**Score: 8/8 GRID verified**

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `src/Muonroi.Pdf/Internal/Layout/GridLayoutEngine.cs` | ✓ VERIFIED | Real track-sizing (fr/minmax/auto/percent/length) + 3 placement modes + cell positioning + recursion via `_blockEngine.Layout`; wired |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/GridContainerBox.cs` | ✓ VERIFIED | Container box w/ resolved props (template cols/rows, auto-flow/rows/cols, gaps, justify/align, TemplateAreas, IsInlineGrid) |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/GridTrack.cs` | ✓ VERIFIED | Length/Percent/Fraction/Auto/MinMax + repeat()/minmax() parser + DoS clamp |
| `LegacyPrintPolicy.cs` (modified) | ✓ VERIFIED | Both `&& !allowModernLayout` guards added; flex/DefaultStrict untouched |
| `BoxTreeBuilder.cs` (modified) | ✓ VERIFIED | Gated `grid`/`inline-grid` mapping + `ResolveGridProperties` |
| `tests/.../GridLayoutTests.cs` | ✓ VERIFIED | 12 operand-value facts (not `.Count>0`) |
| `tests/.../Golden/GridLayoutGoldenTests.cs` + 10 baselines | ✓ VERIFIED | Flag-aware render; 10 grid-*.pdf committed |
| `FlexRegressionGuardTests.cs` (modified) | ✓ VERIFIED | Count 84 + `GridCases_AreExcludedFromDefaultPath` |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `BlockLayoutEngine.DispatchLayout` | `GridLayoutEngine.Layout` | `case GridContainerBox` `:504-509` | WIRED |
| `LayoutEngine` ctor | `_blockEngine.GridEngine` | post-construction `:31-32` | WIRED |
| `BoxTreeBuilder` | `GridContainerBox` | `"grid" when _allowModernLayout` `:234-235` | WIRED |
| `GridLayoutGoldenTests` | flag-aware harness | `VerifyAsync(...allowModernLayout:true)` `:24` | WIRED |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Pdf.Tests suite (net8.0) | `dotnet test Muonroi.Pdf.Tests` | Passed 661 / Failed 0 | ✓ PASS |
| Governance.Tests suite (net8.0) | `dotnet test Muonroi.Pdf.Governance.Tests` | Passed 11 / Failed 0 | ✓ PASS |
| Baseline non-modification | `git diff --name-status abf376d3 4599fce1 -- TestResources/Golden/` | 10 A, 0 M | ✓ PASS |

### Known-Reconciliation Verification (sound, not bugs)

| Item | Verdict | Evidence |
|------|---------|----------|
| Regression-guard count = 84 (AllCases registered cases, grid+flex excluded) | ✓ SOUND | `FlexRegressionGuardTests.cs:18-33` documents 81 on-disk files vs 84 registered cases (3 canary-only w7-* cases without baseline files); `DefaultPath_Baseline_Count_Unchanged` green |
| 19-04 Rule-1 auto-track-collapse bug fixed (commit c32d7601) | ✓ SOUND | Fix present `GridLayoutEngine.cs:671-672` `if (box.Width > 0f) intrinsicWidth = MathF.Max(intrinsicWidth, box.Width)`; the wrap behaviour asserted by `AutoPlacementColumn_WrapsToNextColumn` (`GridLayoutTests.cs:273` — `child3.X > child1.X`) which the bug had broken (child3.X=0). Operand-value test caught what the Plan-03 fixed-`100px` smoke test hid |

### D-05 Deferral Documentation

| Deferral | Status | Evidence |
|----------|--------|----------|
| subgrid, auto-fill/fit, dense→sparse, masonry, baseline≈start, indefinite-%, pagination atomic, inline-grid atomic | ✓ DOCUMENTED | `GridLayoutEngine.cs:18-25` header block + inline `:111`,`:494`,`:725`,`:768`,`:818`; `GridTrack.cs:155-161` auto-fill/fit skip + DoS clamp |

### Anti-Patterns Found

None. No TODO/FIXME/XXX/PLACEHOLDER markers in the phase's modified/created files. No stub returns; the engine performs real computation. `Known Stubs: None` (19-04 SUMMARY) confirmed against code.

### Human Verification Required

None — phase is pure backend layout logic with operand-value unit assertions and structural golden byte-equality; all verifiable programmatically.

### Gaps Summary

No gaps. All 5 Success Criteria and all 8 GRID requirements are delivered in code, wired, and proven:
- The grid engine is genuinely substantive (real fr/minmax/auto track sizing, three placement modes), not a stub.
- The policy gate mirrors the Phase-18 flex pattern exactly with `&& !allowModernLayout` on both the display branch (`:264`) and the grid sub-prop branch (`:295`).
- Byte-identity is proven by git (0 baselines modified, 10 grid-*.pdf added) AND by the green flag-less default-path theories + the count-84 regression guard.
- The Phase-18 grid-blocked tests were FLIPPED (not deleted); flag-off control tests and flex+grid-both-accepted coverage exist.
- `DefaultStrictPolicy` is git-unchanged; no new flag was introduced.
- Both reconciliations (count 84, c32d7601 auto-track-collapse fix) are sound and code-confirmed.
- Both test suites green on the net8.0 TFM (verifier reproduced).

---

_Verified: 2026-06-21_
_Verifier: Claude (gsd-verifier)_
