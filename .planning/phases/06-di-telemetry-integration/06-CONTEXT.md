# Phase 6 Context: DI + Telemetry + Integration

**Phase**: 6 of 9
**Name**: DI + Telemetry + Integration
**Date captured**: 2026-05-27
**Mode**: Headless autonomous (no interactive discussion)

---

## Domain

Phase 6 wires the full PDF rendering pipeline through a production-grade DI registration and activates OpenTelemetry instrumentation. It delivers `MPdfService` (the concrete `IMPdfService` implementation), the `AddPdf()` extension method in `Muonroi.Pdf.Extensions`, `PdfTelemetryDescriptor` in Abstractions, and render-timeout enforcement (PIPE-08). No new public contracts are introduced — all interfaces were defined in Phase 1; this phase provides their wiring and default implementations.

---

## Canonical References

- `.planning/REQUIREMENTS.md` — PKG-02, DI-01–DI-04, PIPE-08, TEL-01–TEL-05
- `.planning/ROADMAP.md` — Phase 6 success criteria (SC1–SC5)
- `.planning/PROJECT.md` — Key Decisions table; D16 (multi-tenant cache keys from ambient ITenantContext)
- `.planning/phases/05-pdf-writer-determinism-security/05-CONTEXT.md` — IPdfWriter seam; PdfSharpCoreWriter default implementation (Phase 5 output)
- `src/Muonroi.Pdf.Abstractions/IMPdfService.cs` — primary service contract; three overloads; singleton-safe
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — `SectionName = "PdfConfigs"`; PdfLimits with 7 constants
- `src/Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryNames.cs` — ActivitySourceName, OperationMetric, PageCountMetric, tag keys
- `src/Muonroi.Core.Abstractions/Interfaces/ITelemetryDescriptor.cs` — `ActivitySourceNames` + `MeterNames` properties; parameterless ctor required
- `src/Muonroi.RuleEngine.Abstractions/Telemetry/RuleEngineTelemetryDescriptor.cs` — canonical ITelemetryDescriptor implementation pattern
- `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` — `LayoutAsync()` entry point; accepts ITextMetrics
- `src/Muonroi.Pdf/Internal/Font/FontPipeline.cs` — async font pre-pass
- `src/Muonroi.Pdf/Internal/Image/ImagePipeline.cs` — async image pre-pass
- `src/Muonroi.Pdf.Abstractions/Engine/IPdfWriter.cs` — `WriteAsync(IPositionedPageList, PdfRenderOptions, Stream, CancellationToken)`
- `src/Muonroi.Observability/OpenTelemetry/MuonroiMetrics.cs` — static Meter + static Counter/Histogram pattern

---

## Decisions

### 1. MPdfService — constructor injection vs. manual wiring

**Decision**: `MPdfService` uses constructor injection for the five registered adapter interfaces (`IHtmlParser`, `ICssCascadeEngine`, `IPdfWriter`, `IFontResolver?`, `IResourceResolver`) and a singleton `IOptions<PdfConfigs>`. Internal pipeline stages (`LayoutEngine`, `FontPipeline`, `ImagePipeline`) are instantiated directly inside `MPdfService`'s constructor — they are internal implementation details, not DI-registered services.

`IFontResolver` is optional at the DI level (no default registration); if not registered, `IFontResolver?` resolves to `null` and font resolution returns `null` (no embedded fonts). `IResourceResolver` defaults to `ThrowingResourceResolver` (D14).

**File**: `src/Muonroi.Pdf/Internal/Service/MPdfService.cs`

### 2. Render pipeline orchestration order

**Decision**: `MPdfService.RenderAsync` chains stages in this order:
1. Validate HTML length against `MaxHtmlBytes` (early exit)
2. Create linked `CancellationTokenSource` with `MaxRenderDurationMs` timeout (PIPE-08)
3. Start OTel activity span
4. `IHtmlParser.ParseAsync` → `IParsedDocument`
5. `ICssCascadeEngine.CascadeAsync` → `IStyledDocument`
6. `IPdfCssPolicy.ValidateAsync` → diagnostics; throw `PdfPolicyException` on violation
7. `LayoutEngine.LayoutAsync` → `IPositionedPageList`
8. `FontPipeline.RunAsync` → mutates `PositionedPageList.EmbeddedFonts`
9. `ImagePipeline.RunAsync` → mutates `PositionedPageList.Images`
10. `IPdfWriter.WriteAsync` → writes to destination `Stream`
11. Record OTel metrics; return `PdfRenderResult`

The linked CTS token is threaded through every `async` call. `OperationCanceledException` from timeout propagates unmodified (SC4).

### 3. Timeout enforcement (PIPE-08)

**Decision**: At the start of `RenderAsync`, create:
```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(TimeSpan.FromMilliseconds(_configs.Limits.MaxRenderDurationMs));
```
Pass `cts.Token` to all downstream async calls. This is the canonical .NET pattern; no `Task.WhenAny` needed.

### 4. PdfTelemetryDescriptor — location and structure

**Decision**: `PdfTelemetryDescriptor` lives in `Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryDescriptor.cs` (not in `Muonroi.Pdf`), matching the `DistributedCacheTelemetryDescriptor` pattern where the descriptor travels with the abstractions package. 

```csharp
public sealed class PdfTelemetryDescriptor : ITelemetryDescriptor
{
    public IEnumerable<string> ActivitySourceNames => [PdfTelemetryNames.ActivitySourceName];
    public IEnumerable<string> MeterNames         => [PdfTelemetryNames.ActivitySourceName];
}
```

