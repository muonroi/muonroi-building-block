# Phase 02 Plan 01 — Summary

Added exception hierarchy and extended PolicyViolation/PdfConfigs in Muonroi.Pdf.Abstractions so Phase 2 parse/policy-gate implementations have typed exception contracts.

## Tasks Completed

| # | Description | Commit |
|---|-------------|--------|
| 1 | Create exception hierarchy in Abstractions/Exceptions/ | `3738b13` |
| 2 | Extend PolicyViolation record + add RequirePolicySignature to PdfConfigs | `bd507eb` |

## Deviations

None. Plan executed as specified.

## Files Created

- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfException.cs` — abstract base with `RuleId` + `Detail`
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfInputLimitException.cs` — parse-stage limit violation (ruleId, limitName, actualValue, limitValue)
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfPolicyException.cs` — policy rejection carrying `IReadOnlyList<PolicyViolation>`

## Files Modified

- `src/Muonroi.Pdf.Abstractions/Policy/PolicyValidationResult.cs` — `PolicyViolation` extended to 7 parameters (`PropertyName`, `RejectedValue`, `CssSelector`, `SuggestedAlternative` all nullable/optional); existing call sites unchanged
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — added `public bool RequirePolicySignature { get; set; } = false;`

## Verification

`dotnet build src/Muonroi.Pdf.Abstractions/ --no-incremental` exits 0 with 0 errors (36 pre-existing CS1591 warnings, none introduced by this plan).

## Known Issues

None.
