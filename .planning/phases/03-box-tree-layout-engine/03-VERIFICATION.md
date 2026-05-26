---
phase: 03-box-tree-layout-engine
verified: 2026-05-27T00:00:00Z
status: gaps_found
score: 3/5 must-haves verified
gaps:
  - truth: "border-collapse:collapse triggers a PolicyViolation naming border-collapse:separate as the alternative"
    status: failed
    reason: "DefaultStrictPolicy.CheckCssFeatures() does not check border-collapse:collapse. The policy checks flex, grid, float, position, @import, and @keyframes — border-collapse is absent from the code. The governance test project (Muonroi.Pdf.Governance.Tests) contains 0 test files, so no regression test guards this requirement."
    artifacts:
      - path: "src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs"
        issue: "Lines 46-118: CheckCssFeatures() has no check for style.GetPropertyValue(\"border-collapse\") == \"collapse\". LAYOUT-07 is not implemented here."
      - path: "tests/Muonroi.Pdf.Tests/Layout/TableLayoutTests.cs"
        issue: "Lines 8-9: Comment claims LAYOUT-07 is covered in Muonroi.Pdf.Governance.Tests, but that project has 0 test methods (dotnet test reports 'No test is available')."
      - path: "tests/Muonroi.Pdf.Governance.Tests/GlobalUsings.cs"
        issue: "Only file in the test project besides the .csproj. No test classes exist."
    missing:
      - "Add border-collapse:collapse check in DefaultStrictPolicy.CheckCssFeatures() using style.GetPropertyValue(\"border-collapse\") and emit PolicyViolation with SuggestedAlternative: \"border-collapse:separate\""
      - "Add a test in Muonroi.Pdf.Governance.Tests verifying DefaultStrictPolicy emits a PolicyViolation with RuleId containing 'border-collapse' when border-collapse:collapse is present in a document"
  - truth: "Inline text with mixed Latin+Vietnamese in the same line breaks at correct Unicode break opportunities"
    status: failed
    reason: "InlineLayoutEngine.WordSeparators contains only: space, tab, newline, carriage return, and U+200B (zero-width space). It does NOT implement Unicode Line Breaking Algorithm (UAX#14). The SC2 criterion requires correct break opportunities for mixed Latin+Vietnamese. While Vietnamese is space-delimited, the success criterion says 'correct Unicode break opportunities' which requires UAX#14 compliance. No test exercises Vietnamese text with mixed Latin characters."
    artifacts:
      - path: "src/Muonroi.Pdf/Internal/Layout/InlineLayoutEngine.cs"
        issue: "Line 9: WordSeparators = { ' ', '\\t', '\\n', '\\r', U+200B }. No UAX#14 Unicode line breaking table lookup. Text.Split() on these separators does not implement Unicode line break classes (BA, BB, ID, AL, etc.)."
      - path: "tests/Muonroi.Pdf.Tests/Layout/InlineLayoutTests.cs"
        issue: "4 tests cover vertical-align variants and single-font line height only. No test uses Vietnamese text (e.g., 'Xin chào Latin mix') to verify break opportunities."
    missing:
      - "Add a test using mixed Vietnamese+Latin text (e.g., 'Xin chào world') that verifies line breaks occur at space boundaries between syllables"
      - "Document the current behavior as a known deviation (KD-03-05) if UAX#14 compliance is deferred, specifying that space-based splitting is accepted for v0.1 given Vietnamese is space-separated"
human_verification:
  - test: "Render a document with <table style='border-collapse:collapse'> through DefaultStrictPolicy.ValidateAsync() and assert the returned PolicyValidationResult has at least one PolicyViolation with SuggestedAlternative containing 'separate'"
    expected: "PolicyValidationResult.Violations.Count > 0 with a violation whose SuggestedAlternative contains 'border-collapse:separate'"
    why_human: "The Muonroi.Pdf.Governance.Tests project currently has zero test methods. The check would fail to even run programmatically. A human must either add the test or execute it manually via a spike program."
---

