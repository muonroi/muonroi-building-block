---
phase: 02-parse-cascade-policy-gate
plan: 05
type: summary
status: complete
---

# Plan 02-05 Summary: Policy Gate (DefaultStrictPolicy + SignedPdfCssPolicyDecorator)

Implemented the policy gate layer: `DefaultStrictPolicy` enforces GOV-01's 9 blocked CSS features with GOV-02 structured violations, and `SignedPdfCssPolicyDecorator` adds GOV-03 signature verification as a wrapping decorator.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Create `DefaultStrictPolicy` (GOV-01 + GOV-02) | f269a8e |
| 2 | Create `SignedPdfCssPolicyDecorator` (GOV-03) | a9c0e32 |

## Files Created

| File | Purpose |
|------|---------|
| `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` | IPdfCssPolicy with Id="default-strict-v1"; two-pass CSS inspection; limit checks |
| `src/Muonroi.Pdf.Governance/Policies/SignedPdfCssPolicyDecorator.cs` | Decorator that throws PdfPolicyException when RequirePolicySignature=true and signature verifier returns false |

## Deviations from Plan

None. Implementation follows the plan exactly:
- `DefaultStrictPolicy.ValidateAsync` returns `PolicyValidationResult` (never throws)
- `SignedPdfCssPolicyDecorator.ValidateAsync` throws `PdfPolicyException` (not returns failed result) on signature failure
- `Func<bool>` abstraction used to avoid LicensePolicy coupling in the decorator
- For-loop used to iterate `ICssRuleList` (explicit Length + indexer, no IEnumerable assumption)
- `GetComputedStyle` called as extension method on `IWindow` (from `AngleSharp.Dom.WindowExtensions` in AngleSharp.Css)

## GOV-01 Blocked Feature Coverage

| # | Feature | Rule ID | Detection |
|---|---------|---------|-----------|
| 1 | display:flex / inline-flex | forbidden.display.flex | Pass 2 computed style |
| 2 | display:grid / inline-grid | forbidden.display.grid | Pass 2 computed style |
| 3 | float:left or right | forbidden.float | Pass 2 computed style |
| 4 | position:absolute | forbidden.position.absolute | Pass 2 computed style |
| 5 | position:fixed | forbidden.position.fixed | Pass 2 computed style |
| 6 | position:sticky | forbidden.position.sticky | Pass 2 computed style |
| 7 | @keyframes / animation | forbidden.css-animation | Pass 1 stylesheet AST |
| 8 | CSS transition | forbidden.css-transition | Pass 1 stylesheet AST |
| 9 | @import external URI | forbidden.import.external | Pass 1 stylesheet AST |

All violations carry all 4 GOV-02 fields: PropertyName, RejectedValue, CssSelector, SuggestedAlternative.

## Verification

- `dotnet build src/Muonroi.Pdf.Governance/` → 0 errors, 50 warnings (XML comment warnings only)
- All 9 rule IDs present in DefaultStrictPolicy.cs
- `AngleSharpStyledDocument` internal cast confirmed
- `RequirePolicySignature` check and `PdfPolicyException` throw confirmed in decorator

## Known Issues

None.
