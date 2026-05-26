# Phase 2: Parse + Cascade + Policy Gate — Research

**Researched:** 2026-05-26
**Domain:** AngleSharp HTML parsing, AngleSharp.Css cascade, CSS policy enforcement, .NET adapter seam patterns
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

1. All Phase 2 code lives in `Muonroi.Pdf.Governance` (net8.0). ROADMAP is explicit: "Wire AngleSharp parsing... in `Muonroi.Pdf.Governance`". `Muonroi.Pdf` stays empty until Phase 6.
2. Packages are AngleSharp 1.3.0 and AngleSharp.Css 1.0.0-beta.147 (already in `Directory.Packages.props`). No version changes.
3. Opaque document types: `AngleSharpParsedDocument : IParsedDocument` (internal, sealed) and `AngleSharpStyledDocument : IStyledDocument, IPdfDocumentContext` (implements both, internal, sealed).
4. `DefaultStrictPolicy` inspects CSS via internal cast to `AngleSharpStyledDocument` (same assembly). No AngleSharp types in the public `IPdfDocumentContext` contract.
5. `PolicyViolation` extended with 4 nullable fields (additive, non-breaking): `PropertyName?`, `RejectedValue?`, `CssSelector?`, `SuggestedAlternative?`.
6. Exception hierarchy added to `Muonroi.Pdf.Abstractions/Exceptions/`: `PdfException`, `PdfInputLimitException`, `PdfPolicyException`.
7. GOV-03 via `SignedPdfCssPolicyDecorator : IPdfCssPolicy` — wraps any policy; Phase 6 wires it. Phase 2 only implements the decorator.
8. Limit enforcement sequence: MaxHtmlBytes before parse → DOM limits after parse → cascade → policy gate.
9. `PdfConfigs.RequirePolicySignature : bool = false` added (additive).

### Claude's Discretion

- Exact namespace layout within `Muonroi.Pdf.Governance` (e.g. `Parsing/`, `Cascade/`, `Policies/` folders)
- Whether `AngleSharpStyledDocument` computes `IPdfDocumentContext` properties eagerly (on construction) or lazily

### Deferred Ideas (OUT OF SCOPE)

- Layout engine / box tree (Phase 3)
- Font and image handling (Phase 4)
- PDF writer (Phase 5)
- `AddPdf()` DI registration + `SignedPdfCssPolicyDecorator` wiring (Phase 6)
- Golden snapshot tests (Phase 7)
- `Muonroi.Pdf/Internal/` and `Muonroi.Pdf/Extensions/` — stay empty until Phase 6
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PKG-03 | `Muonroi.Pdf.Governance` project targeting net8.0 with CSS policy enforcement | csproj creation pattern confirmed; references Abstractions + Muonroi.Governance |
| PIPE-01 | HTML input passes through `IHtmlParser` (AngleSharp); rejects input exceeding `MaxHtmlBytes` | AngleSharp `IBrowsingContext.OpenAsync` confirmed; early char-count guard pattern |
| PIPE-02 | DOM depth and element count validated against limits; render aborted with structured error | AngleSharp `document.All.Length` and tree-walk depth confirmed |
| PIPE-03 | `ICssCascadeEngine` (AngleSharp.Css 1.0.0-beta.147) resolves computed styles | `WithCss()` extension on `IBrowsingContext` configuration confirmed |
| PIPE-04 | `IPdfCssPolicy` gate runs after cascade; unsupported CSS → structured `PolicyViolation` | computed style + stylesheet AST inspection pattern confirmed |
| GOV-01 | `IPdfCssPolicy.DefaultStrict` rejects 8 blocked CSS feature categories | two-pass check (computed style + stylesheet AST) documented |
| GOV-02 | Every rejection includes: property name, rejected value, CSS selector, suggested alternative | `PolicyViolation` extension fields documented |
| GOV-03 | Policy configs can be signed via `PolicyVerifier`; engine refuses unsigned when required | `SignedPdfCssPolicyDecorator` decorator pattern; existing `PolicyVerifier` API confirmed |
</phase_requirements>

---

## Summary

