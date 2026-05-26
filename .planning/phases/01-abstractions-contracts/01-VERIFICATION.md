---
phase: 01-abstractions-contracts
verified: 2026-05-26T00:00:00Z
status: gaps_found
score: 3/5 must-haves verified
gaps:
  - truth: PdfRenderResult has no Content property of any type
    status: failed
    reason: >
      ROADMAP SC4 requires PdfRenderResult to expose Content : Stream.
      Actual record: (PageCount, ByteCount, Elapsed, TemplateHash, PolicyId, Diagnostics) — metadata-only.
      This is a deliberate deviation documented in CONTEXT.md Decision 4: the stream-destination pattern
      on IMPdfService.RenderAsync makes a Content:Stream field on the result redundant and introduces
      stream ownership ambiguity. The ROADMAP SC4 was never updated to reflect this decision.
    artifacts:
      - path: src/Muonroi.Pdf.Abstractions/PdfRenderResult.cs
        issue: No Content property — record has 6 metadata fields only
      - path: .planning/phases/01-abstractions-contracts/01-CONTEXT.md
        issue: Decision 4 documents deliberate divergence but ROADMAP was not updated
    missing:
      - Update ROADMAP.md SC4 to remove Content:Stream requirement (accepted design is stream-destination
        pattern on IMPdfService), or explicitly add Content:Stream to PdfRenderResult if SC4 is required.

  - truth: AngleSharp.Css is pinned at 1.0.0-beta.147, not 1.0.0-beta.146 as required by ROADMAP SC5
    status: failed
    reason: >
      ROADMAP SC5 explicitly requires AngleSharp.Css pinned 1.0.0-beta.146.
      Directory.Packages.props line 13 has Version="1.0.0-beta.147".
      01-01-SUMMARY.md documents the deviation: beta.146 does not exist on NuGet
      (registry jumps beta.144 to beta.147). The selected version is the nearest available release.
      The ROADMAP SC5 was never updated.
    artifacts:
      - path: Directory.Packages.props
        issue: Line 13 — AngleSharp.Css Version="1.0.0-beta.147" (not beta.146)
    missing:
      - Update ROADMAP.md SC5 to replace "pinned 1.0.0-beta.146" with "pinned 1.0.0-beta.147".
---

# Phase 1: Abstractions + Contracts Verification Report

**Phase Goal:** All public API contracts and adapter seams exist in `Muonroi.Pdf.Abstractions`; every downstream implementation package can reference them without circular dependencies.
**Verified:** 2026-05-26T00:00:00Z
**Status:** gaps_found — 2 ROADMAP success criteria have documented deliberate deviations with ROADMAP not updated
**Method:** Every source file read directly; no inference from SUMMARY.md claims.

---

## Step 1: Must-Haves

The five success criteria from ROADMAP.md Phase 1:

| # | Must-Have |
|---|-----------|
| SC1 | `Muonroi.Pdf.Abstractions` compiles targeting `netstandard2.0` with zero implementation code |
| SC2 | All six adapter interfaces defined in the Abstractions assembly |
| SC3 | `PdfConfigs.Limits` exposes all seven hard limits as compile-time constants |
| SC4 | `PdfRenderResult` exposes `Content : Stream` — not `byte[]` |
| SC5 | `Directory.Packages.props` contains AngleSharp, AngleSharp.Css (pinned 1.0.0-beta.146), SixLabors.Fonts, PdfSharpCore; zero inline Version attributes |

---

## SC1 — netstandard2.0 + Zero Implementation

**TFM:** `src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj` line 3: `<TargetFramework>netstandard2.0</TargetFramework>`. PASS.

**Implementation code scan:** All 25 `.cs` files read directly.

