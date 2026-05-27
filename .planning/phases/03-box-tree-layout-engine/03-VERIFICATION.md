---
phase: 03-box-tree-layout-engine
verified: 2026-05-27T00:00:00Z
status: passed
score: 5/5 must-haves verified
gaps: []
human_verification: []
---

# Phase 3: Box Tree + Layout Engine — Verification Report

**Phase Goal:** A styled DOM converts to a box tree and lays out into pages with correct block/inline/table formatting, margin collapsing, and pagination
**Verified:** 2026-05-27 (updated after gap closure commits d5342a9 and aefe4f1)
**Status:** passed

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Margin collapsing per CSS 2.1 §8.3.1 | PASS | `BlockLayoutEngine.CollapseMargins()` uses `max(positives) + min(negatives)`; `IsBfcRoot()` gates boundary collapse; 3 passing unit tests (AdjacentBlocks_MarginCollapsesTo_Maximum, AdjacentBlocks_GapIsNotSumOfMargins, BfcRoot_PreservesFirstChildMarginTop) |
| 2 | Inline baseline + Unicode breaks | PASS | Baseline offsets: lineAscender - boxAscender formula covers vertical-align top/middle/bottom/baseline; 4 tests pass. Vietnamese+Latin test added in aefe4f1: `VietnamesePlusLatin_MixedText_ProducesOneElementPerSpaceSeparatedToken` passes (3 tokens from "Xin chào world"). Space-based splitting documented as accepted for Phase 3 in KD-03-05. |
| 3 | Table colspan/rowspan + border-collapse:collapse policy | PASS | colspan/rowspan layout correct (4 tests pass). Gap closed in d5342a9: `DefaultStrictPolicy.CheckCssFeatures()` Pass 2 now checks `border-collapse == "collapse"` at lines 119-121 and emits `ViolationFor("forbidden.border-collapse.collapse", ..., "border-collapse:separate")`. Regression test `DefaultStrictPolicy_BorderCollapseCollapse_EmitsPolicyViolation` passes in Muonroi.Pdf.Governance.Tests. |
| 4 | page-break-before:always + @page header repetition | PASS | `PaginationEngine`: `forceBreak` flag set on `PageBreakBefore == "always"` (line 59); `ApplyHeaderFooter()` called for every page in loop (lines 111-113); header sourced from `IPageRule.TopMarginBoxHtml`. Test `PageBreakBeforeAlways_SecondBlock_IsOnPageIndex1` passes. |
| 5 | counter(pages) via two-pass layout | PASS | `LayoutEngine.Layout()`: pass 1 with `totalPages=0`, then `RunLayout(totalPages: pass1.PageCount)`; `PaginationEngine` replaces `counter(pages)` and `counter(page)` by string substitution. Tests `CounterPages_ResolvesToCorrectTotalAfterTwoPassLayout` and `CounterPage_ResolvesToOneBased_PageNumber` both pass. |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` | Build box tree from IStyledNode | PRESENT | 274 lines; handles display:none, AnonymousBox wrapping, all table box types, CSS property resolution |
| `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` | Block layout with margin collapse | PRESENT | 185 lines; CollapseMargins(), IsBfcRoot(), DispatchLayout() wired to table/inline/replaced |
| `src/Muonroi.Pdf/Internal/Layout/InlineLayoutEngine.cs` | IFC with baseline alignment | PRESENT | 120 lines; CommitLine() with vertical-align switch; word wrap on spaces |
| `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` | Table layout with colspan/rowspan | PRESENT | 374 lines; two-pass row heights; ComputeAutoColumnWidths() per CSS 2.1 §17.5.2 |
| `src/Muonroi.Pdf/Internal/Layout/PaginationEngine.cs` | Pagination + counters + headers | PRESENT | 188 lines; forced breaks; counter substitution; ApplyHeaderFooter() per page |
| `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` | Two-pass orchestrator | PRESENT | 101 lines; pass1(totalPages=0) → pass2(totalPages=N) |
| `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` | border-collapse:collapse policy check | PRESENT | Lines 119-121 added in d5342a9: reads `border-collapse` from computed style per element; emits `ViolationFor("forbidden.border-collapse.collapse", ..., "border-collapse:separate")` |
| `KNOWN-DEVIATIONS.md` | 5 documented CSS deviations | PRESENT | KD-03-01 (@page size), KD-03-02 (orphans/widows), KD-03-03 (two-pass boundary shift), KD-03-04 (counter recursion), KD-03-05 (UAX#14 deferred — added in aefe4f1) |
| `tests/Muonroi.Pdf.Tests/Layout/BlockLayoutTests.cs` | SC1 margin collapse tests | PRESENT | 3 tests, all pass |
| `tests/Muonroi.Pdf.Tests/Layout/InlineLayoutTests.cs` | SC2 baseline + Vietnamese break tests | PRESENT | 5 tests (4 vertical-align + 1 Vietnamese+Latin), all pass |
| `tests/Muonroi.Pdf.Tests/Layout/TableLayoutTests.cs` | SC3 table layout tests | PRESENT | 4 tests pass |
| `tests/Muonroi.Pdf.Tests/Layout/PaginationTests.cs` | SC4+SC5 pagination tests | PRESENT | 4 tests, all pass |
| `tests/Muonroi.Pdf.Tests/Layout/LayoutEngineIntegrationTests.cs` | Integration tests | PRESENT | 3 tests, all pass |
| `tests/Muonroi.Pdf.Governance.Tests/Policies/DefaultStrictPolicyTests.cs` | LAYOUT-07 border-collapse regression test | PRESENT | 1 test: `DefaultStrictPolicy_BorderCollapseCollapse_EmitsPolicyViolation` — passes in Governance.Tests suite |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` | 24 | `return null;` | INFO | Intentional: display:none nodes return null, guarded by `if (boxNode != null)` in CollectChildren. Not a stub. |