# Phase 3: Box Tree + Layout Engine — Verification Report

**Phase Goal:** A styled DOM converts to a box tree and lays out into pages with correct block/inline/table formatting, margin collapsing, and pagination
**Verified:** 2026-05-27
**Status:** gaps_found

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Margin collapsing per CSS 2.1 §8.3.1 | ✓ | `BlockLayoutEngine.CollapseMargins()` uses `max(positives) + min(negatives)`; `IsBfcRoot()` gates boundary collapse; 3 passing unit tests (AdjacentBlocks_MarginCollapsesTo_Maximum, AdjacentBlocks_GapIsNotSumOfMargins, BfcRoot_PreservesFirstChildMarginTop) |
| 2 | Inline baseline + Unicode breaks | ✗ | Baseline offsets implemented (lineAscender - boxAscender formula); vertical-align top/middle/bottom/baseline all handled; 4 tests pass. Gap: WordSeparators has only ASCII whitespace + U+200B — no UAX#14 Unicode line breaking. No Vietnamese test exists to confirm break correctness. |
| 3 | Table colspan/rowspan + border-collapse policy | ✗ | colspan/rowspan layout correct (4 tests pass). Gap: `DefaultStrictPolicy.CheckCssFeatures()` has NO check for `border-collapse:collapse`; LAYOUT-07 is unimplemented in code; `Muonroi.Pdf.Governance.Tests` has 0 test methods. |
| 4 | page-break-before:always + @page header repetition | ✓ | `PaginationEngine`: `forceBreak` flag set on `PageBreakBefore == "always"` (line 59); `ApplyHeaderFooter()` called for every page in loop (lines 111-113); header sourced from `IPageRule.TopMarginBoxHtml`. Test `PageBreakBeforeAlways_SecondBlock_IsOnPageIndex1` passes. |
| 5 | counter(pages) via two-pass layout | ✓ | `LayoutEngine.Layout()`: pass 1 with `totalPages=0`, then `RunLayout(totalPages: pass1.PageCount)`; `PaginationEngine` replaces `counter(pages)` and `counter(page)` by string substitution. Tests `CounterPages_ResolvesToCorrectTotalAfterTwoPassLayout` and `CounterPage_ResolvesToOneBased_PageNumber` both pass. |

**Score:** 3/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` | Build box tree from IStyledNode | PRESENT | 274 lines; handles display:none, AnonymousBox wrapping, all table box types, CSS property resolution |
| `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` | Block layout with margin collapse | PRESENT | 185 lines; CollapseMargins(), IsBfcRoot(), DispatchLayout() wired to table/inline/replaced |
| `src/Muonroi.Pdf/Internal/Layout/InlineLayoutEngine.cs` | IFC with baseline alignment | PRESENT | 120 lines; CommitLine() with vertical-align switch; word wrap on spaces |
| `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` | Table layout with colspan/rowspan | PRESENT | 374 lines; two-pass row heights; ComputeAutoColumnWidths() per CSS 2.1 §17.5.2 |
| `src/Muonroi.Pdf/Internal/Layout/PaginationEngine.cs` | Pagination + counters + headers | PRESENT | 188 lines; forced breaks; counter substitution; ApplyHeaderFooter() per page |
| `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` | Two-pass orchestrator | PRESENT | 101 lines; pass1(totalPages=0) → pass2(totalPages=N) |
| `KNOWN-DEVIATIONS.md` | 4 documented CSS deviations | PRESENT | KD-03-01 (@page size), KD-03-02 (orphans/widows), KD-03-03 (two-pass boundary shift), KD-03-04 (counter recursion) |
| `tests/Muonroi.Pdf.Tests/Layout/BlockLayoutTests.cs` | SC1 margin collapse tests | PRESENT | 3 tests, all pass |
| `tests/Muonroi.Pdf.Tests/Layout/InlineLayoutTests.cs` | SC2 baseline tests | PRESENT | 4 tests, all pass. Vietnamese+Latin break test MISSING. |
| `tests/Muonroi.Pdf.Tests/Layout/TableLayoutTests.cs` | SC3 table layout tests | PRESENT | 4 tests pass. border-collapse:collapse policy test MISSING (deferred to governance tests that don't exist). |
| `tests/Muonroi.Pdf.Tests/Layout/PaginationTests.cs` | SC4+SC5 pagination tests | PRESENT | 4 tests, all pass |
| `tests/Muonroi.Pdf.Tests/Layout/LayoutEngineIntegrationTests.cs` | Integration tests | PRESENT | 3 tests, all pass |
| `tests/Muonroi.Pdf.Governance.Tests/` - test classes | LAYOUT-07 border-collapse test | MISSING | Only GlobalUsings.cs exists. `dotnet test` reports "No test is available." |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` | 24 | `return null;` | INFO | Intentional: display:none nodes return null to be filtered by CollectChildren. Not a stub — null is the correct sentinel to signal "exclude this node from the box tree." No issue. |

