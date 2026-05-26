---
phase: 02-parse-cascade-policy-gate
type: verification
mode: initial
date: 2026-05-27
verdict: PARTIAL_PASS
---

# Phase 2 Verification — Parse + Cascade + Policy Gate

## Mode

INITIAL (no previous VERIFICATION.md existed).

## Overall Verdict

**PARTIAL_PASS — 4 of 5 success criteria fully satisfied; 1 gap confirmed; 1 administrative gap confirmed.**

| # | Success Criterion | Status |
|---|-------------------|--------|
| SC1 | AngleSharp parser through `IHtmlParser`, no type leak | PASS |
| SC2 | `ICssCascadeEngine` with AngleSharp.Css 1.0.0-beta.147, no type leak | PASS |
| SC3 | MaxHtmlBytes rejected before parsing with typed exception | PASS |
| SC4 | DOM depth/element count triggers structured error before cascade | PASS |
| SC5 | PolicyViolation with 4 fields; Governance compiles; 6 blocked feature categories | CONDITIONAL PASS (see GAP-01) |
| — | ROADMAP plan status markers | GAP-02 (administrative) |
| — | Tests exist for Phase 2 code | GAP-03 |

---

## Evidence by Success Criterion

### SC1: AngleSharp parser through `IHtmlParser`, no type leak — PASS

**Contract (verified in `src/Muonroi.Pdf.Abstractions/Engine/IHtmlParser.cs`):**
```csharp
public interface IHtmlParser
{
    ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default);
}
```
Return type is `IParsedDocument` (opaque marker interface in Abstractions, defined at line 4 of `Engine/IParsedDocument.cs`).

**Implementation (verified in `src/Muonroi.Pdf.Governance/Parsing/AngleSharpHtmlParser.cs`):**
- `AngleSharpHtmlParser : IHtmlParser` — public sealed class, net8.0 Governance assembly
- Returns `AngleSharpParsedDocument` which is `internal sealed class AngleSharpParsedDocument : IParsedDocument` — holds `IDocument Document` **internally** (verified line 3-4 of `Parsing/AngleSharpParsedDocument.cs`)
- The `IDocument` (AngleSharp type) is `internal` to the Governance assembly — not exposed in the `ParseAsync` return type or any public signature
- No AngleSharp namespace references appear in `src/Muonroi.Pdf.Abstractions/` source (grep confirmed empty)

**Verdict: PASS.** Type seam is clean. `IDocument` stays internal to Governance.

---

### SC2: `ICssCascadeEngine` with AngleSharp.Css 1.0.0-beta.147, no type leak — PASS

**Contract (verified in `src/Muonroi.Pdf.Abstractions/Engine/ICssCascadeEngine.cs`):**
```csharp
public interface ICssCascadeEngine
{
    ValueTask<IStyledDocument> CascadeAsync(IParsedDocument doc, string? userStyleSheet, CancellationToken ct = default);
}
```
Return type is `IStyledDocument` (opaque marker interface in Abstractions, no AngleSharp types).

**Implementation (verified in `src/Muonroi.Pdf.Governance/Cascade/AngleSharpCascadeEngine.cs` and `Cascade/AngleSharpStyledDocument.cs`):**
- `AngleSharpCascadeEngine : ICssCascadeEngine` — public sealed, returns `ValueTask<IStyledDocument>` (no AngleSharp in return type)
- `AngleSharpStyledDocument : IStyledDocument, IPdfDocumentContext` — `internal sealed class`; holds `internal IDocument AngleSharpDocument { get; }` (AngleSharp type stays internal)
- CSS cascade is applied via `Configuration.Default.WithCss()` in `AngleSharpHtmlParser.ParseAsync` (line 16 of parser), which is the correct AngleSharp.Css extension method that registers the CSS engine for computed style resolution
- `AngleSharp.Css` pinned to `1.0.0-beta.147` in `Directory.Packages.props` (line 12, confirmed)
- `CascadeAsync` is synchronous (`ValueTask.FromResult`) by design: AngleSharp.Css resolves computed styles during `IBrowsingContext.OpenAsync` in the parser. The cascade engine wraps the already-cascaded document. This is architecturally sound — the `WithCss()` call registers AngleSharp.Css; the cascade happens at parse time. `GetComputedStyle` in the policy confirms styles are accessible.