Static `ActivitySource` and static `Meter` instances live in `MPdfService` (not the descriptor). The descriptor is a discovery/registration token only — no state.

### 5. Static ActivitySource and Meter placement

**Decision**: Static telemetry instances live in a dedicated internal class `PdfMetrics` inside `Muonroi.Pdf/Internal/Telemetry/PdfMetrics.cs`:
```csharp
internal static class PdfMetrics
{
    internal static readonly ActivitySource Source = new(PdfTelemetryNames.ActivitySourceName);
    private static readonly Meter _meter = new(PdfTelemetryNames.ActivitySourceName);
    internal static readonly Counter<long> OperationCounter =
        _meter.CreateCounter<long>(PdfTelemetryNames.OperationMetric, unit: "{render}");
    internal static readonly Histogram<int> PageCountHistogram =
        _meter.CreateHistogram<int>(PdfTelemetryNames.PageCountMetric, unit: "{page}");
}
```
This mirrors the `MuonroiMetrics.cs` pattern without polluting `MPdfService` with static state.

### 6. Tenant ID resolution for telemetry

**Decision**: `MPdfService` constructor accepts `IServiceProvider` and resolves `ITenantContext` lazily per-call via `_serviceProvider.GetService<ITenantContext>()`. If `ITenantContext` is not registered (non-HTTP, unit-test contexts), `tenant.id` tag defaults to `"unknown"`.

This avoids injecting a scoped service into a singleton (scope violation). `IServiceProvider` itself is singleton-safe.

### 7. Default DI registrations in AddPdf()

**Decision**: `AddPdf(IServiceCollection, IConfiguration)` registers with `TryAddSingleton`:
| Service | Default Implementation |
|---|---|
| `IHtmlParser` | `AngleSharpHtmlParser` (from Governance) |
| `ICssCascadeEngine` | `AngleSharpCascadeEngine` (from Governance) |
| `IImageDecoder` | `PureImageDecoder` |
| `IPdfWriter` | `PdfSharpCoreWriter` (Phase 5) |
| `IResourceResolver` | `ThrowingResourceResolver` |
| `IMPdfService` | `MPdfService` |

`IFontResolver` has **no default registration** — callers must provide it. If absent, `MPdfService` uses the null-fallback path (no embedded fonts, system font metrics only). `IPdfCssPolicy` defaults to `DefaultStrictPolicy` via `TryAddSingleton`.

### 8. PdfConfigs validation at startup (DI-03)

**Decision**: Use Options validation with `ValidateOnStart()`:
```csharp
services
    .AddOptions<PdfConfigs>()
    .BindConfiguration(PdfConfigs.SectionName)
    .Validate(cfg =>
        cfg.Limits.MaxPages > 0 &&
        cfg.Limits.MaxHtmlBytes > 0 &&
        cfg.Limits.MaxDomDepth > 0 &&
        cfg.Limits.MaxElementCount > 0 &&
        cfg.Limits.MaxImagePixels > 0 &&
        cfg.Limits.MaxRenderDurationMs > 0 &&
        cfg.Limits.MaxFontFiles > 0,
        "PdfConfigs: all limits must be positive integers")
    .ValidateOnStart();
```
`ValidateOnStart()` causes the host to throw during startup (before any render) if validation fails — satisfies SC5.

### 9. RenderMultiPageAsync and RenderToBytesAsync

**Decision**: `RenderMultiPageAsync` renders each page's HTML fragment through `RenderAsync` into individual temp streams, then concatenates via `PdfSharpCore`'s `PdfDocument.Import` pattern. `RenderToBytesAsync` wraps `RenderAsync` with a `MemoryStream` — no extra logic.

If Phase 5's `PdfSharpCoreWriter` supports multi-document merge natively, use that. Otherwise, render each fragment to a temp `MemoryStream` and merge. This is a Phase 5/6 boundary detail — Phase 6 plans should verify the merge approach.

### 10. Extension method file placement (PKG-02)

**Decision**: DI extension lives in `src/Muonroi.Pdf/Extensions/PdfServiceCollectionExtensions.cs` in namespace `Muonroi.Pdf.Extensions` — matching the requirement exactly. The `Muonroi.Pdf` csproj already exists; this adds a new file.

---

## Deferred / Out of Scope for Phase 6

- `IMPdfRendererFactory` and `IMPdfRenderer<TModel>` — no default factory registration in Phase 6; deferred to Phase 8 source generator
- `Muonroi.BuildingBlock.All` meta-package update (PKG-05) — Phase 7
- `OSS-BOUNDARY.md` update (PKG-06) — Phase 7
- Scoped per-tenant `IPdfCssPolicy` override — Phase 9 Enterprise
- `IFontResolver` default (system font fallback) — post-v0.1 nice-to-have

---

## Success Criteria Traceability

| SC | Requirement | Satisfied by Decision |
|----|-------------|----------------------|
| SC1 | DI-01, DI-02 | Dec 7 (TryAddSingleton, AddPdf) |
| SC2 | PIPE-07, PIPE-08 | Dec 2 (pipeline chain), Dec 3 (timeout) |
| SC3 | TEL-02, TEL-03, TEL-04 | Dec 5 (PdfMetrics static class) |
| SC4 | PIPE-08 | Dec 3 (linked CTS + CancelAfter) |
| SC5 | DI-03 | Dec 8 (ValidateOnStart) |

---

*Context captured: 2026-05-27 — headless autonomous mode*