No blockers from anti-pattern scan. The `return null` at line 24 of BoxTreeBuilder is intentional display:none handling, guarded by an `if (boxNode != null)` check in CollectChildren (line 219).

### Test Results

```
dotnet test tests/Muonroi.Pdf.Tests
Passed! - Failed: 0, Passed: 22, Skipped: 0, Total: 22, Duration: 30 ms

dotnet test tests/Muonroi.Pdf.Governance.Tests
No test is available in Muonroi.Pdf.Governance.Tests.dll
(0 test methods in the project — only GlobalUsings.cs exists)
```

22/22 tests in `Muonroi.Pdf.Tests` pass. The governance test project has no test methods.

### Human Verification Required

**LAYOUT-07 (border-collapse:collapse → PolicyViolation):** Manual verification that the requirement is intentionally unimplemented or that the governance test project needs populating. The code in `DefaultStrictPolicy.CheckCssFeatures()` does not contain any check for `border-collapse`. The comment in `TableLayoutTests.cs` (lines 8-9) claims "LAYOUT-07 is covered in Muonroi.Pdf.Governance.Tests via DefaultStrictPolicy" but the governance test project is empty (0 test methods). Either:
- The implementation was planned but never written (gap requiring a code fix + test), or
- It was intentionally omitted and the comment is stale

Expected: `DefaultStrictPolicy.ValidateAsync()` on a document with `border-collapse:collapse` returns a `PolicyValidationResult` with at least one `PolicyViolation` whose `SuggestedAlternative` contains `"border-collapse:separate"`.

### Gaps Summary

**Gap 1 — LAYOUT-07 not implemented (BLOCKER for SC3):**
The success criterion SC3 states: "border-collapse:collapse triggers a PolicyViolation naming border-collapse:separate as the alternative." The `DefaultStrictPolicy.CheckCssFeatures()` method (src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs, lines 46-118) checks for flex, grid, float, position, @import, and @keyframes — but has no check for `border-collapse:collapse`. The LAYOUT-07 requirement is unimplemented. Additionally, the governance test project at `tests/Muonroi.Pdf.Governance.Tests/` has 0 test files (only GlobalUsings.cs). Running `dotnet test` on that project reports "No test is available." The `TableLayoutTests.cs` comment at lines 8-9 claims this is "covered in Muonroi.Pdf.Governance.Tests" but that is factually incorrect — the tests do not exist.

**Gap 2 — SC2 Unicode break test missing (WARNING for SC2):**
The `InlineLayoutEngine` splits text using `String.Split(WordSeparators)` where `WordSeparators = { ' ', '\t', '\n', '\r', U+200B }`. This handles Vietnamese correctly in practice (Vietnamese is space-delimited), but the SC2 criterion specifically says "breaks at correct Unicode break opportunities" implying UAX#14. No test exercises mixed Vietnamese+Latin text. Without a test, compliance cannot be confirmed. This is either a test gap or a documentation gap (should be recorded as KD-03-05 if word-space splitting is accepted).

---
_Verified: 2026-05-27_
_Verifier: Claude (gsd-verifier)_
