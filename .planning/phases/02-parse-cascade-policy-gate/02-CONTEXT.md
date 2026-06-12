# Phase 2 Context: Parse + Cascade + Policy Gate

**Phase**: 2 of 9
**Name**: Parse + Cascade + Policy Gate
**Date captured**: 2026-05-26
**Mode**: Headless autonomous (no interactive discussion)

---

## Domain

HTML input is parsed via AngleSharp, CSS is cascaded via AngleSharp.Css 1.0.0-beta.147, and every unsupported CSS construct is caught with a structured diagnostic before any layout code runs. All Phase 2 implementation lives in `Muonroi.Pdf.Governance` (net8.0). No layout, no PDF writing, no DI wiring — those are Phases 3–6.

Requirements locked: PKG-03, PIPE-01, PIPE-02, PIPE-03, PIPE-04, GOV-01, GOV-02, GOV-03.

---

## Canonical References

- `.planning/REQUIREMENTS.md` — locked requirements PKG-03, PIPE-01–04, GOV-01–03
- `.planning/ROADMAP.md` — Phase 2 success criteria (SC1–SC5)
- `.planning/phases/01-abstractions-contracts/01-CONTEXT.md` — Phase 1 decisions (adapter seam shapes, opaque types)
- `src/Muonroi.Pdf.Abstractions/` — all public contracts Phase 2 must implement
- `src/Muonroi.Pdf.Abstractions/Engine/IHtmlParser.cs` — adapter seam: `ParseAsync(string html, ct) : ValueTask<IParsedDocument>`
- `src/Muonroi.Pdf.Abstractions/Engine/ICssCascadeEngine.cs` — adapter seam: `CascadeAsync(IParsedDocument, string?, ct) : ValueTask<IStyledDocument>`
- `src/Muonroi.Pdf.Abstractions/Policy/IPdfCssPolicy.cs` — policy gate contract + `IPdfDocumentContext`
- `src/Muonroi.Pdf.Abstractions/Policy/PolicyValidationResult.cs` — `PolicyViolation`, `PolicySeverity`
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — `PdfConfigs.PdfLimits` constants (MaxHtmlBytes, MaxDomDepth, MaxElementCount)
- `src/Muonroi.Pdf.Governance/` — Phase 2 implementation target (currently empty `Policies/` dir only)
- `src/Muonroi.Governance/Policy/PolicyVerifier.cs` — GOV-03 signing infrastructure (already exists)
- `Directory.Packages.props` — AngleSharp 1.3.0, AngleSharp.Css 1.0.0-beta.147, PdfSharpCore 1.3.65 already declared

---

## Existing State (verified 2026-05-26)

| Component | Status |
|-----------|--------|
| `Muonroi.Pdf.Abstractions` | Complete — netstandard2.0, all seams defined |
| `Directory.Packages.props` | AngleSharp + AngleSharp.Css beta.147 + SixLabors.Fonts + PdfSharpCore already present |
| `Muonroi.Pdf.Governance/Policies/` | Directory exists, empty |
| `Muonroi.Pdf.Governance/*.csproj` | Missing — must create |
| `Muonroi.Pdf/Extensions/`, `Internal/` | Directories exist, empty — Phase 6 scope |
| `Muonroi.Governance/Policy/PolicyVerifier.cs` | Exists — usable for GOV-03 |
| Exception types in Abstractions | Missing — gap from Phase 1, Phase 2 adds them |
| `PolicyViolation` structured fields | Missing `PropertyName`, `RejectedValue`, `CssSelector`, `SuggestedAlternative` — Phase 2 extends |

---

## Implementation Decisions

### Decision 1: Package layout — all Phase 2 code lives in `Muonroi.Pdf.Governance`

**Problem**: `AngleSharpHtmlParser`, `AngleSharpCascadeEngine`, and `DefaultStrictPolicy` could live in `Muonroi.Pdf.Governance` or `Muonroi.Pdf`.

**Decision**: Everything in `Muonroi.Pdf.Governance` (net8.0). ROADMAP explicitly states "Wire AngleSharp parsing... in `Muonroi.Pdf.Governance`". `Muonroi.Pdf` stays empty until Phase 6 (DI wiring).

**Why**: Keeps parsing/cascade/policy as a cohesive governance concern. Phase 6's `AddPdf()` wires these implementations into DI without needing them in the main package.

