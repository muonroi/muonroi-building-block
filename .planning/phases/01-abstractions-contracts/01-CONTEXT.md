# Phase 1 Context: Abstractions + Contracts

**Phase**: 1 of 9
**Name**: Abstractions + Contracts
**Date captured**: 2026-05-26
**Mode**: Headless autonomous (no interactive discussion)

---

## Domain

All public API contracts and adapter seams exist in `Muonroi.Pdf.Abstractions`; every downstream implementation package can reference them without circular dependencies.

Requirements are locked by PKG-01, ABST-01–ABST-14 in `.planning/REQUIREMENTS.md`.

---

## Canonical References

- `.planning/REQUIREMENTS.md` — locked requirements for Phase 1 (PKG-01, ABST-01–ABST-14)
- `.planning/PROJECT.md` — Key Decisions table (D1–D20, all Pending as of 2026-05-26)
- `.planning/ROADMAP.md` — Phase 1 success criteria
- `src/Muonroi.Pdf.Abstractions/` — existing partial implementation (current state)
- `src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj` — must fix TFM (see Decision 1)
- `Directory.Packages.props` — CPM: four PDF deps must be added (see Decision 5)

---

## Existing State (verified 2026-05-26)

The following are already implemented in `src/Muonroi.Pdf.Abstractions/`:

| File | Status |
|------|--------|
| `IMPdfService.cs` | Done — `RenderAsync`, `RenderMultiPageAsync`, `RenderToBytesAsync` |
| `IMPdfRenderer.cs` | Done — `IMPdfRenderer<T>` + `IMPdfRendererFactory` |
| `IFontResolver.cs` | Done — `FontRequest`, `FontWeight`, `FontStyle` enums |
| `IResourceResolver.cs` | Done — bytes-only, `ResourceResult` record |
| `PdfRenderOptions.cs` | Done — full record with all per-call fields |
| `PdfRenderResult.cs` | Done (metadata-only — see Decision 4) |
| `PdfHeaderFooter.cs` | Done |
| `PdfMargins.cs` | Done |
| `PdfPageSize.cs` | Done — A4/A5/A3/Letter/Legal |
| `PdfOrientation.cs` | Done |
| `Policy/IPdfCssPolicy.cs` | Done — `IPdfDocumentContext`, `ValidateAsync` |
| `Policy/PdfPolicyLimits.cs` | Done — rich per-policy limits, `Strict`/`Relaxed` presets |
| `Policy/PolicyValidationResult.cs` | Done — `PolicyViolation`, `PolicySeverity` |
| `Engine/` | **Empty** — adapter seams not yet defined (see Decision 3) |
| `Telemetry/` | **Empty** — telemetry descriptor not yet defined |
| `PdfConfigs` | **Missing** — IConfiguration-bound options class (see Decision 2) |

---

## Implementation Decisions

### Decision 1: TFM — Change net8.0 → netstandard2.0, remove Core.Abstractions reference

**Problem**: The csproj currently targets `net8.0` and references `Muonroi.Core.Abstractions` (which also targets `net8.0`). REQUIREMENTS PKG-01 and ROADMAP mandate `netstandard2.0`.

**Finding**: After reading all source files in `Muonroi.Pdf.Abstractions/`, no types from `Muonroi.Core.Abstractions` are actually used at the compile-time level. The reference is a leftover from project scaffolding.

**Decision**: Remove the `<ProjectReference>` to `Muonroi.Core.Abstractions` and change `<TargetFramework>` to `netstandard2.0`. The Abstractions package must be dependency-free to remain maximally portable.

**Why**: `netstandard2.0` is required so the v0.2 source generator project can reference this assembly as an analyzer reference. A `net8.0` target breaks that path. Also, zero external dependencies in a contracts assembly is correct architecture.

**Action**: Edit `Muonroi.Pdf.Abstractions.csproj`.

---

### Decision 2: PdfConfigs class — add as IConfiguration-bound options; keep PdfPolicyLimits separate

**Problem**: REQUIREMENTS ABST-13 and ABST-14 require `PdfConfigs` with `SectionName = "PdfConfigs"` and a nested `Limits` object with 7 specific compile-time constants. Currently only `PdfPolicyLimits` exists, with different default values.

**Finding**: `PdfPolicyLimits` defaults differ from REQUIREMENTS constants:
- `MaxHtmlBytes`: 2 MB (PdfPolicyLimits.Strict) vs 8 MB (REQUIREMENTS)
- `MaxElementCount`: 50,000 vs 100,000 (REQUIREMENTS)
These are different concerns at different layers.