No blockers from anti-pattern scan. The `return null` at line 24 of BoxTreeBuilder is intentional display:none handling, not a placeholder.

### Test Results

```
dotnet test tests/Muonroi.Pdf.Tests/
Passed! - Failed: 0, Passed: 23, Skipped: 0, Total: 23, Duration: 29 ms

dotnet test tests/Muonroi.Pdf.Governance.Tests/
Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 162 ms
```

24/24 total tests pass across both test projects.

### Gap Closure History

The initial verification (same date, earlier run) found 2 gaps. Both were closed in commits `d5342a9` and `aefe4f1`:

| Gap | Commit | Resolution |
|-----|--------|-----------|
| LAYOUT-07: `border-collapse:collapse` not checked in `DefaultStrictPolicy` | d5342a9 | Added check at lines 119-121 of DefaultStrictPolicy.cs + governance regression test |
| SC2: No Vietnamese+Latin inline break test | aefe4f1 | Added `VietnamesePlusLatin_MixedText_ProducesOneElementPerSpaceSeparatedToken` test + KD-03-05 deviation doc |

### Accepted Deviations (from KNOWN-DEVIATIONS.md)

All 5 deviations are documented with rationale and deferral phase. No deviation blocks any Phase 3 success criterion:
- **KD-03-01**: `@page { size }` ignored — SC4 tests @page headers, not size
- **KD-03-02**: orphans/widows unimplemented — not required by SC1–SC5
- **KD-03-03**: two-pass page boundary shift — SC5 counter tests pass despite theoretical edge case
- **KD-03-04**: counter() in header/footer not recursive — SC5 plain-text footer fully covered
- **KD-03-05**: UAX#14 not implemented — space-based splitting correct for Phase 3 Latin+Vietnamese; deferred to Phase 4 font integration

---
_Verified: 2026-05-27_
_Verifier: Claude (gsd-verifier)_