**csproj refs needed**:
```xml
<ProjectReference Include="..\Muonroi.Pdf.Abstractions\Muonroi.Pdf.Abstractions.csproj" />
<ProjectReference Include="..\Muonroi.Governance\Muonroi.Governance.csproj" />
<PackageReference Include="AngleSharp" />
<PackageReference Include="AngleSharp.Css" />
```

---

### Decision 2: Opaque document type — `AngleSharpParsedDocument` + `AngleSharpStyledDocument`

**Problem**: `IHtmlParser` returns `IParsedDocument` (opaque marker). `ICssCascadeEngine` takes `IParsedDocument` and returns `IStyledDocument`. The policy gate takes `IPdfDocumentContext`. These are three different interfaces that must be bridged.

**Decision**:
- `AngleSharpParsedDocument : IParsedDocument` — holds `AngleSharp.Dom.IDocument` internally; sealed, internal to Governance
- `AngleSharpStyledDocument : IStyledDocument, IPdfDocumentContext` — holds styled DOM; computes `ElementCount`, `MaxDepth`, `TotalStylesheetBytes`, `SourceHtmlBytes` by walking the AngleSharp DOM on construction

**Why**: `IStyledDocument` and `IPdfDocumentContext` are both opaque from the Abstractions perspective. Since `AngleSharpCascadeEngine` and `DefaultStrictPolicy` both live in `Muonroi.Pdf.Governance`, the concrete class can implement both interfaces. No extra adapter needed.

**`IPdfDocumentContext` implementation** (computed at cascade time):
- `ElementCount` — `document.All.Length`
- `MaxDepth` — tree walk to find max nesting depth
- `TotalStylesheetBytes` — sum of `sheet.Href`/inline text byte lengths
- `SourceHtmlBytes` — passed through from `AngleSharpHtmlParser`

---

### Decision 3: CSS property inspection for DefaultStrictPolicy — internal cast pattern

**Problem**: `DefaultStrictPolicy.ValidateAsync(IPdfDocumentContext context, ct)` receives the opaque `IPdfDocumentContext`. To implement GOV-01 (detect `display:flex`, `float`, etc.), the policy must inspect CSS declarations. The public contract doesn't expose this.

**Decision**: `DefaultStrictPolicy` casts `context` to `AngleSharpStyledDocument` directly (same assembly = safe, no reflection). If the cast fails (a foreign implementation), the policy returns `PolicyValidationResult.Ok` for CSS property checks (limit checks always run via the public interface properties).

```csharp
// Inside DefaultStrictPolicy.ValidateAsync
if (context is not AngleSharpStyledDocument styledDoc)
    return await CheckLimitsOnly(context, ct);
// Walk styledDoc.AngleSharpDocument.All to check computed styles
```

**Why**: Avoids polluting `IPdfDocumentContext` (a public Abstractions contract) with AngleSharp-specific inspection APIs. Since policy and document types are co-located in Governance, the internal coupling is intentional and contained. A future `IStyleInspector` interface can formalize this in a later phase if needed.

---

### Decision 4: `PolicyViolation` structured fields — additive extension in Abstractions

**Problem**: GOV-02 requires `PolicyViolation` to carry `property name, rejected value, CSS selector, and suggested alternative`. Current `PolicyViolation` has only `RuleId`, `Message`, `Severity`.

**Decision**: Extend `PolicyViolation` in `Muonroi.Pdf.Abstractions` (additive, non-breaking):
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

All new fields are nullable with null defaults. Existing construction sites `new PolicyViolation("rule", "msg")` remain valid.

**Why**: Structured fields enable tooling, dashboards, and future rule editors to consume violation details without parsing message strings. Callers that only need `RuleId`/`Message` are unaffected.

---

### Decision 5: Exception types — add to `Muonroi.Pdf.Abstractions` in Phase 2

**Problem**: PIPE-01/PIPE-02 require "typed exception and structured error" for limit violations. No exception types exist in Abstractions. Phase 1 didn't implement them (gap).

**Decision**: Add to `Muonroi.Pdf.Abstractions/Exceptions/`:
- `PdfException : Exception` — base with `string RuleId` + `string Detail` properties
- `PdfInputLimitException : PdfException` — thrown by `IHtmlParser` when `MaxHtmlBytes`, `MaxDomDepth`, or `MaxElementCount` exceeded; carries `long ActualValue` and `long LimitValue`
- `PdfPolicyException : PdfException` — thrown by the policy pipeline when `PolicyValidationResult.Accepted == false`; carries `IReadOnlyList<PolicyViolation> Violations`