| Category | Files | Has PDF logic? |
|----------|-------|----------------|
| Interfaces (7) | IMPdfService, IMPdfRenderer (x2), IHtmlParser, ICssCascadeEngine, IImageDecoder, IPdfWriter, IFontResolver, IResourceResolver, IPdfCssPolicy, IPdfDocumentContext | No — method signatures only |
| Marker interfaces (3) | IParsedDocument, IStyledDocument, IPositionedPageList | No — empty bodies |
| Records (7) | PdfRenderResult, PdfHeaderFooter, FontRequest, ResourceResult, DecodedImage, PolicyValidationResult, PolicyViolation | No — positional records, no domain logic |
| Enums (4) | PdfPageSize, PdfOrientation, FontWeight, FontStyle | No |
| Config/options records (4) | PdfRenderOptions, PdfConfigs, PdfPolicyLimits, PdfMargins | No PDF logic. PdfMargins has static readonly factories and one `Uniform()` factory method. PdfPolicyLimits has two static readonly preset instances. PolicyValidationResult has one static readonly field and one `Fail()` factory. All are value-type convenience members, not PDF rendering logic. |
| Constants (2) | PdfTelemetryNames (static class, const strings only), IsExternalInit (polyfill for netstandard2.0 + C# 9 records) | No |

No `NotImplementedException`, no I/O operations, no algorithmic implementation anywhere. Borderline convenience members on value types (factory methods, static presets) are consistent with "contracts assembly" intent and do not violate the "zero implementation code" requirement.

**SC1: PASS**

---

## SC2 — All Six Adapter Interfaces

| Interface | File | Method Signature | Real (non-empty)? |
|-----------|------|-----------------|-------------------|
| `IHtmlParser` | `Engine/IHtmlParser.cs` | `ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default)` | Yes |
| `ICssCascadeEngine` | `Engine/ICssCascadeEngine.cs` | `ValueTask<IStyledDocument> CascadeAsync(IParsedDocument doc, string? userStyleSheet, CancellationToken ct = default)` | Yes |
| `IImageDecoder` | `Engine/IImageDecoder.cs` | `DecodedImage Decode(ReadOnlySpan<byte> data, string contentType)` | Yes |
| `IPdfWriter` | `Engine/IPdfWriter.cs` | `ValueTask<long> WriteAsync(IPositionedPageList pages, PdfRenderOptions options, Stream destination, CancellationToken ct = default)` | Yes |
| `IFontResolver` | `IFontResolver.cs` | `ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(FontRequest request, CancellationToken cancellationToken = default)` | Yes |
| `IResourceResolver` | `IResourceResolver.cs` | `ValueTask<ResourceResult?> ResolveAsync(Uri uri, string? contentTypeHint = null, CancellationToken cancellationToken = default)` | Yes |

All six are real interfaces with correct non-empty method signatures.

**SC2: PASS**

---

## SC3 — PdfConfigs.Limits with All Seven Constants

File: `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs`

The nested class is named `PdfLimits` (not `Limits`) to avoid a C# CS0102 naming conflict with the property named `Limits`. The property `public PdfLimits Limits { get; set; }` means `PdfConfigs.Limits.MaxHtmlBytes` is still valid at runtime via the property. The SC says "PdfConfigs.Limits exposes all seven hard limits" — the property accessor `Limits` satisfies this.

| Constant | Required Value | Actual Value | Match |
|----------|---------------|--------------|-------|
| `MaxHtmlBytes` | 8_388_608 | `8_388_608` (line 18) | PASS |
| `MaxDomDepth` | 256 | `256` (line 19) | PASS |
| `MaxElementCount` | 100_000 | `100_000` (line 20) | PASS |
| `MaxImagePixels` | 25_000_000 | `25_000_000` (line 21) | PASS |
| `MaxPages` | 1000 | `1_000` (line 22) | PASS |
| `MaxRenderDurationMs` | 15_000 | `15_000` (line 23) | PASS |
| `MaxFontFiles` | 32 | `32` (line 24) | PASS |

All seven constants are `const` fields.

**SC3: PASS**

---

## SC4 — PdfRenderResult.Content : Stream

File: `src/Muonroi.Pdf.Abstractions/PdfRenderResult.cs` (verbatim):
```csharp
public sealed record PdfRenderResult(
    int PageCount,
    long ByteCount,
    TimeSpan Elapsed,
    string TemplateHash,
    string PolicyId,
    IReadOnlyList<PolicyViolation> Diagnostics);
```

There is no `Content` property of any type. The record is metadata-only.

**Documented deviation:** CONTEXT.md Decision 4 records the deliberate choice. `IMPdfService.RenderAsync` already accepts a caller-supplied `Stream destination`; including `Content : Stream` in the result would create stream ownership ambiguity (who disposes). The `Diagnostics` field from ABST-12 was added; `Content : Stream` was intentionally omitted. The ROADMAP SC4 was never updated.

**SC4: FAIL** — deliberate, documented deviation; ROADMAP not updated.

**Action required:** Update ROADMAP.md SC4 to reflect the stream-destination design, or add `Content : Stream` to the record if SC4 is a hard requirement.

---

## SC5 — Directory.Packages.props CPM Compliance

### Package presence

| Package | SC Requirement | Actual (Directory.Packages.props line) | Match |
|---------|---------------|----------------------------------------|-------|
| `AngleSharp` | any version | `1.3.0` (line 10) | PASS |
| `AngleSharp.Css` | `1.0.0-beta.146` | `1.0.0-beta.147` (line 13) | FAIL |
| `SixLabors.Fonts` | any version | `2.1.0` (line 139) | PASS |
| `PdfSharpCore` | any version | `1.3.65` (line 120) | PASS |

**Documented deviation:** `01-01-SUMMARY.md` states: "beta.146 does not exist on NuGet (registry jumps beta.144→beta.147); using beta.147 per research verification." The inline comment on line 11 of `Directory.Packages.props` also states this. ROADMAP SC5 was never updated.

### Inline Version attributes in csproj

Grep of all `*.csproj` files under `src/` for `Version=`: **0 matches**. CPM compliance is complete.

**SC5: FAIL** — AngleSharp.Css version mismatch. Deliberate, documented deviation; ROADMAP not updated.

**Action required:** Update ROADMAP.md SC5 to replace "pinned 1.0.0-beta.146" with "pinned 1.0.0-beta.147".

---

## Requirements Coverage (ABST-01 through ABST-14, PKG-01, PKG-04)

| Req | Status | Notes |
|-----|--------|-------|
| PKG-01 | PASS | netstandard2.0, zero ProjectReference, builds 0 errors |
| PKG-04 | PASS | Enterprise stub: net8.0, IsCommercialPackage=true, empty, builds 0 errors |
| ABST-01 | DEVIATED | Stream overload present with correct semantics. Parameter order is (html, Stream destination, PdfRenderOptions options, ct) rather than (html, options, destination, ct). No generic `RenderAsync<TModel>` on IMPdfService — routed to IMPdfRenderer<T> per Decision 7. |
| ABST-02 | DEVIATED | `RenderAsync(TModel, Stream, PdfRenderOptions?, CancellationToken)` replaces `GetTemplateAsync`. Decision 7: renderer as terminal rendering contract, not HTML string producer. |
| ABST-03 | DEVIATED | `Get<TModel>(string templateId)` + `TryGet<TModel>(...)` replaces `CreateRenderer<T>()`. Decision 8: templateId required to distinguish templates sharing same model type. |
| ABST-04 | DEVIATED | `ValidateAsync(IPdfDocumentContext, CancellationToken) : ValueTask<PolicyValidationResult>` replaces `Validate(IStyleSheet sheet) : PolicyResult`. Async + opaque context prevents AngleSharp type leakage through the policy seam. |
| ABST-05 | DEVIATED | `ResolveAsync(Uri uri, string? contentTypeHint, CancellationToken) : ValueTask<ResourceResult?>` replaces `ResolveAsync(string key, CancellationToken) : Task<ReadOnlyMemory<byte>>`. Uri type enforces scheme validation; ResourceResult carries content type alongside bytes. |
| ABST-06 | DEVIATED | `ResolveAsync(FontRequest request, CancellationToken) : ValueTask<ReadOnlyMemory<byte>?>` replaces `ResolveAsync(string family, FontStyle style, CancellationToken) : Task<ReadOnlyMemory<byte>>`. FontRequest record encapsulates family + weight + style; nullable return avoids separate Exists check. |
| ABST-07 | PASS | `ICssCascadeEngine` in Engine/ with `CascadeAsync` |
| ABST-08 | PASS | `IHtmlParser` in Engine/ with `ParseAsync` |
| ABST-09 | PASS | `IImageDecoder` in Engine/ with synchronous `Decode` (adds `string contentType` param — additive) |
| ABST-10 | PASS | `IPdfWriter` in Engine/ with `WriteAsync` |
| ABST-11 | PASS | `PdfRenderOptions` record with all required fields plus header/footer/encoding extensions |
| ABST-12 | PARTIAL | `Diagnostics : IReadOnlyList<PolicyViolation>` present. `Content : Stream` deliberately absent (Decision 4). |
| ABST-13 | PASS | `SectionName = "PdfConfigs"` (line 9) |
| ABST-14 | PASS | All 7 constants correct |

All ABST-01 through ABST-06 deviations are documented in CONTEXT.md Decisions 7 and 8. These are intentional API improvements, not mistakes. The REQUIREMENTS.md text predates the design decisions.

---

## Anti-Pattern Scan

Grep of all `.cs` files in `src/Muonroi.Pdf.Abstractions/` for TBD, FIXME, XXX, placeholder, TODO, NotImplemented (case-insensitive): **0 matches**.

Notable build warnings (not blockers):
- `CS1574`: XML `<see cref="IPdfTemplate.Id"/>` in `IMPdfRenderer.cs` line 15 — `IPdfTemplate` does not exist in assembly. Documentation-only; no functional impact.
- `CS1574`: XML `<see cref="With"/>` in `PdfPolicyLimits.cs` — references non-existent helper. Documentation-only.
- `CS1591`: 28 missing XML doc comment warnings on Engine/ interfaces and PdfConfigs constants.

---

## Behavioral Verification

Command: `dotnet build src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj --no-incremental`

Result: **Build succeeded. 30 Warning(s). 0 Error(s). Time Elapsed 00:00:00.89**

Note: `01-03-SUMMARY.md` claimed "0 warnings" — the actual build produces 30 XML documentation warnings. These are pre-existing gaps, not introduced by Phase 1 changes. Not blocking.

---

## Human Verification Needs

N/A — infrastructure/contracts phase with no user-facing elements. All checks are programmatically verifiable.

---

## Score and Status

| SC | Result |
|----|--------|
| SC1 — netstandard2.0 + zero implementation | PASS |
| SC2 — All six adapter interfaces | PASS |
| SC3 — PdfConfigs.Limits with 7 constants | PASS |
| SC4 — PdfRenderResult.Content : Stream | FAIL (deliberate deviation, ROADMAP not updated) |
| SC5 — CPM compliance (AngleSharp.Css beta.146) | FAIL (using beta.147, ROADMAP not updated) |

**Score: 3/5 must-haves verified**
**Status: gaps_found**

Both failures are deliberate documented deviations where the implementation is architecturally sound but the ROADMAP success criteria were never updated to reflect the final design decisions. This is a documentation synchronization problem, not an implementation defect.

---

## Recommended Actions

1. **Update ROADMAP.md SC4**: Replace "PdfRenderResult exposes `Content : Stream` — not `byte[]`" with "PdfRenderResult is metadata-only (PageCount, ByteCount, Elapsed, TemplateHash, PolicyId, Diagnostics); PDF bytes are written to a caller-supplied `Stream destination` in `IMPdfService.RenderAsync`".

2. **Update ROADMAP.md SC5**: Replace "AngleSharp.Css (pinned 1.0.0-beta.146)" with "AngleSharp.Css (pinned 1.0.0-beta.147)" — beta.146 does not exist on NuGet.

3. **Update REQUIREMENTS.md ABST-01 through ABST-06 and ABST-12**: Capture the actual implemented signatures. Phase 2+ implementors will use REQUIREMENTS.md as spec — stale signatures will cause confusion. Specifically:
   - ABST-02: Document `RenderAsync` (not `GetTemplateAsync`)
   - ABST-03: Document `Get<TModel>(string templateId)` / `TryGet` (not `CreateRenderer<T>()`)
   - ABST-04: Document `ValidateAsync(IPdfDocumentContext, CancellationToken)` (not `Validate(IStyleSheet)`)
   - ABST-05: Document `ResolveAsync(Uri, string?, CancellationToken) : ValueTask<ResourceResult?>` (not string key)
   - ABST-06: Document `ResolveAsync(FontRequest, CancellationToken) : ValueTask<ReadOnlyMemory<byte>?>` (not family/style params)
   - ABST-12: Remove `Content : Stream` from PdfRenderResult description

4. **Fix dangling cref** in `IMPdfRenderer.cs` line 15: Replace `<see cref="IPdfTemplate.Id"/>` with a plain text reference since `IPdfTemplate` does not exist in the assembly.

---

_Verified: 2026-05-26T00:00:00Z_
_Verifier: gsd-verifier agent (Claude, goal-backward)_