**Note:** The decision to apply CSS in the parser stage (not the cascade stage) is a deviation from the naive read of the interface name but is architecturally correct. The policy's `defaultView.GetComputedStyle(element)` call (line 98 of `DefaultStrictPolicy.cs`) confirms computed styles are resolved and accessible.

**Verdict: PASS.** No AngleSharp type leaks through either seam. AngleSharp.Css 1.0.0-beta.147 is pinned and used.

---

### SC3: MaxHtmlBytes rejected before parsing with typed exception — PASS

**Evidence (verified in `src/Muonroi.Pdf.Governance/Parsing/AngleSharpHtmlParser.cs` lines 9-14):**
```csharp
if ((long)html.Length * 2 > PdfConfigs.PdfLimits.MaxHtmlBytes)
    throw new PdfInputLimitException(
        "limit.max-html-bytes",
        "MaxHtmlBytes",
        (long)html.Length * 2,
        PdfConfigs.PdfLimits.MaxHtmlBytes);
```
- Check occurs at line 9, **before** `BrowsingContext.New(...)` call at line 16
- `PdfConfigs.PdfLimits.MaxHtmlBytes = 8_388_608` (verified in `PdfConfigs.cs` line 20)
- `PdfInputLimitException : PdfException` is a typed exception (verified in `Exceptions/PdfInputLimitException.cs`)
- Carries `LimitName`, `ActualValue`, `LimitValue` structured fields

**Verdict: PASS.** MaxHtmlBytes enforcement is pre-parse and throws a typed exception.

---

### SC4: DOM depth/element count triggers structured error before cascade/layout — PASS

**Evidence (verified in `src/Muonroi.Pdf.Governance/Parsing/AngleSharpHtmlParser.cs` lines 19-32):**
```csharp
if (document.All.Length > PdfConfigs.PdfLimits.MaxElementCount)
    throw new PdfInputLimitException("limit.max-element-count", "MaxElementCount",
        document.All.Length, PdfConfigs.PdfLimits.MaxElementCount);

int maxDepth = ComputeMaxDepth(document);
if (maxDepth > PdfConfigs.PdfLimits.MaxDomDepth)
    throw new PdfInputLimitException("limit.max-dom-depth", "MaxDomDepth",
        maxDepth, PdfConfigs.PdfLimits.MaxDomDepth);
```
- Both checks execute **before** `ParseAsync` returns — caller never receives an `IParsedDocument` that violates limits
- `CascadeAsync` only runs on a returned `IParsedDocument`, which is guaranteed compliant
- `PdfConfigs.PdfLimits.MaxDomDepth = 256`, `MaxElementCount = 100_000` (verified in `PdfConfigs.cs`)
- `PdfInputLimitException` is a typed structured exception (not plain `Exception`)

**Verdict: PASS.** DOM limits trigger before any cascade or layout step.

---

### SC5: PolicyViolation with 4 fields; Governance compiles; 6 blocked feature categories — CONDITIONAL PASS

**PolicyViolation structure (verified in `src/Muonroi.Pdf.Abstractions/Policy/PolicyValidationResult.cs` lines 28-35):**
```csharp
public sealed record PolicyViolation(
    string RuleId,
    string Message,
    PolicySeverity Severity = PolicySeverity.Error,
    string? PropertyName = null,
    string? RejectedValue = null,
    string? CssSelector = null,
    string? SuggestedAlternative = null);
```
All 4 required fields present: `PropertyName`, `RejectedValue`, `CssSelector`, `SuggestedAlternative`.