**Why**: Exception types belong in Abstractions (not Governance) so any caller of `IMPdfService.RenderAsync` can catch them without referencing implementation assemblies. This is a gap closure from Phase 1 — safe to add in Phase 2 since the Abstractions package is backward-compatible with additions.

---

### Decision 6: Limit enforcement sequence — PIPE-01 in parser, PIPE-02 in cascade, PIPE-04 after cascade

**Problem**: PIPE-01–04 form a sequence. The exact enforcement point for each limit matters: reject early (less work) vs. reject late (more context).

**Decision**:
1. **PIPE-01 (MaxHtmlBytes)** — enforced in `AngleSharpHtmlParser.ParseAsync` BEFORE DOM construction: `if (html.Length * 2 > PdfConfigs.PdfLimits.MaxHtmlBytes) throw new PdfInputLimitException(...)`. Uses char count × 2 as a conservative UTF-16 byte estimate.
2. **PIPE-02 (MaxDomDepth + MaxElementCount)** — enforced in `AngleSharpHtmlParser.ParseAsync` AFTER DOM construction (AngleSharp must parse to count); stats collected then validated before returning `IParsedDocument`.
3. **PIPE-03 (cascade)** — `AngleSharpCascadeEngine.CascadeAsync` calls `.WithCss()` to apply AngleSharp.Css styles.
4. **PIPE-04 (policy gate)** — `DefaultStrictPolicy.ValidateAsync` called by the engine orchestrator AFTER cascade, BEFORE layout.

**Why**: Enforcing MaxHtmlBytes before parsing is the cheapest check (no DOM allocation). Enforcing DOM limits after parsing but before returning ensures the caller never receives an `IParsedDocument` that violates limits. Policy runs last because it needs computed styles.

---

### Decision 7: GOV-01 blocked feature list — implementation approach

**Problem**: GOV-01 requires blocking 8 specific CSS features. Implementation options: (a) string-match on raw CSS declarations, (b) check computed style values on every element, (c) check AngleSharp `IRule` AST.

**Decision**: Two-pass check in `DefaultStrictPolicy`:
1. **Stylesheet pass** — walk `AngleSharpStyledDocument.AngleSharpDocument.StyleSheets`, check `@import` rules for external URIs; check for `@keyframes` rules (animations) and `transition` properties in rule declarations
2. **Element pass** — for each element, check computed `display`, `float`, `position` values via AngleSharp's `IWindow.ComputedStyle(element)` shorthand

Blocked features per GOV-01:
| Feature | Detection method | Suggested alternative in violation |
|---------|-----------------|-----------------------------------|
| `display:flex` | computed style | `display:block` or `display:table` |
| `display:grid` | computed style | `display:table` |
| `float:left/right` | computed style | Use `display:table` layout |
| `position:absolute` | computed style | `position:static` |
| `position:fixed` | computed style | `position:static` |
| `position:sticky` | computed style | `position:static` |
| CSS animations (`@keyframes`, `animation:`) | stylesheet AST | Remove animation properties |
| CSS transitions (`transition:`) | stylesheet AST | Remove transition properties |
| `@import` with external URI | stylesheet AST rule | Inline the stylesheet |

**Why**: Computed style check (pass 2) catches inherited and cascade-resolved values, not just declared values. Stylesheet AST check (pass 1) catches animations/transitions which may not appear in computed styles until layout.

---

### Decision 8: GOV-03 — signed policy via decorator

**Problem**: GOV-03 requires policy configs to be verifiable via `Muonroi.Governance.Policy.PolicyVerifier`. `PdfConfigs` doesn't currently have a signing requirement flag.

**Decision**:
1. **Add to `PdfConfigs`** (Abstractions, additive): `public bool RequirePolicySignature { get; set; } = false;`
2. **`SignedPdfCssPolicyDecorator : IPdfCssPolicy`** in `Muonroi.Pdf.Governance` — wraps any `IPdfCssPolicy`; validates signature via `PolicyVerifier` on `ValidateAsync`; forwards to inner policy when valid; throws `PdfPolicyException` with `PolicyViolation("gov.policy.signature-invalid", ...)` when signature check fails and `RequirePolicySignature = true`
3. **Phase 6 DI wiring** (out of Phase 2 scope) will apply the decorator conditionally when `PdfConfigs.RequirePolicySignature = true`