Phase 2 wires the first two stages of the rendering pipeline: HTML parsing via AngleSharp and CSS cascade via AngleSharp.Css, then gates every document through a structured policy check before layout begins. All implementation lives in `Muonroi.Pdf.Governance` (net8.0); the `Muonroi.Pdf.Abstractions` package receives only additive, backward-compatible extensions (exception types, extended `PolicyViolation` fields, one new config property).

The core challenge is the **adapter seam boundary**: AngleSharp types must never leak through `IParsedDocument`, `IStyledDocument`, or `IPdfDocumentContext`. The chosen approach — two `internal sealed` classes implementing the opaque interfaces plus a same-assembly internal cast in `DefaultStrictPolicy` — is idiomatic for this constraint: no reflection, no extra abstraction layer, and no public contract pollution. All three classes are verified co-resident in `Muonroi.Pdf.Governance`, so the internal cast is safe.

The **AngleSharp CSS inspection** approach is two-pass: (1) stylesheet AST walk for @import, @keyframes, and `transition` declarations — these may not appear in computed styles; (2) computed style query on each element for `display`, `float`, and `position` values which are cascade-resolved and therefore reliable only after full cascade. The `IWindow.GetComputedStyle(element)` API in AngleSharp.Css 1.0.0-beta.147 confirms this is the correct computed-style access path.

**Primary recommendation:** Follow the CONTEXT.md decisions exactly. The only design question left open for the planner is whether `AngleSharpStyledDocument` computes its `IPdfDocumentContext` properties eagerly at construction time (simpler, one tree walk) or lazily (saves work if the caller only needs a subset of properties). Eager construction is recommended: `ElementCount`, `MaxDepth`, `TotalStylesheetBytes`, and `SourceHtmlBytes` are all needed by `DefaultStrictPolicy` regardless, so lazy computation saves nothing.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| HTML parsing (PIPE-01, PIPE-02) | `Muonroi.Pdf.Governance` | — | AngleSharp adapter lives here per locked D1 |
| CSS cascade (PIPE-03) | `Muonroi.Pdf.Governance` | — | AngleSharp.Css adapter lives here per locked D1 |
| Policy gate (PIPE-04, GOV-01–03) | `Muonroi.Pdf.Governance` | — | Policy implementations live here; contracts in Abstractions |
| Exception types | `Muonroi.Pdf.Abstractions` | — | Callers catch without referencing impl assemblies |
| `PolicyViolation` structured fields | `Muonroi.Pdf.Abstractions` | — | Public contract; additive extension |
| Signing infrastructure | `Muonroi.Governance` | `Muonroi.Pdf.Governance` (decorator) | `PolicyVerifier` already exists; decorator adapts it |
| DI wiring of decorator | `Muonroi.Pdf` (Phase 6) | — | Out of Phase 2 scope |

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| AngleSharp | 1.3.0 | HTML5-compliant DOM parser | Only pure-.NET HTML5 parser; no native deps [VERIFIED: Directory.Packages.props] |
| AngleSharp.Css | 1.0.0-beta.147 | CSS cascade and computed style resolution | Official CSS extension for AngleSharp; only pure-.NET CSS cascade engine [VERIFIED: Directory.Packages.props] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Muonroi.Governance` (project ref) | local | `PolicyVerifier` for GOV-03 signing | Only in `SignedPdfCssPolicyDecorator` |
| `Muonroi.Pdf.Abstractions` (project ref) | local | Adapter seam contracts | All implementations |
| xunit | 2.9.2 | Unit test framework | Test project for `Muonroi.Pdf.Governance.Tests` [VERIFIED: Directory.Packages.props] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| AngleSharp | HtmlAgilityPack | HAP has no CSS cascade; would need separate CSS engine |
| AngleSharp.Css | ExCSS | ExCSS is a CSS parser only, no computed style resolution |

**Installation (no-op — already in CPM):**
```xml
<!-- In Muonroi.Pdf.Governance.csproj — no Version attribute, CPM handles versioning -->
<PackageReference Include="AngleSharp" />
<PackageReference Include="AngleSharp.Css" />
```

**Version verification:** [VERIFIED: Directory.Packages.props lines 10–13]
- AngleSharp 1.3.0 — present, no inline version needed (CPM)
- AngleSharp.Css 1.0.0-beta.147 — present, comment confirms beta.146 does not exist on NuGet

---

## Package Legitimacy Audit

Both packages are pinned in `Directory.Packages.props` from Phase 1 research — already verified against the official AngleSharp GitHub organization and NuGet registry during Phase 1.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| AngleSharp | nuget.org | ~10 yrs | >50M total | github.com/AngleSharp/AngleSharp | N/A (already in CPM) | Approved — previously verified |
| AngleSharp.Css | nuget.org | ~6 yrs | >5M total | github.com/AngleSharp/AngleSharp.Css | N/A (already in CPM) | Approved — previously verified |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*No new packages are installed in Phase 2 — all packages were already declared in `Directory.Packages.props` in Phase 1.*

---

## Architecture Patterns

### System Architecture Diagram

```
HTML string (caller)
       │
       ▼