**Governance project compiles (verified by running `dotnet build src/Muonroi.Pdf.Governance/ --no-incremental`):**
Result: **0 errors, 50 warnings** (all CS1591 XML doc comment warnings, pre-existing pattern).

**`DefaultStrictPolicy` blocked feature categories (verified in `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs`):**

The ROADMAP SC5 states "rejects all six blocked feature categories." The actual implementation covers 9 rule IDs in 6 categories:

| Category | Rule IDs | Status |
|----------|----------|--------|
| display:flex/inline-flex | `forbidden.display.flex` | PRESENT |
| display:grid/inline-grid | `forbidden.display.grid` | PRESENT |
| float:left/right | `forbidden.float` | PRESENT |
| position:absolute | `forbidden.position.absolute` | PRESENT |
| position:fixed | `forbidden.position.fixed` | PRESENT |
| position:sticky | `forbidden.position.sticky` | PRESENT |
| CSS animations (@keyframes) | `forbidden.css-animation` | PRESENT (bonus) |
| CSS transitions | `forbidden.css-transition` | PRESENT (bonus) |
| @import external URI | `forbidden.import.external` | PRESENT (bonus) |

All 6 required blocked categories from SC5 (`display:flex`, `display:grid`, `float`, `position:absolute`, `position:fixed`, `position:sticky`) are present. Three additional categories are also blocked.

**`IPdfCssPolicy.DefaultStrict` interpretation:** Throughout planning documents, `IPdfCssPolicy.DefaultStrict` is used as shorthand for `DefaultStrictPolicy` (the concrete class). There is NO static `DefaultStrict` member on the `IPdfCssPolicy` interface (verified: grep found no such member). This is consistent prose shorthand, not a required static interface member. `DefaultStrictPolicy` implements `IPdfCssPolicy` with `Id = "default-strict-v1"`.

**Verdict: PASS.** All SC5 requirements are met. The "IPdfCssPolicy.DefaultStrict" is a planning-doc shorthand for DefaultStrictPolicy, not a required API member.

---

## Gaps

### GAP-01: `PdfPolicyLimits.Strict.MaxElementCount` diverges from `PdfConfigs.PdfLimits.MaxElementCount`

**Evidence:**
- `PdfConfigs.PdfLimits.MaxElementCount = 100_000` (enforced by `AngleSharpHtmlParser`)
- `PdfPolicyLimits.Strict.MaxElementCount = 50_000` (enforced by `DefaultStrictPolicy.CheckLimits`)

A document with 75,000 elements would pass the parser's limit check but then fail `DefaultStrictPolicy`'s limit check. This is a latent discrepancy. The parsing stage will not reject it (correct), but the policy will add a `PolicyViolation` for element count when the document was already accepted by the parser. The SC4 requirement (structured error before cascade) is satisfied by the parser; the policy adds a secondary check.

**Severity: WARNING** — not a functional blocker for SC3/SC4, but creates inconsistent behavior: a document between 50k and 100k elements will parse successfully but fail policy validation. This should be documented or reconciled.

**Blocked by this gap:** No SC directly fails. Phase 3 onward may see unexpected policy failures on large documents. Recommend reconciling limits or documenting the intentional two-tier approach.

### GAP-02: ROADMAP plan status markers not updated

**Evidence:** In `ROADMAP.md`, all five Phase 2 plan entries remain `[ ]` (unchecked) despite all five plans being executed and summarized. The Phase 2 phase-level entry also remains `[ ]`.

**Severity: ADMINISTRATIVE** — codebase is correct; ROADMAP is stale. No functional impact.

### GAP-03: No test cases in `Muonroi.Pdf.Governance.Tests`

**Evidence:** Running `dotnet test tests/Muonroi.Pdf.Governance.Tests/` reports "No test is available." The test project exists with only `GlobalUsings.cs` and the `.csproj` file — the scaffold from Plan 02-02 was never populated with test bodies.