**Why**: Decorator pattern is the correct approach — it keeps `DefaultStrictPolicy` signature-unaware and lets signing be a cross-cutting concern. The Phase 6 DI code selects the correct decorator. Phase 2 only implements the decorator; wiring happens later.

---

### Decision 9: `Muonroi.Pdf.Governance` csproj structure

**Problem**: PKG-03 requires `Muonroi.Pdf.Governance` targeting `net8.0`. The csproj doesn't exist yet.

**Decision**: Create `src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Description>CSS policy enforcement and HTML/CSS parsing adapters for Muonroi PDF rendering.</Description>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Muonroi.Pdf.Abstractions\Muonroi.Pdf.Abstractions.csproj" />
    <ProjectReference Include="..\Muonroi.Governance\Muonroi.Governance.csproj" />
    <PackageReference Include="AngleSharp" />
    <PackageReference Include="AngleSharp.Css" />
  </ItemGroup>
</Project>
```

Must add to solution `Muonroi.BuildingBlock.sln`.

---

## File Creation Plan

Priority order (strict dependency order):

**Phase 2a — Abstractions gap closure (in `Muonroi.Pdf.Abstractions`):**
1. `Exceptions/PdfException.cs` — base exception
2. `Exceptions/PdfInputLimitException.cs`
3. `Exceptions/PdfPolicyException.cs`
4. Extend `Policy/PolicyViolation` record in `PolicyValidationResult.cs` — add 4 nullable fields
5. Add `RequirePolicySignature : bool` to `PdfConfigs.cs`

**Phase 2b — Governance package setup:**
6. Create `Muonroi.Pdf.Governance.csproj`
7. Add project to solution

**Phase 2c — Parser adapter:**
8. `src/Muonroi.Pdf.Governance/Parsing/AngleSharpParsedDocument.cs`
9. `src/Muonroi.Pdf.Governance/Parsing/AngleSharpHtmlParser.cs` — implements `IHtmlParser`; enforces PIPE-01 + PIPE-02

**Phase 2d — Cascade adapter:**
10. `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs` — implements `IStyledDocument` + `IPdfDocumentContext`
11. `src/Muonroi.Pdf.Governance/Cascade/AngleSharpCascadeEngine.cs` — implements `ICssCascadeEngine`; enforces PIPE-03

**Phase 2e — Policy gate:**
12. `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` — implements `IPdfCssPolicy` with GOV-01 + GOV-02; id = `"default-strict-v1"`
13. `src/Muonroi.Pdf.Governance/Policies/SignedPdfCssPolicyDecorator.cs` — GOV-03 decorator

**Phase 2f — Verification:**
14. `dotnet build` on solution — must pass 0 errors
15. Success criteria SC1–SC5 manual verification

---

## Out of Phase 2 Scope

- Layout engine / box tree (Phase 3)
- Font and image handling (Phase 4)
- PDF writer (Phase 5)
- `AddPdf()` DI registration + `SignedPdfCssPolicyDecorator` wiring (Phase 6)
- Golden snapshot tests (Phase 7)
- `Muonroi.Pdf/Internal/` and `Muonroi.Pdf/Extensions/` — stay empty until Phase 6

---

## Autonomous Gray Area Resolutions

The following decisions were made autonomously (headless mode) with reasoning:

| Gray Area | Decision | Rationale |
|-----------|----------|-----------|
| Where do parser/cascade adapters live? | `Muonroi.Pdf.Governance` | ROADMAP is explicit; keeps governance cohesive |
| How does `DefaultStrictPolicy` inspect CSS? | Internal cast to `AngleSharpStyledDocument` (same assembly) | Avoids polluting public `IPdfDocumentContext` contract |
| `PolicyViolation` structured fields | Additive extension in Abstractions (nullable fields) | Non-breaking; enables structured tooling consumption |
| Exception types | Add to Abstractions (gap from Phase 1) | Callers must catch without referencing impl assemblies |
| GOV-03 signing | Decorator pattern; Phase 6 wires it | Keeps policy implementations signing-unaware |