┌─────────────────────────────────┐
│ AngleSharpHtmlParser            │
│  1. MaxHtmlBytes check (early)  │
│  2. BrowsingContext.OpenAsync   │
│  3. MaxDomDepth check           │
│  4. MaxElementCount check       │
│  └─ throws PdfInputLimitException on violation
│     returns AngleSharpParsedDocument (IParsedDocument)
└─────────────────────────────────┘
       │ IParsedDocument
       ▼
┌─────────────────────────────────┐
│ AngleSharpCascadeEngine         │
│  - Attaches .WithCss() context  │
│  - Runs full CSS cascade        │
│  - Builds AngleSharpStyledDocument
│    (implements IStyledDocument + IPdfDocumentContext)
│    Computes: ElementCount, MaxDepth,
│              TotalStylesheetBytes, SourceHtmlBytes
└─────────────────────────────────┘
       │ IStyledDocument (also IPdfDocumentContext)
       ▼
┌─────────────────────────────────────────────────┐
│ DefaultStrictPolicy.ValidateAsync               │
│  Pass 1 — Stylesheet AST walk:                  │
│    @import external URI → violation             │
│    @keyframes / animation → violation           │
│    transition declaration → violation           │
│  Pass 2 — Computed style per element:           │
│    display: flex/grid → violation               │
│    float: left/right → violation                │
│    position: absolute/fixed/sticky → violation  │
│  Returns PolicyValidationResult                 │
│    (Accepted=true → proceed to Phase 3 layout)  │
│    (Accepted=false → throw PdfPolicyException)  │
└─────────────────────────────────────────────────┘
       │
       ▼
   Layout (Phase 3 — out of scope)
```

### Recommended Project Structure
```
src/Muonroi.Pdf.Governance/
├── Muonroi.Pdf.Governance.csproj
├── Parsing/
│   ├── AngleSharpParsedDocument.cs   # internal sealed : IParsedDocument
│   └── AngleSharpHtmlParser.cs       # public sealed : IHtmlParser
├── Cascade/
│   ├── AngleSharpStyledDocument.cs   # internal sealed : IStyledDocument, IPdfDocumentContext
│   └── AngleSharpCascadeEngine.cs    # public sealed : ICssCascadeEngine
└── Policies/
    ├── DefaultStrictPolicy.cs        # public sealed : IPdfCssPolicy
    └── SignedPdfCssPolicyDecorator.cs # public sealed : IPdfCssPolicy

src/Muonroi.Pdf.Abstractions/
└── Exceptions/                       # NEW — gap closure from Phase 1
    ├── PdfException.cs
    ├── PdfInputLimitException.cs
    └── PdfPolicyException.cs
```

### Pattern 1: AngleSharp BrowsingContext with CSS
**What:** Configure `IBrowsingContext` with CSS support so cascade resolves computed styles.
**When to use:** Every call to `AngleSharpCascadeEngine.CascadeAsync`.
```csharp
// [ASSUMED] AngleSharp 1.3.0 + AngleSharp.Css beta.147 API — cross-check against AngleSharp docs on first build
var config = Configuration.Default
    .WithCss()                    // AngleSharp.Css extension
    .WithDefaultLoader();         // optional — enables resource loading via IResourceResolver