**Decision**: Implement BOTH — they serve different architectural layers:
- `PdfConfigs` = IConfiguration-bound options class, bound from `"PdfConfigs"` section. Contains `Limits` nested object with the 7 REQUIREMENTS constants as default values. This is the DI/startup configuration contract.
- `PdfPolicyLimits` = Per-policy runtime object with richer limits used inside `IPdfCssPolicy`. Can vary per render via `PdfRenderOptions.Policy`.

**Location**: `PdfConfigs.cs` in root of `Muonroi.Pdf.Abstractions` namespace.

**Limits to enforce per REQUIREMENTS ABST-14**:
- `MaxHtmlBytes = 8_388_608` (8 MB)
- `MaxDomDepth = 256`
- `MaxElementCount = 100_000`
- `MaxImagePixels = 25_000_000`
- `MaxPages = 1_000`
- `MaxRenderDurationMs = 15_000`
- `MaxFontFiles = 32`

---

### Decision 3: Engine/ adapter seams — define all four with opaque intermediate types

**Problem**: `Engine/` directory exists but is empty. ABST-07 through ABST-10 require `IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter`.

**Decision**: Define all four interfaces using opaque intermediate types that do NOT leak third-party types (AngleSharp, SixLabors, PdfSharpCore) through the seam:

- `IHtmlParser` — `ParseAsync(string html, CancellationToken ct) : ValueTask<IParsedDocument>` (string, not ReadOnlyMemory<byte> — matches `IMPdfService.RenderAsync(string html, ...)` input)
- `ICssCascadeEngine` — `CascadeAsync(IParsedDocument doc, string? userStyleSheet, CancellationToken ct) : ValueTask<IStyledDocument>`
- `IImageDecoder` — `Decode(ReadOnlySpan<byte> data, string contentType) : DecodedImage` (sync — no I/O inside decoder)
- `IPdfWriter` — `WriteAsync(IPositionedPageList pages, PdfRenderOptions options, Stream destination, CancellationToken ct) : ValueTask<long>` (returns bytes written)

**Opaque marker interfaces needed** (also in Engine/):
- `IParsedDocument` — marker interface; engine impl holds AngleSharp DOM internally
- `IStyledDocument` — marker interface; engine impl holds computed styles internally
- `IPositionedPageList` — marker interface; layout engine output
- `DecodedImage` — sealed record with `int Width`, `int Height`, `ReadOnlyMemory<byte> Data`, `string ContentType`

These are internal engine contracts — not called by consumers of `Muonroi.Pdf.Abstractions`. They exist in Abstractions so Phase 2–5 implementation packages can reference them without circular deps.

---

### Decision 4: PdfRenderResult shape — keep metadata-only; deviation from REQUIREMENTS ABST-12

**Problem**: REQUIREMENTS ABST-12 specifies `PdfRenderResult` with `Content : Stream` and a `Diagnostics` collection. The current implementation is metadata-only: `(PageCount, ByteCount, Elapsed, TemplateHash, PolicyId)`.

**Finding**: The stream-based overloads of `IMPdfService` already write to a caller-supplied `destination` stream. Including `Content : Stream` in the result record would be redundant and introduce a resource-management problem (who disposes the stream?).

**Decision**: Keep the metadata-only design. The current `PdfRenderResult` correctly separates content (written to destination by the engine) from metadata (returned to the caller). REQUIREMENTS ABST-12's `Content : Stream` was a spec draft artifact that was superseded by the cleaner destination-stream pattern.

**Add `Diagnostics` collection**: `IReadOnlyList<PolicyViolation> Diagnostics` should be added to `PdfRenderResult` to carry non-fatal policy warnings from the render. This is the valid part of ABST-12 that the current record is missing.

**Updated record**: `PdfRenderResult(int PageCount, long ByteCount, TimeSpan Elapsed, string TemplateHash, string PolicyId, IReadOnlyList<PolicyViolation> Diagnostics)`

---

### Decision 5: Directory.Packages.props — add all four PDF dep versions in Phase 1

**Problem**: None of the four required PDF packages (AngleSharp, AngleSharp.Css, SixLabors.Fonts, PdfSharpCore) appear in `Directory.Packages.props`. CPM compliance requires all versions declared there.

**Decision**: Add all four version entries in Phase 1, even though implementation packages referencing them come in Phase 2+. Version declarations in `Directory.Packages.props` without any `<PackageReference>` using them are harmless.