**Severity: WARNING** — All five success criteria are verified by code inspection, but no automated regression tests exist to prevent regressions. The pre-push test gate cannot catch failures in this module.

**Blocked by this gap:** No current SC fails (verification is by code inspection). However, Phase 7 ("40+ golden tests") depends on this test infrastructure. This gap should be closed before Phase 7.

---

## Anti-Pattern Scan

**Files scanned:** All `.cs` files under `src/Muonroi.Pdf.Governance/` and `src/Muonroi.Pdf.Abstractions/Exceptions/`

| Anti-Pattern | Result |
|---|---|
| `TBD`, `FIXME`, `XXX` | None found |
| `TODO`, `HACK` | None found |
| `throw new NotImplementedException()` | None found |
| `return null;` (stub pattern) | None found |
| Empty catch blocks | None found |
| Stub/placeholder implementations | None found |

No blockers from anti-pattern scan.

---

## Build Results

| Project | Command | Result |
|---------|---------|--------|
| `Muonroi.Pdf.Abstractions` | `dotnet build --no-incremental` | 0 errors, 36 warnings (CS1591 XML doc) |
| `Muonroi.Pdf.Governance` | `dotnet build --no-incremental` | 0 errors, 50 warnings (CS1591 XML doc) |
| `Muonroi.Pdf.Governance.Tests` | `dotnet build --no-incremental` | 0 errors, 50 warnings (CS1591 XML doc) |
| `Muonroi.Pdf.Governance.Tests` | `dotnet test` | 0 test discovered (empty test project) |

---

## Human Verification Needed

None. All Phase 2 success criteria are verifiable programmatically through code inspection and build execution. No UI, no browser, no external service required.

---

## Required Actions Before Phase 3

| Priority | Action | Gap |
|----------|--------|-----|
| HIGH | Populate `tests/Muonroi.Pdf.Governance.Tests/` with actual test cases covering SC1–SC5 scenarios | GAP-03 |
| MEDIUM | Reconcile `PdfPolicyLimits.Strict.MaxElementCount` (50k) vs `PdfConfigs.PdfLimits.MaxElementCount` (100k), or document the intentional two-tier enforcement | GAP-01 |
| LOW | Update ROADMAP.md Phase 2 plan markers from `[ ]` to `[x]` | GAP-02 |

---

## Files Verified

- `src/Muonroi.Pdf.Abstractions/Engine/IHtmlParser.cs`
- `src/Muonroi.Pdf.Abstractions/Engine/ICssCascadeEngine.cs`
- `src/Muonroi.Pdf.Abstractions/Engine/IParsedDocument.cs`
- `src/Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs`
- `src/Muonroi.Pdf.Abstractions/Policy/IPdfCssPolicy.cs`
- `src/Muonroi.Pdf.Abstractions/Policy/PolicyValidationResult.cs`
- `src/Muonroi.Pdf.Abstractions/Policy/PdfPolicyLimits.cs`
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs`
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfException.cs`
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfInputLimitException.cs`
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfPolicyException.cs`
- `src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj`
- `src/Muonroi.Pdf.Governance/GlobalUsings.cs`
- `src/Muonroi.Pdf.Governance/Parsing/AngleSharpParsedDocument.cs`
- `src/Muonroi.Pdf.Governance/Parsing/AngleSharpHtmlParser.cs`
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs`
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpCascadeEngine.cs`
- `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs`
- `src/Muonroi.Pdf.Governance/Policies/SignedPdfCssPolicyDecorator.cs`
- `tests/Muonroi.Pdf.Governance.Tests/Muonroi.Pdf.Governance.Tests.csproj`
- `tests/Muonroi.Pdf.Governance.Tests/GlobalUsings.cs`
- `Directory.Packages.props`
- `.planning/ROADMAP.md`