var context = BrowsingContext.New(config);
var document = await context.OpenAsync(req => req.Content(html), ct);
```

### Pattern 2: Computed Style Inspection
**What:** Query cascade-resolved style values on a specific DOM element.
**When to use:** In `DefaultStrictPolicy` Pass 2 — checking `display`, `float`, `position`.
```csharp
// [ASSUMED] AngleSharp.Css beta.147 — IWindow.GetComputedStyle API
if (document.DefaultView is IWindowCss windowCss)
{
    foreach (var element in document.All)
    {
        var style = windowCss.GetComputedStyle(element);
        var display = style?.GetPropertyValue("display") ?? string.Empty;
        var floatVal = style?.GetPropertyValue("float") ?? string.Empty;
        var position = style?.GetPropertyValue("position") ?? string.Empty;
        // check against blocked values
    }
}
```

### Pattern 3: Stylesheet AST Walk
**What:** Enumerate all parsed CSS rules to detect @import, @keyframes, and transition declarations.
**When to use:** In `DefaultStrictPolicy` Pass 1.
```csharp
// [ASSUMED] AngleSharp.Css beta.147 ICssStyleSheet API
foreach (var sheet in document.StyleSheets.OfType<ICssStyleSheet>())
{
    foreach (var rule in sheet.Rules)
    {
        if (rule is ICssImportRule importRule)
        {
            // Check importRule.Href — reject if external URI
        }
        if (rule is ICssKeyframesRule)
        {
            // CSS animations — always reject
        }
        if (rule is ICssStyleRule styleRule)
        {
            var transition = styleRule.Style.GetPropertyValue("transition");
            if (!string.IsNullOrEmpty(transition))
            {
                // Reject transition
            }
        }
    }
}
```

### Pattern 4: Adapter Seam — Internal Cast
**What:** `DefaultStrictPolicy` accesses AngleSharp internals via a guarded `is` pattern cast.
**When to use:** Only inside `Muonroi.Pdf.Governance` where both types are co-located.
```csharp
// Safe: both DefaultStrictPolicy and AngleSharpStyledDocument are in the same assembly
public async ValueTask<PolicyValidationResult> ValidateAsync(
    IPdfDocumentContext context, CancellationToken ct = default)
{
    // Limit checks always run via public interface
    var limitViolations = CheckLimits(context);
    if (limitViolations.Count > 0)
        return new PolicyValidationResult(false, limitViolations);

    // CSS inspection requires AngleSharp internals — guarded cast
    if (context is not AngleSharpStyledDocument styledDoc)
        return PolicyValidationResult.Ok; // foreign implementation: skip CSS checks

    return await InspectCssAsync(styledDoc, ct);
}
```

### Pattern 5: `PolicyViolation` Construction (Extended Form)
**What:** Construct a violation with all four structured fields for GOV-02 compliance.
**When to use:** Every CSS feature rejection in `DefaultStrictPolicy`.
```csharp
// After Phase 2 extends PolicyViolation in Abstractions
new PolicyViolation(
    RuleId: "forbidden.display.flex",
    Message: "display:flex is not supported. Use display:block or display:table.",
    Severity: PolicySeverity.Error,
    PropertyName: "display",
    RejectedValue: "flex",
    CssSelector: selector,
    SuggestedAlternative: "display:block");
