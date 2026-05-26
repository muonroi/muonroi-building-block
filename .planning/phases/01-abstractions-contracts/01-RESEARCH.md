# Phase 1: Abstractions + Contracts — Research

**Researched:** 2026-05-26
**Domain:** .NET netstandard2.0 contracts library; NuGet CPM version pinning
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

1. **TFM**: Change `net8.0` → `netstandard2.0`; remove `Muonroi.Core.Abstractions` project reference.
2. **PdfConfigs**: New `PdfConfigs` class bound from `"PdfConfigs"` IConfiguration section with nested `Limits` object (7 constants). `PdfPolicyLimits` remains separate (different concern).
3. **Engine/ adapter seams**: Define all four interfaces (`IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter`) using opaque intermediate marker types that do not leak third-party types.
4. **PdfRenderResult**: Keep metadata-only design (no `Content : Stream`). Add `IReadOnlyList<PolicyViolation> Diagnostics` field.
5. **Directory.Packages.props**: Add all four PDF package CPM entries in Phase 1 (versions below — see Critical Finding on AngleSharp.Css).
6. **Telemetry/**: Add `PdfTelemetryNames.cs` with string constants only (no Meter/Instrument).
7. **IMPdfRenderer<T>**: Keep `RenderAsync` (not `GetTemplateAsync`) — intentional ABST-02 divergence.
8. **IMPdfRendererFactory**: Keep `Get/TryGet(templateId)` (not `CreateRenderer<T>()`) — intentional ABST-03 divergence.

### Claude's Discretion

None specified.

### Deferred Ideas (OUT OF SCOPE)

- Any implementation code (Phase 2+ only)
- `Muonroi.Pdf.Governance` CSS policy enforcement (Phase 2)
- `AddPdf()` DI registration (Phase 6)
- `IMPdfService.RenderAsync<TModel>` redundant generic overload (kept as-is)
- `IPdfTemplate` interface (not required)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PKG-01 | `Muonroi.Pdf.Abstractions` project targeting `netstandard2.0`, zero implementation code | Decision 1: TFM change + reference removal confirmed safe (no compile-time Core.Abstractions types used) |
| PKG-04 | `Muonroi.Pdf.Enterprise` empty stub, `net8.0`, `<IsCommercialPackage>true</IsCommercialPackage>` | No csproj exists yet — must be created as a stub |
| ABST-01 | `IMPdfService` with `RenderAsync`, `RenderMultiPageAsync`, `RenderToBytesAsync` | Already implemented — verified |
| ABST-02 | `IMPdfRenderer<T>` with `GetTemplateAsync` | Intentional divergence (Decision 7): keep `RenderAsync` |
| ABST-03 | `IMPdfRendererFactory` with `CreateRenderer<T>()` | Intentional divergence (Decision 8): keep `Get/TryGet(templateId)` |
| ABST-04 | `IPdfCssPolicy` with `Validate(IStyleSheet) : PolicyResult` | Exists as `ValidateAsync(IPdfDocumentContext)` — verified |
| ABST-05 | `IResourceResolver` bytes-only, `ResolveAsync(string key)` | Exists as `ResolveAsync(Uri, string?, CancellationToken)` — verified (richer signature, same contract intent) |
| ABST-06 | `IFontResolver` bytes-only, `ResolveAsync(family, style)` | Exists as `ResolveAsync(FontRequest, CancellationToken)` — verified |
| ABST-07 | `ICssCascadeEngine` adapter seam | Missing — must be created in `Engine/` |
| ABST-08 | `IHtmlParser` adapter seam | Missing — must be created in `Engine/` |
| ABST-09 | `IImageDecoder` adapter seam | Missing — must be created in `Engine/` |
| ABST-10 | `IPdfWriter` adapter seam | Missing — must be created in `Engine/` |
| ABST-11 | `PdfRenderOptions` record | Already implemented — verified |
| ABST-12 | `PdfRenderResult` with `Content:Stream` and `Diagnostics` | Partial: metadata-only (Decision 4 divergence). Must add `Diagnostics` field. |
| ABST-13 | `PdfConfigs` options class, `SectionName = "PdfConfigs"` | Missing — must be created |
| ABST-14 | `PdfConfigs.Limits` with 7 compile-time constants | Missing — must be created |
</phase_requirements>

---

## Summary

Phase 1 is a pure contracts phase: interfaces, records, and compile-time constants. No algorithms, no third-party library consumption, no I/O. The existing `Muonroi.Pdf.Abstractions` project is approximately 60% complete — 10 of 16 required types exist and are correct. The remaining work is: fix the csproj (TFM + reference), add 6 missing files, update one existing file (`PdfRenderResult`), and add 4 CPM entries to `Directory.Packages.props`.

The most important constraint is the `netstandard2.0` target: it ensures the Abstractions assembly can be referenced by the v0.2 source generator as an analyzer reference, and keeps the contracts portable. The switch is safe because `ITenantContext` appears only in XML comments — not as a compile-time type.

**Critical finding:** `AngleSharp.Css 1.0.0-beta.146` (the version specified in CONTEXT.md Decision 5 and PROJECT.md D4) **does not exist on NuGet**. The registry skips from `beta.144` to `beta.147`. The planner must resolve this before writing `Directory.Packages.props`. See `## Common Pitfalls` for options.

**Primary recommendation:** Implement Phase 1 exactly as specified in CONTEXT.md Decisions 1–8, substitute `AngleSharp.Css 1.0.0-beta.149` (current latest) for the non-existent `beta.146`, and confirm this with the user before committing.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Public rendering contract (`IMPdfService`) | Contracts library | — | Entry point contract; callers program to this interface |
| Adapter seams (`IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter`) | Contracts library | — | Seams live in Abstractions so Phase 2–5 impl packages share them without circular deps |
| Configuration binding (`PdfConfigs`) | Contracts library | DI layer (Phase 6) | Constants defined here; binding wired in `AddPdf()` Phase 6 |
| Policy contract (`IPdfCssPolicy`) | Contracts library | Governance (Phase 2) | Interface + domain types here; `DefaultStrict` impl in Phase 2 |
| Telemetry names | Contracts library | — | String constants only; no Meter/Instrument instances |
| CPM version pinning | Build infrastructure | — | `Directory.Packages.props` — harmless to declare unused versions |

---

## Standard Stack

### Core (Phase 1 — CPM version declarations only, no `<PackageReference>`)

| Library | Verified Version | Purpose | Source |
|---------|-----------------|---------|--------|
| AngleSharp | 1.4.0 | HTML5 parsing (Phase 2) | [VERIFIED: NuGet API] |
| AngleSharp.Css | 1.0.0-beta.149 ⚠️ | CSS cascade (Phase 2) — see Critical Finding | [VERIFIED: NuGet API — beta.146 absent; beta.149 is current latest] |
| SixLabors.Fonts | 2.1.3 | Font loading + shaping (Phase 4) | [VERIFIED: NuGet API — latest in 2.1.x series] |
| PdfSharpCore | 1.3.65 | PDF writing (Phase 5) | [VERIFIED: NuGet API] |

> **Note on AngleSharp.Css:** Version `1.0.0-beta.146` does not exist on NuGet. Available versions in the beta.14x range: `beta.144`, `beta.147`, `beta.149` (no `beta.145`, `beta.146`, `beta.148`). The planner should use `1.0.0-beta.149` (current latest beta) as the recommended pin. This decision needs user confirmation before the commit.

> **Note on SixLabors.Fonts:** `3.0.0` is now available on NuGet (major version bump). Pinning to `2.1.3` (latest stable 2.x) is correct for Phase 1. Phase 4 must evaluate `3.0.0` API compatibility before upgrading — a major version bump may introduce breaking changes in the font shaping API.

### No NuGet references in the Abstractions project itself

The `Muonroi.Pdf.Abstractions` csproj should have **zero `<PackageReference>` entries** after Phase 1. All types it exposes come from:
- `netstandard2.0` BCL (interfaces, records, enums, `Stream`, `CancellationToken`, `ValueTask`, `ReadOnlyMemory<byte>`)
- `System.Diagnostics.DiagnosticSource` — **not needed** for the contracts package since `PdfTelemetryNames.cs` contains only `const string` values, not `Meter`/`ActivitySource` instances

## Package Legitimacy Audit

These packages are not being installed in Phase 1 — they are only declared as CPM version pins in `Directory.Packages.props`. No slopcheck run is required for version-pin-only entries. All four packages are established .NET ecosystem libraries.

| Package | Registry | Disposition |
|---------|----------|-------------|
| AngleSharp | NuGet | Approved — established HTML parser, active GitHub.com/AngleSharp/AngleSharp |
| AngleSharp.Css | NuGet | Approved — same org, beta track only (no stable release) |
| SixLabors.Fonts | NuGet | Approved — SixLabors org, Apache 2.0, widely used |
| PdfSharpCore | NuGet | Approved — community fork of PDFsharp for .NET Core |

---

## Architecture Patterns

### Opaque Seam Pattern (Engine/ interfaces)

Each adapter interface uses opaque marker interfaces as input/output types. This prevents third-party library types from leaking through the seam boundary.

```
IHtmlParser.ParseAsync(string html) → IParsedDocument
                                           ↑ marker interface only
ICssCascadeEngine.CascadeAsync(IParsedDocument, string?) → IStyledDocument
                                                                ↑ marker interface only
IImageDecoder.Decode(ReadOnlySpan<byte>, string) → DecodedImage (sealed record)
IPdfWriter.WriteAsync(IPositionedPageList, PdfRenderOptions, Stream) → ValueTask<long>
                          ↑ marker interface only
```

Implementation packages (Phase 2–5) hold the AngleSharp/SixLabors/PdfSharpCore types internally and only expose them through these seam types.

### Recommended Project Structure (final state after Phase 1)

```
src/Muonroi.Pdf.Abstractions/
├── Engine/
│   ├── DecodedImage.cs          # sealed record
│   ├── ICssCascadeEngine.cs     # adapter seam
│   ├── IHtmlParser.cs           # adapter seam
│   ├── IImageDecoder.cs         # adapter seam
│   ├── IParsedDocument.cs       # marker interface
│   ├── IPositionedPageList.cs   # marker interface
│   ├── IPdfWriter.cs            # adapter seam
│   └── IStyledDocument.cs      # marker interface
├── Policy/
│   ├── IPdfCssPolicy.cs         # ✅ exists
│   ├── PdfPolicyLimits.cs       # ✅ exists
│   └── PolicyValidationResult.cs # ✅ exists
├── Telemetry/
│   └── PdfTelemetryNames.cs    # string constants only
├── GlobalUsings.cs              # needs Metrics using removed
├── IFontResolver.cs             # ✅ exists
├── IMPdfRenderer.cs             # ✅ exists
├── IMPdfService.cs              # ✅ exists
├── IResourceResolver.cs         # ✅ exists
├── PdfConfigs.cs               # NEW
├── PdfHeaderFooter.cs           # ✅ exists
├── PdfMargins.cs                # ✅ exists
├── PdfOrientation.cs            # ✅ exists
├── PdfPageSize.cs               # ✅ exists
├── PdfRenderOptions.cs          # ✅ exists
├── PdfRenderResult.cs           # needs Diagnostics field added
└── Muonroi.Pdf.Abstractions.csproj # needs TFM + ref fix

src/Muonroi.Pdf.Enterprise/      # NEW stub project (PKG-04)
└── Muonroi.Pdf.Enterprise.csproj
```

### Pattern: PdfConfigs with nested Limits

```csharp
// Source: REQUIREMENTS ABST-13, ABST-14
namespace Muonroi.Pdf.Abstractions;

public sealed class PdfConfigs
{
    public const string SectionName = "PdfConfigs";

    public Limits Limits { get; set; } = new();

    public sealed class Limits
    {
        public const long MaxHtmlBytes = 8_388_608;      // 8 MB
        public const int MaxDomDepth = 256;
        public const int MaxElementCount = 100_000;
        public const long MaxImagePixels = 25_000_000;
        public const int MaxPages = 1_000;
        public const long MaxRenderDurationMs = 15_000;
        public const int MaxFontFiles = 32;
    }
}
```

> **Note:** The seven values are `const` (compile-time constants), not `get; set;` properties. This matches ABST-14: "compile-time constants matching the documented values."

### Pattern: Engine marker interfaces

```csharp
// Source: CONTEXT.md Decision 3
namespace Muonroi.Pdf.Abstractions.Engine;

/// Marker — engine holds AngleSharp DOM internally.
public interface IParsedDocument { }

/// Marker — engine holds computed styles internally.
public interface IStyledDocument { }

/// Marker — layout engine output.
public interface IPositionedPageList { }

/// Decoded image data — no image library types exposed.
public sealed record DecodedImage(
    int Width,
    int Height,
    ReadOnlyMemory<byte> Data,
    string ContentType);
```

### Pattern: Adapter seam interfaces

```csharp
// Source: CONTEXT.md Decision 3
public interface IHtmlParser
{
    ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default);
}

public interface ICssCascadeEngine
{
    ValueTask<IStyledDocument> CascadeAsync(
        IParsedDocument doc,
        string? userStyleSheet,
        CancellationToken ct = default);
}

public interface IImageDecoder
{
    DecodedImage Decode(ReadOnlySpan<byte> data, string contentType);
}

public interface IPdfWriter
{
    ValueTask<long> WriteAsync(
        IPositionedPageList pages,
        PdfRenderOptions options,
        Stream destination,
        CancellationToken ct = default);
}
```

### Pattern: PdfTelemetryNames (constants only)

```csharp
// Source: CONTEXT.md Decision 6, REQUIREMENTS TEL-01–TEL-05
namespace Muonroi.Pdf.Abstractions.Telemetry;

public static class PdfTelemetryNames
{
    public const string ActivitySourceName = "Muonroi.BuildingBlock.Pdf";
    public const string OperationMetric    = "pdf.operation";
    public const string PageCountMetric    = "pdf.page_count";
    public const string TemplateIdTag      = "pdf.template_id";
    public const string TenantIdTag        = "tenant.id";
}
```

No `Meter`, `ActivitySource`, or any `System.Diagnostics.Metrics` types — string constants only, so no package reference needed.

### Anti-Patterns to Avoid

- **Exposing AngleSharp types through seam interfaces**: `IHtmlParser` must return `IParsedDocument`, never `IDocument` or any AngleSharp type. Once a third-party type crosses the seam, swapping the library requires changing all callers.
- **Mutable limits as properties**: `PdfConfigs.Limits` constants must be `const`, not `{ get; set; }`. They are compile-time guarantees, not runtime configuration.
- **Putting `Meter`/`ActivitySource` instances in Abstractions**: Telemetry infrastructure belongs in the implementation package (Phase 6); Abstractions only holds the string constants.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CSS cascading | Custom CSS inheritance resolver | AngleSharp.Css (Phase 2) | The cascade algorithm spans hundreds of edge cases; spec compliance requires a full parser + specificity calculator |
| Font shaping | Unicode combining char math | SixLabors.Fonts (Phase 4) | Diacritic stacking and line-breaking require full OpenType GSUB/GPOS tables |
| PDF serialization | Raw PDF object stream writer | PdfSharpCore (Phase 5) | Object cross-reference table, stream compression, font embedding are non-trivial |
| HTML5 parsing | Regex-based tag splitter | AngleSharp (Phase 2) | HTML5 has complex error recovery; spec-conformant parsers differ from intuition |

**Key insight for Phase 1:** Don't hand-roll anything — Phase 1 is contracts only. The adapter seams exist precisely so that these libraries are swappable; the seam design is the deliverable.

---

## Common Pitfalls

### Pitfall 1: AngleSharp.Css Version Does Not Exist
**What goes wrong:** `Directory.Packages.props` declares `AngleSharp.Css 1.0.0-beta.146`, `dotnet restore` fails with "package not found."
**Why it happens:** The version number in CONTEXT.md Decision 5 and PROJECT.md D4 (`1.0.0-beta.146`) does not exist on NuGet. Available versions in the `1.0.0-beta.14x` range are only: `beta.144`, `beta.147`, `beta.149` (no `beta.145`, `beta.146`, `beta.148`).
**How to avoid:** Use `1.0.0-beta.149` (current latest beta). Confirm with the user before committing. Document the substitution in a `Directory.Packages.props` comment.
**Warning signs:** Restore failure `NU1102: Unable to find package AngleSharp.Css with version 1.0.0-beta.146`.

### Pitfall 2: System.Diagnostics.Metrics Global Using
**What goes wrong:** `GlobalUsings.cs` has `global using System.Diagnostics.Metrics;`. On `netstandard2.0` without an explicit `System.Diagnostics.DiagnosticSource` package reference, the namespace may resolve with warnings or fail to resolve.
**Why it happens:** `System.Diagnostics.Metrics` was added in .NET 6 and is not in the `netstandard2.0` BCL. It is available on netstandard2.0 via the `System.Diagnostics.DiagnosticSource` package — but Abstractions should have zero NuGet references.
**How to avoid:** Remove `global using System.Diagnostics.Metrics;` from `GlobalUsings.cs`. The only Metrics-related type in Abstractions is `PdfTelemetryNames.cs` which contains only `const string` — it does not need this namespace.
**Warning signs:** CS8933 warning or compile error after TFM change.

### Pitfall 3: Core.Abstractions Reference Appears Safe But Breaks netstandard2.0
**What goes wrong:** The `<ProjectReference>` to `Muonroi.Core.Abstractions` compiles on `net8.0` but `Muonroi.Core.Abstractions` likely targets `net8.0`. A `netstandard2.0` project cannot reference a `net8.0` project via `<ProjectReference>`.
**Why it happens:** `<ProjectReference>` resolves at build time; targeting incompatibility surfaces as an error only after the TFM change.
**How to avoid:** Remove the `<ProjectReference>` first (Decision 1). Verified: no `ITenantContext` or any other Core.Abstractions type is used in compile scope — only in XML doc comments.
**Warning signs:** `NETSDK1138: The target framework ... is not compatible with` after TFM change.

### Pitfall 4: PdfConfigs.Limits Constants vs Properties
**What goes wrong:** Limits defined as `{ get; set; }` properties break the ABST-14 contract ("compile-time constants"). Callers cannot use them in `switch` expressions, attribute arguments, or other compile-time contexts.
**Why it happens:** Instinct to write auto-properties for all "configuration values."
**How to avoid:** Use `public const long MaxHtmlBytes = 8_388_608;` inside the nested `Limits` class, not `public long MaxHtmlBytes { get; set; } = 8_388_608;`.

### Pitfall 5: ImplicitUsings and netstandard2.0
**What goes wrong:** `<ImplicitUsings>enable</ImplicitUsings>` in the csproj generates different implicit namespaces for `net8.0` vs `netstandard2.0`. On `netstandard2.0`, the implicit set is smaller.
**Why it happens:** The implicit usings list differs by TFM. Some usings currently implicit (e.g. `System.Net.Http`) may disappear after the TFM change.
**How to avoid:** Run `dotnet build` immediately after the TFM change and fix any newly missing using statements. The project's `GlobalUsings.cs` already declares the critical namespaces explicitly, which mitigates most risk.

---

## Runtime State Inventory

SKIPPED — This is a greenfield contracts phase. No rename/refactor/migration is involved. No databases, live services, OS registrations, or build artifacts from a previous name exist.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | All build tasks | ✓ | 10.0.201 | — |
| NuGet feed (nuget.org) | Directory.Packages.props restore | ✓ | — | — |

No missing dependencies. Phase 1 is purely file creation + build verification.

---

## Validation Architecture

`workflow.nyquist_validation` is not set in `.planning/config.json` → treat as enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (repo convention — inferred from existing test projects) |
| Phase 1 test requirement | `dotnet build` on netstandard2.0 target passes with 0 errors |
| Phase 1 has no behavioral logic | No unit tests required — contracts only |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command |
|--------|----------|-----------|-------------------|
| PKG-01 | Assembly targets netstandard2.0, zero implementation | Build verification | `dotnet build src/Muonroi.Pdf.Abstractions` |
| PKG-04 | Enterprise stub compiles, `IsCommercialPackage=true` | Build verification | `dotnet build src/Muonroi.Pdf.Enterprise` |
| ABST-13/14 | `PdfConfigs.Limits` constants have correct values | Compile-time constant check | `dotnet build` (constant values are compile-time) |
| All ABST | All interfaces/records compile with no errors | Build | `dotnet build src/Muonroi.Pdf.Abstractions` |

### Wave 0 Gaps

No test files needed for Phase 1. All verification is `dotnet build` passing. No behavioral code exists to test.

---

## Security Domain

### Applicable ASVS Categories for Phase 1

| ASVS Category | Applies | Note |
|---------------|---------|------|
| V2 Authentication | No | No auth in contracts phase |
| V3 Session Management | No | — |
| V4 Access Control | No | — |
| V5 Input Validation | Partial | `PdfConfigs.Limits` constants define input bounds — values verified against requirements |
| V6 Cryptography | No | — |
| V14 Configuration | Yes | Constants must match documented values exactly (ABST-14) |

### Security-Relevant Findings

- `IResourceResolver` contract signature uses `Uri` (not `string`) — forces callers to parse URIs, which surfaces scheme (enabling `file://` rejection at the contract level). [VERIFIED: read IResourceResolver.cs]
- `IFontResolver` is bytes-only — no file path in the return type. [VERIFIED: read IFontResolver.cs]
- `IPdfDocumentContext` exposes only aggregate counts (ElementCount, MaxDepth), not DOM content — correct for policy validation without leaking template data. [VERIFIED: read IPdfCssPolicy.cs]

---

## State of the Art

| Area | Current Approach | Note |
|------|-----------------|------|
| Managed CSS cascade for .NET | AngleSharp.Css (only viable pure-managed option) | No stable release exists; beta track is the production reality |
| PDF generation (.NET, no native) | PdfSharpCore (fork of PDFsharp) | The original PDFsharp targets Windows-only GDI+; Core fork removes native deps |
| Font shaping | SixLabors.Fonts 2.1.x (pinned) | Handles OpenType shaping including diacritics; no HarfBuzz needed for the declared subset. **3.0.0 now available** — evaluate in Phase 4 before upgrading (potential breaking API changes) |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `AngleSharp.Css 1.0.0-beta.149` (current latest) is a drop-in pin for the intended `beta.146` with no breaking interface changes | Standard Stack | Restore succeeds but Phase 2 implementation may fail at runtime if API changed between betas; mitigated by using the current latest |
| A2 | `SixLabors.Fonts 2.1.3` (latest 2.x) is safe to pin — `3.0.0` is available but has not been evaluated for Phase 4 API compatibility | Standard Stack | No risk in Abstractions phase (version pinned only, not used); Phase 4 must evaluate 3.0.0 API before any upgrade |
| A3 | Removing `global using System.Diagnostics.Metrics;` has no downstream consumers within the Abstractions assembly | Common Pitfalls | No code in Abstractions uses Meter types — verified by reading all source files; risk is LOW |

---

## Open Questions

1. **AngleSharp.Css version substitution**
   - What we know: `1.0.0-beta.146` does not exist. Available beta.14x versions: `beta.144`, `beta.147`, `beta.149`. `beta.149` is the current latest.
   - What's unclear: Whether the PROJECT.md D4 decision author intended a specific beta or simply documented the latest-at-time-of-writing
   - Recommendation: **Planner should use `beta.149` (current latest) and add a code comment in `Directory.Packages.props`; flag for user confirmation before first NuGet restore**

2. **PdfSharpCore version: 1.3.65 vs 1.3.67**
   - What we know: `1.3.65` (as specified in CONTEXT.md Decision 5) exists. Latest is `1.3.67`.
   - What's unclear: Whether `1.3.67` has bug fixes relevant to Phase 5 PDF writing
   - Recommendation: Pin `1.3.65` as specified in CONTEXT.md; upgrading is a Phase 5 decision when the writer is implemented

---

## Sources

### Primary (HIGH confidence)
- NuGet Flat Container API (`api.nuget.org/v3-flatcontainer/*/index.json`) — all four package version lists verified
- Source file reads — all existing `.cs` files in `src/Muonroi.Pdf.Abstractions/` read directly
- `dotnet build` output — confirmed current build succeeds on `net8.0` with 2 non-blocking warnings
- `.planning/phases/01-abstractions-contracts/01-CONTEXT.md` — all 8 decisions read verbatim

### Secondary (MEDIUM confidence)
- [ASSUMED] `System.Diagnostics.Metrics` not in netstandard2.0 BCL — based on .NET API versioning knowledge; verified indirectly by the fact that no NuGet reference is present in the csproj and the type originates in `System.Diagnostics.DiagnosticSource` package

---

## Metadata

**Confidence breakdown:**
- Standard stack versions: HIGH — verified via NuGet API
- Existing implementation state: HIGH — read all source files
- AngleSharp.Css version gap: HIGH — confirmed beta.146 absent from registry
- Architecture decisions: HIGH — sourced from locked CONTEXT.md decisions
- netstandard2.0 Metrics compatibility: MEDIUM — inferred from type origin, not a live compile test

**Research date:** 2026-05-26
**Valid until:** 2026-06-26 (NuGet package versions; check AngleSharp.Css beta for new pins before Phase 2)
