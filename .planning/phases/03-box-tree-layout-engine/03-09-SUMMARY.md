# Plan 03-09 Summary: Phase 3 Gap Closure (LAYOUT-07 + SC2 Vietnamese Test)

Closed two Phase 3 verification gaps: fixed the missing border-collapse:collapse policy gate (LAYOUT-07) and added the SC2 Vietnamese+Latin tokenisation test with KD-03-05 documentation.

## Tasks Completed

### Task 1: Fix LAYOUT-07 + Governance Test
- **Commit**: `d5342a9`
- `DefaultStrictPolicy.CheckCssFeatures()` Pass 2 now reads `border-collapse` from computed style and emits `PolicyViolation(RuleId: "forbidden.border-collapse.collapse", SuggestedAlternative: "border-collapse:separate")` when value is `"collapse"`.
- `tests/Muonroi.Pdf.Governance.Tests/GlobalUsings.cs`: added `Muonroi.Pdf.Governance.Cascade`, `Parsing`, `Policies` global usings.
- `tests/Muonroi.Pdf.Governance.Tests/Policies/DefaultStrictPolicyTests.cs`: created regression test via `AngleSharpHtmlParser` + `AngleSharpCascadeEngine` pipeline. Governance suite: 0 → 1 test passing.

### Task 2: SC2 Vietnamese Test + KD-03-05
- **Commit**: `aefe4f1`
- `tests/Muonroi.Pdf.Tests/Layout/InlineLayoutTests.cs`: added `VietnamesePlusLatin_MixedText_ProducesOneElementPerSpaceSeparatedToken` — verifies "Xin chào world" tokenises to 3 `PositionedElement`s. Pdf.Tests suite: 22 → 23 tests passing.
- `KNOWN-DEVIATIONS.md`: added KD-03-05 documenting that full UAX#14 line-breaking is deferred to Phase 4 and space-based splitting is accepted for Phase 3.

## Deviations from Plan

None. All required artifacts produced and all verification criteria met.

## Files Modified

| File | Change |
|------|--------|
| `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` | Added border-collapse:collapse check in Pass 2 element loop |
| `tests/Muonroi.Pdf.Governance.Tests/GlobalUsings.cs` | Added 3 global using directives for Governance namespaces |
| `tests/Muonroi.Pdf.Governance.Tests/Policies/DefaultStrictPolicyTests.cs` | Created (new file) — LAYOUT-07 regression test |
| `tests/Muonroi.Pdf.Tests/Layout/InlineLayoutTests.cs` | Added Vietnamese+Latin tokenisation test |
| `KNOWN-DEVIATIONS.md` | Added KD-03-05 (UAX#14 deferred) |

## Verification Results

- `dotnet test tests/Muonroi.Pdf.Governance.Tests`: **Passed 1/1** (was 0/0)
- `dotnet test tests/Muonroi.Pdf.Tests`: **Passed 23/23** (was 22/22)

## Known Issues

None.