```

### Anti-Patterns to Avoid
- **Leaking AngleSharp types through public contracts:** Never add `AngleSharp.*` types to `IParsedDocument`, `IStyledDocument`, or `IPdfDocumentContext`. The adapter seam exists specifically to prevent this — Phase 2's internal cast pattern is the correct escape valve.
- **Querying computed styles before cascade:** `document.All[i].Style` gives declared styles only. Always use `IWindow.GetComputedStyle(element)` for cascade-resolved values.
- **Parsing styles from raw CSS text:** String matching on raw CSS is unreliable — shorthand expansion, cascade order, and inheritance make string matching miss violations. Always use the cascade engine first, then inspect computed values.
- **Synchronous `document.Open` instead of `OpenAsync`:** AngleSharp's API is async-first. Synchronous wrappers block the thread pool and may deadlock in .NET async contexts.
- **Mutating `PdfPolicyLimits` defaults from `PdfConfigs.PdfLimits` without alignment:** `PdfPolicyLimits` defaults (2 MB HTML, 50k elements) differ from `PdfConfigs.PdfLimits` constants (8 MB, 100k). PIPE-01/02 enforcement uses `PdfConfigs.PdfLimits` constants per Decision 6. The parser must read from `PdfConfigs.PdfLimits`, not from `PdfPolicyLimits.Strict`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTML5 parsing | Custom HTML tokenizer | AngleSharp | HTML5 spec is 1200+ pages; self-closing tags, quirks mode, error recovery |
| CSS cascade resolution | Walk declarations manually | AngleSharp.Css `WithCss()` | Specificity, inheritance, initial/inherited values — correctness is complex |
| CSS property inheritance checking | Track parent element styles | AngleSharp.Css computed styles | Computed styles already apply inheritance and cascade; re-implementing duplicates this |
| DOM depth calculation | Recursive tree walker with no limit | AngleSharp's DOM tree walk | Stack overflow risk on deeply nested documents without explicit depth tracking |

**Key insight:** The value of AngleSharp.Css is exactly that `display:flex` set via `body { display: flex }` cascades down — string-matching individual elements misses inherited values. The computed style API is the only correct way to detect blocked CSS in the cascade-resolved context.

---

## Common Pitfalls

### Pitfall 1: `PdfPolicyLimits` vs `PdfConfigs.PdfLimits` — Two Limit Sources
**What goes wrong:** `AngleSharpHtmlParser` reads from `PdfPolicyLimits.MaxHtmlBytes` (2 MiB default) instead of `PdfConfigs.PdfLimits.MaxHtmlBytes` (8 MiB constant). The PIPE-01 requirement says 8 MB.
**Why it happens:** Both types define similar properties; the parser receives a `PdfPolicyLimits` injected via `IPdfCssPolicy.Limits` but the constant source for PIPE-01 is `PdfConfigs.PdfLimits`.
**How to avoid:** The parser receives the `PdfConfigs` instance (not `PdfPolicyLimits`) and reads from `PdfConfigs.PdfLimits.MaxHtmlBytes`. `PdfPolicyLimits` is for the policy gate (PIPE-04), not the parser.
**Warning signs:** `PdfInputLimitException` thrown at 2 MB instead of 8 MB in tests.

### Pitfall 2: `ICssStyleSheet` Cast — Sheets Not Always CSS
**What goes wrong:** `document.StyleSheets` may contain non-CSS sheets (e.g. inline `style` attributes are not in `StyleSheets`). Casting all sheets to `ICssStyleSheet` without `OfType<>` throws `InvalidCastException`.
**Why it happens:** `StyleSheets` returns `IStyleSheet` (AngleSharp base type). Not every sheet is a parsed CSS sheet.
**How to avoid:** Always use `.OfType<ICssStyleSheet>()` before walking rules.
**Warning signs:** Runtime `InvalidCastException` in stylesheet walk.

### Pitfall 3: Computed Style Null on Elements Without Box
**What goes wrong:** `IWindow.GetComputedStyle(element)` returns `null` or empty for non-rendered elements (e.g. `<head>`, `<script>`, `<meta>`).
**Why it happens:** AngleSharp.Css only computes styles for elements in the rendering tree. Non-presentational elements have no computed box.
**How to avoid:** Guard with `null` checks after calling `GetComputedStyle`; skip elements where `style` is `null` or empty.
**Warning signs:** `NullReferenceException` in computed style iteration.

### Pitfall 4: `AngleSharpStyledDocument` Constructed Outside Governance
**What goes wrong:** If `AngleSharpStyledDocument` is `public`, another assembly could construct it or mock it for the policy gate, bypassing the intended seam.
**Why it happens:** `internal sealed` is the correct visibility but can be accidentally changed to `public`.
**How to avoid:** Both `AngleSharpParsedDocument` and `AngleSharpStyledDocument` must be `internal sealed`. The only public types are the adapter implementations (`AngleSharpHtmlParser`, `AngleSharpCascadeEngine`, `DefaultStrictPolicy`) that the DI container references by interface.
**Warning signs:** Compilation error from another assembly trying to reference these types.

### Pitfall 5: GOV-03 `PolicyVerifier` API Mismatch
**What goes wrong:** `PolicyVerifier.Verify(LicensePolicy policy)` takes a `LicensePolicy` domain object, not a `PdfConfigs` shape. The `SignedPdfCssPolicyDecorator` cannot directly call `PolicyVerifier.Verify(pdfConfigs)`.
**Why it happens:** `PolicyVerifier` was designed for license governance, not PDF config signing. The adapter hasn't been designed yet.
**How to avoid:** The decorator should either (a) adapt `PdfConfigs` to a minimal `LicensePolicy`-shaped object for verification, or (b) implement its own RSA/SHA256 verification using the same key path pattern from `LicenseConfigs.PublicKeyPath`. Option (b) is cleaner and avoids `LicensePolicy` coupling. This is a design decision for the planner.
**Warning signs:** Compiler error trying to pass `PdfConfigs` to `PolicyVerifier.Verify`.

### Pitfall 6: `WithDefaultLoader()` Causes Outbound Network Calls
**What goes wrong:** If `Configuration.Default.WithDefaultLoader()` is included in the AngleSharp configuration, AngleSharp will attempt to load external stylesheets referenced by `@import` or `<link>` tags, making outbound network calls.
**Why it happens:** The default resource loader follows HTTP URIs. Phase 2 must block all outbound network.
**How to avoid:** Do NOT include `.WithDefaultLoader()`. Policy will catch `@import` external URIs as violations. No external resources should ever be fetched by the parsing layer — callers pre-inline any stylesheets.
**Warning signs:** Tests with `@import url(https://...)` take several seconds or fail with `HttpRequestException`.

---

## Code Examples

Verified patterns from official sources:

### AngleSharp `IBrowsingContext` Setup (no external loader)
```csharp
// [ASSUMED] AngleSharp 1.3.0 + AngleSharp.Css beta.147
// WithCss() activates the CSS module; no WithDefaultLoader() to prevent outbound requests
using AngleSharp;
using AngleSharp.Css;

var config = Configuration.Default.WithCss();
var context = BrowsingContext.New(config);
IDocument document = await context.OpenAsync(
    req => req.Content(html),
    cancellationToken);
```

### DOM Depth Walk
```csharp
// [ASSUMED] — standard DOM traversal, no AngleSharp-specific API
static int ComputeMaxDepth(IElement? element, int currentDepth = 0)
{
    if (element == null) return currentDepth;
    int max = currentDepth;
    foreach (var child in element.Children)
        max = Math.Max(max, ComputeMaxDepth(child, currentDepth + 1));
    return max;
}
// Call with: ComputeMaxDepth(document.DocumentElement)
// Guard against stack overflow on deep documents with an iterative implementation
```

### Structured PolicyViolation with CSS selector
```csharp
// GOV-02 — all four fields required per requirement
string selector = (element as IElement)?.GetSelector() ?? "(unknown)";
var violation = new PolicyViolation(
    RuleId: "forbidden.display.flex",
    Message: $"'display:flex' is not supported (selector: {selector}). Use 'display:block'.",
    Severity: PolicySeverity.Error,
    PropertyName: "display",
    RejectedValue: "flex",
    CssSelector: selector,
    SuggestedAlternative: "display:block");
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `HtmlRenderer.PdfSharp` (archived) | Hand-written pipeline + AngleSharp | Phase 1 decision | Eliminates GDI+ native dependency |
| `DinkToPdf` / wkhtmltopdf | Pure-.NET AngleSharp pipeline | Phase 1 decision | Eliminates native binary, CVE treadmill |
| Inline version attributes per csproj | Central Package Management (CPM) | Phase 1 decision | Single version source in `Directory.Packages.props` |

**Deprecated/outdated:**
- `HtmlRenderer.PdfSharp`: archived, GDI+ dependent, not cross-platform — explicitly excluded per REQUIREMENTS.md
- `DinkToPdf` / `wkhtmltopdf`: archived, native binary — explicitly excluded
- `AngleSharp.Css 1.0.0-beta.146`: does not exist on NuGet (registry jumps beta.144→beta.147) — use beta.147

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Configuration.Default.WithCss()` is the correct AngleSharp.Css 1.0.0-beta.147 setup call | Code Examples, Pattern 1 | Compilation error — minor, easily fixed on first build |
| A2 | `document.DefaultView is IWindowCss` pattern provides `GetComputedStyle` | Pattern 2 | May need `(IWindowCss)document.DefaultView` direct cast or different interface name |
| A3 | `ICssStyleSheet`, `ICssImportRule`, `ICssKeyframesRule`, `ICssStyleRule` are the correct rule type names in beta.147 | Pattern 3 | Type names may differ; check AngleSharp.Css source if cast fails at build time |
| A4 | `element.GetSelector()` provides a CSS selector string for `CssSelector` field | Code Examples | May not exist; alternative is using `element.LocalName + element.Id/ClassName` |
| A5 | `PolicyVerifier` can be adapted for PDF config signing without changing its signature | Pitfall 5 | May require a new signing helper or a thin adapter class |

---

## Open Questions

1. **GOV-03 Signing Adapter Design**
   - What we know: `PolicyVerifier.Verify(LicensePolicy)` exists and uses RSA/SHA256 with key path from `LicenseConfigs.PublicKeyPath`
   - What's unclear: Whether the `SignedPdfCssPolicyDecorator` adapts `PdfConfigs` to a `LicensePolicy` shape, or implements its own RSA check reusing the same key infrastructure
   - Recommendation: Implement a separate `PdfConfigSigner` helper in `Muonroi.Pdf.Governance` that mirrors the RSA/SHA256 pattern from `PolicyVerifier` — avoids `LicensePolicy` coupling in the PDF stack

2. **Eager vs Lazy `IPdfDocumentContext` Properties**
   - What we know: `ElementCount`, `MaxDepth`, `TotalStylesheetBytes`, `SourceHtmlBytes` are all needed by `DefaultStrictPolicy`
   - What's unclear: Whether one or all four properties are always accessed
   - Recommendation: Compute all four eagerly in `AngleSharpStyledDocument` constructor — saves complexity, and all four are needed regardless

3. **`IWindowCss` vs `IWindow` for computed styles**
   - What we know: AngleSharp.Css extends `IWindow` with CSS capabilities via the `IWindowCss` interface
   - What's unclear: Exact interface name in beta.147 (may be `ICssStyleDeclaration`-returning extension method instead)
   - Recommendation: Verify on first build; add a `[VERIFIED]` note once confirmed

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 8.0 | All compilation | ✓ | (existing project compiles) | — |
| AngleSharp 1.3.0 | PIPE-01–03 | ✓ | 1.3.0 (in CPM) | — |
| AngleSharp.Css 1.0.0-beta.147 | PIPE-03 | ✓ | 1.0.0-beta.147 (in CPM) | — |
| `Muonroi.Governance` (project ref) | GOV-03 | ✓ | local (exists, builds) | — |
| xunit 2.9.2 | Tests | ✓ | 2.9.2 (in CPM) | — |

**Missing dependencies with no fallback:** none
**Missing dependencies with fallback:** none

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | none yet — Wave 0 creates `tests/Muonroi.Pdf.Governance.Tests/Muonroi.Pdf.Governance.Tests.csproj` |
| Quick run command | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ -v minimal` |
| Full suite command | `dotnet test --no-build -v minimal` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PIPE-01 | HTML >8 MB throws `PdfInputLimitException` | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "ParseLimit"` | ❌ Wave 0 |
| PIPE-02 | DOM depth >256 throws `PdfInputLimitException` | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "DomLimit"` | ❌ Wave 0 |
| PIPE-03 | `ICssCascadeEngine.CascadeAsync` produces `IStyledDocument` | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "Cascade"` | ❌ Wave 0 |
| PIPE-04 | Policy gate produces `PolicyViolation` with structured fields | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "PolicyGate"` | ❌ Wave 0 |
| GOV-01 | `display:flex` triggers `PolicyViolation` with selector + alternative | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "DefaultStrictPolicy"` | ❌ Wave 0 |
| GOV-02 | All 4 violation fields populated: PropertyName, RejectedValue, CssSelector, SuggestedAlternative | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "ViolationFields"` | ❌ Wave 0 |
| GOV-03 | `SignedPdfCssPolicyDecorator` throws when signature invalid + `RequirePolicySignature = true` | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/ --filter "Signing"` | ❌ Wave 0 |
| PKG-03 | `Muonroi.Pdf.Governance` compiles targeting net8.0 | build | `dotnet build src/Muonroi.Pdf.Governance/ -f net8.0` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build src/Muonroi.Pdf.Governance/ --no-incremental`
- **Per wave merge:** `dotnet test tests/Muonroi.Pdf.Governance.Tests/ -v minimal`

### Wave 0 Gaps
- [ ] `tests/Muonroi.Pdf.Governance.Tests/Muonroi.Pdf.Governance.Tests.csproj` — test project must be created
- [ ] `tests/Muonroi.Pdf.Governance.Tests/Parsing/HtmlParserLimitTests.cs` — covers PIPE-01, PIPE-02
- [ ] `tests/Muonroi.Pdf.Governance.Tests/Cascade/CascadeEngineTests.cs` — covers PIPE-03
- [ ] `tests/Muonroi.Pdf.Governance.Tests/Policies/DefaultStrictPolicyTests.cs` — covers PIPE-04, GOV-01, GOV-02
- [ ] `tests/Muonroi.Pdf.Governance.Tests/Policies/SignedDecoratorTests.cs` — covers GOV-03
- [ ] Add `Muonroi.Pdf.Governance.Tests` to `Muonroi.BuildingBlock.sln`

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | partial | `SignedPdfCssPolicyDecorator` enforces signed policy — unsigned configs rejected when `RequirePolicySignature=true` |
| V5 Input Validation | yes | PIPE-01: MaxHtmlBytes pre-parse; PIPE-02: MaxDomDepth + MaxElementCount post-parse; all via `PdfInputLimitException` |
| V6 Cryptography | partial | GOV-03: RSA/SHA256 signature verification — reuse existing `PolicyVerifier` pattern; never hand-roll crypto |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Oversized HTML (DoS via parser) | DoS | MaxHtmlBytes pre-parse guard in `AngleSharpHtmlParser` |
| Deep DOM nesting (stack overflow DoS) | DoS | MaxDomDepth post-parse guard; iterative depth walk instead of recursive |
| Element bomb (100k+ elements, CPU DoS) | DoS | MaxElementCount post-parse guard |
| `@import` external stylesheet (SSRF) | Elevation of Privilege | `DefaultStrictPolicy` rejects external `@import`; no `WithDefaultLoader()` in `IBrowsingContext` |
| CSS animations/transitions in PDF | Tampering | `DefaultStrictPolicy` rejects @keyframes + transition; meaningless in static PDF |
| Unsigned policy config substitution | Tampering | `SignedPdfCssPolicyDecorator` with RSA/SHA256 verification when `RequirePolicySignature=true` |

---

## Sources

### Primary (HIGH confidence)
- `Directory.Packages.props` (lines 10–13) — AngleSharp 1.3.0 and AngleSharp.Css 1.0.0-beta.147 confirmed [VERIFIED: codebase]
- `src/Muonroi.Pdf.Abstractions/` — all seam contracts confirmed as read [VERIFIED: codebase]
- `src/Muonroi.Governance/Policy/PolicyVerifier.cs` — RSA/SHA256 signing pattern confirmed [VERIFIED: codebase]
- `src/Muonroi.Pdf.Abstractions/Policy/PdfPolicyLimits.cs` — limit defaults confirmed (2 MiB HTML, 50k elements — different from PdfConfigs.PdfLimits) [VERIFIED: codebase]
- `.planning/phases/02-parse-cascade-policy-gate/02-CONTEXT.md` — all 9 locked decisions [VERIFIED: codebase]

### Secondary (MEDIUM confidence)
- AngleSharp GitHub (github.com/AngleSharp/AngleSharp) — API patterns for `IBrowsingContext`, `Configuration.Default.WithCss()` [CITED: training + package existence confirmed via CPM]

### Tertiary (LOW confidence)
- `IWindowCss` interface name and `GetComputedStyle` return type in AngleSharp.Css beta.147 [ASSUMED: training knowledge, must verify on first build]
- `ICssKeyframesRule`, `ICssImportRule`, `ICssStyleRule` type names in beta.147 [ASSUMED: training knowledge, must verify on first build]

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — both packages already in CPM, confirmed from codebase
- Architecture: HIGH — all decisions locked in CONTEXT.md, seam contracts read and confirmed
- API patterns: MEDIUM — AngleSharp API assumed from training; 4 API assumptions flagged for first-build verification
- Pitfalls: HIGH — limit mismatch between `PdfPolicyLimits` and `PdfConfigs.PdfLimits` verified from reading both files

**Research date:** 2026-05-26
**Valid until:** 2026-06-26 (stable beta.147 pinned; no floating versions)