**Versions to declare**:
```xml
<PackageVersion Include="AngleSharp" Version="1.3.0" />
<PackageVersion Include="AngleSharp.Css" Version="1.0.0-beta.146" />
<PackageVersion Include="SixLabors.Fonts" Version="2.1.0" />
<PackageVersion Include="PdfSharpCore" Version="1.3.65" />
```

Note: AngleSharp.Css MUST be pinned at exactly `1.0.0-beta.146` — it is the only viable managed CSS cascade engine and the beta is accepted as a known constraint (D4 in PROJECT.md).

---

### Decision 6: Telemetry/ — add PdfTelemetryDescriptor

**Problem**: `Telemetry/` directory exists but is empty. REQUIREMENTS TEL-01–TEL-05 (Phase 6) require a `PdfTelemetryDescriptor : ITelemetryDescriptor`.

**Decision**: Define the contract in Phase 1, implementation in Phase 6. Add to `Telemetry/`:
- `PdfTelemetryNames.cs` — static class with string constants for activity/metric names (avoids magic strings in later phases):
  - `ActivitySourceName = "Muonroi.BuildingBlock.Pdf"`
  - Metric names: `pdf.operation`, `pdf.template_id`, `pdf.page_count`, attribute: `tenant.id`

This is a constants-only file, zero implementation — appropriate for the Abstractions package.

---

## What Must Be Implemented in Phase 1

Priority order (build order dependency):

1. Fix `Muonroi.Pdf.Abstractions.csproj`: remove Core.Abstractions reference, change TFM to netstandard2.0
2. Add `PdfConfigs.cs` with `PdfConfigs.Limits` nested class (7 constants)
3. Add `Engine/IParsedDocument.cs`, `Engine/IStyledDocument.cs`, `Engine/IPositionedPageList.cs`, `Engine/DecodedImage.cs`
4. Add `Engine/IHtmlParser.cs`, `Engine/ICssCascadeEngine.cs`, `Engine/IImageDecoder.cs`, `Engine/IPdfWriter.cs`
5. Update `PdfRenderResult.cs` — add `Diagnostics` field
6. Add `Telemetry/PdfTelemetryNames.cs`
7. Update `Directory.Packages.props` — add four package version entries
8. Verify `dotnet build` succeeds on `netstandard2.0` target

---

## Out of Phase 1 Scope

- Any implementation code (Phase 2+ only)
- `Muonroi.Pdf.Governance` CSS policy enforcement (Phase 2)
- DI registration `AddPdf()` (Phase 6)
- `Muonroi.Pdf.Enterprise` stub (Phase 1 REQUIREMENTS PKG-04 — actually IS in scope, noted below)

**Note**: PKG-04 (Enterprise stub) is Phase 1 per REQUIREMENTS but the scope note says "empty stub project". This is a csproj creation task only — no code. The planner should include it.

---

### Decision 7: IMPdfRenderer<T> — keep RenderAsync, diverge from ABST-02

**REQUIREMENTS ABST-02 says**: `GetTemplateAsync(T model, CancellationToken ct) : Task<string>` (returns HTML string).
**Current impl**: `RenderAsync(TModel model, Stream destination, PdfRenderOptions?, CancellationToken)` (renders PDF directly).

**Decision**: Keep `RenderAsync` — intentional divergence from ABST-02. `GetTemplateAsync` was a draft design where the renderer was a template engine only; the current interface makes `IMPdfRenderer<T>` the terminal rendering contract, which is the correct design for the v0.2 source generator fast path (the SG generates `RenderAsync`, not an HTML string producer).

---

### Decision 8: IMPdfRendererFactory — keep Get/TryGet(templateId), diverge from ABST-03

**REQUIREMENTS ABST-03 says**: `CreateRenderer<T>() : IMPdfRenderer<T>` (no templateId).
**Current impl**: `Get<TModel>(string templateId)` + `TryGet<TModel>(string templateId, out IMPdfRenderer<TModel>? renderer)`.

**Decision**: Keep `Get/TryGet(templateId)` — intentional divergence from ABST-03. A factory keyed only by generic type parameter cannot support multiple templates sharing the same model type (common case: `InvoiceModel` used for proforma + final invoice templates). The `templateId` parameter is required for correct operation.

---

## Deferred / Not Discussed

- `IMPdfService.RenderAsync<TModel>` overload (mentioned in PROJECT.md) — covered by `IMPdfRenderer<T>` + `IMPdfRendererFactory`; the generic overload on IMPdfService would be redundant. Keep current design (no change).
- `IPdfTemplate` interface (referenced in IMPdfRenderer<T> comments as `IPdfTemplate.Id`) — not required by REQUIREMENTS; `string TemplateId` property is sufficient. No action.
