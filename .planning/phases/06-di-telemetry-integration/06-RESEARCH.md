# Phase 6: DI + Telemetry + Integration — Research

**Researched:** 2026-05-27
**Domain:** .NET DI registration, OpenTelemetry instrumentation, MPdfService integration
**Confidence:** HIGH — all findings verified from actual project source files

---

## Summary

Phase 6 wires the completed pipeline stages (Phases 1–5) into a production-ready `MPdfService` singleton, registers everything via `AddPdf()`, and activates OpenTelemetry spans and metrics. All design decisions are locked in CONTEXT.md — this document establishes what exists, what is absent, and the exact code patterns to follow.

The most important finding is that **Phase 5 is partially complete**: `ThrowingResourceResolver` and `PdfSecurityException` exist, but `PdfSharpCoreWriter` (the `IPdfWriter` implementation) does **not yet exist** in the codebase. Phase 6 must not register `PdfSharpCoreWriter` until Phase 5 delivers it; the plan must note this cross-phase dependency. The `PdfTelemetryDescriptor` also does **not yet exist** — Phase 6 must create it. The `Extensions/` directory under `Muonroi.Pdf` exists but is empty.

The telemetry pattern is fully established in the codebase. `DistributedCacheRuntimeTelemetry` (Muonroi.Caching.Abstractions, 68 lines) is the closest structural match to `PdfMetrics`: it holds a static `ActivitySource`, a static `Meter`, and static counters/histograms in a single public static class. The repo uses `System.Diagnostics.ActivitySource` from the .NET BCL — not a separate OTel package — for activity tracing. The OTel packages (version 1.9.0) are in CPM but used only in the host/observability layer, not in library projects.

**Primary recommendation:** Implement in wave order — Wave 1: `PdfTelemetryDescriptor` + `PdfMetrics` + `PdfServiceCollectionExtensions` skeleton → Wave 2: `MPdfService` + `RenderAsync` chain → Wave 3: `RenderMultiPageAsync` + `RenderToBytesAsync` → Wave 4: integration tests. Block Wave 2 on Phase 5 completing `PdfSharpCoreWriter`.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| DI registration (`AddPdf`) | `Muonroi.Pdf` (Extensions) | — | PKG-02 requires `Muonroi.Pdf.Extensions` namespace |
| Service implementation (`MPdfService`) | `Muonroi.Pdf` (Internal/Service) | — | Concrete class; not exported in public API |
| Telemetry discovery token | `Muonroi.Pdf.Abstractions` (Telemetry) | — | Descriptor travels with Abstractions package (Decision 4 pattern) |
| Static ActivitySource + Meter | `Muonroi.Pdf` (Internal/Telemetry) | — | Implementation detail; mirrors `DistributedCacheRuntimeTelemetry` pattern |
| Tenant ID resolution | `MPdfService` (IServiceProvider.GetService) | — | Avoids scoped-in-singleton; safe per Decision 6 |
| Options validation | `AddPdf` via `ValidateOnStart()` | — | SC5: host throws at startup on invalid config |
| Multi-page merge | `MPdfService.RenderMultiPageAsync` | `PdfSharpCoreWriter` (Phase 5) | Per-fragment render + merge; see PdfSharpCore section |

---

## User Constraints (from CONTEXT.md)

### Locked Decisions

| # | Decision |
|---|----------|
| 1 | `MPdfService` uses constructor injection for adapter interfaces; internal stages (`LayoutEngine`, `FontPipeline`, `ImagePipeline`) instantiated directly in constructor |
| 2 | Pipeline order: validate HTML length → linked CTS → OTel activity → parse → cascade → policy → layout → font → image → write → metrics → return |
| 3 | Timeout: `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` + `CancelAfter(MaxRenderDurationMs)` |
| 4 | `PdfTelemetryDescriptor` in `Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryDescriptor.cs`; no static state in descriptor |
| 5 | Static `ActivitySource` + `Meter` in `Muonroi.Pdf/Internal/Telemetry/PdfMetrics.cs` internal static class |
| 6 | `IServiceProvider` injected into `MPdfService`; `ITenantContext` resolved per-call via `GetService<ITenantContext>()`; defaults to `"unknown"` if absent |
| 7 | `AddPdf()` registers with `TryAddSingleton`: `IHtmlParser`→`AngleSharpHtmlParser`, `ICssCascadeEngine`→`AngleSharpCascadeEngine`, `IImageDecoder`→`PureImageDecoder`, `IPdfWriter`→`PdfSharpCoreWriter`, `IResourceResolver`→`ThrowingResourceResolver`, `IMPdfService`→`MPdfService`; `IPdfCssPolicy`→`DefaultStrictPolicy`; no default for `IFontResolver` |
| 8 | `ValidateOnStart()` for `PdfConfigs` Options validation covering all 7 limits > 0 |
| 9 | `RenderMultiPageAsync`: render each fragment to temp `MemoryStream` then merge via `PdfSharpCore`; `RenderToBytesAsync`: wrap `RenderAsync` with `MemoryStream` |
| 10 | DI extension: `src/Muonroi.Pdf/Extensions/PdfServiceCollectionExtensions.cs`, namespace `Muonroi.Pdf.Extensions` |

### Deferred / Out of Scope

- `IMPdfRendererFactory` and `IMPdfRenderer<TModel>` — Phase 8 source generator
- `Muonroi.BuildingBlock.All` meta-package update (PKG-05) — Phase 7
- `OSS-BOUNDARY.md` update (PKG-06) — Phase 7
- Scoped per-tenant `IPdfCssPolicy` override — Phase 9 Enterprise
- `IFontResolver` default (system font fallback) — post-v0.1

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DI-01 | `AddPdf(IServiceCollection, IConfiguration)` registers all default services | Pattern: `TryAddSingleton` + `BindConfiguration` — verified in `RedisExtensions.cs` and `ServiceCollectionExtensions.cs` |
| DI-02 | `IFontResolver` has no default registration; optional null-fallback path | CONTEXT.md Decision 7 + FontPipeline signature verified |
| DI-03 | `PdfConfigs` validated at startup via `ValidateOnStart()` | Pattern: `AddOptions<T>().BindConfiguration().Validate().ValidateOnStart()` — verified in `ServiceCollectionExtensions.cs` (BindConfiguration usage) |
| DI-04 | `PdfTelemetryDescriptor` implements `ITelemetryDescriptor` | `ITelemetryDescriptor` interface verified (2 properties); `DistributedCacheTelemetryDescriptor` is exact pattern |
| PIPE-07 | `IPdfWriter` seam consumed by `MPdfService` | `IPdfWriter.WriteAsync` signature verified (12 lines) |
| PIPE-08 | Render timeout via linked CTS + `CancelAfter` | .NET BCL pattern confirmed from CONTEXT.md Decision 3 |
| TEL-01 | `PdfMetrics.Source` is `ActivitySource` named `"Muonroi.BuildingBlock.Pdf"` | `PdfTelemetryNames.ActivitySourceName` verified |
| TEL-02 | `PdfMetrics.OperationCounter` is `Counter<long>` named `"pdf.operation"` | `PdfTelemetryNames.OperationMetric` verified |
| TEL-03 | `PdfMetrics.PageCountHistogram` is `Histogram<int>` named `"pdf.page_count"` | `PdfTelemetryNames.PageCountMetric` verified |
| TEL-04 | Activity tags: `pdf.template_id`, `tenant.id` | `PdfTelemetryNames.TemplateIdTag` + `TenantIdTag` verified |
| TEL-05 | `PdfTelemetryDescriptor` exposes activity source name and meter name | Pattern from `DistributedCacheTelemetryDescriptor.cs` (15 lines) |
| PKG-02 | Extension method in `Muonroi.Pdf.Extensions` namespace | Decision 10; `Extensions/` directory exists but is empty |

---

## What Already Exists (Phase 1–5 Output)

### Muonroi.Pdf.Abstractions (netstandard2.0)

| File | Lines | Provides to Phase 6 |
|------|-------|---------------------|
| `IMPdfService.cs` | 49 | Primary service contract with 3 overloads |
| `PdfConfigs.cs` | 28 | `SectionName = "PdfConfigs"`, `PdfLimits` with 7 compile-time constants |
| `PdfRenderResult.cs` | 16 | Return type for all overloads |
| `PdfRenderOptions.cs` | — | Per-call options passed to all pipeline stages |
| `Telemetry/PdfTelemetryNames.cs` | 18 | `ActivitySourceName`, `OperationMetric`, `PageCountMetric`, `TemplateIdTag`, `TenantIdTag` |
| `Engine/IPdfWriter.cs` | 12 | `WriteAsync(IPositionedPageList, PdfRenderOptions, Stream, CancellationToken)` |
| `Engine/IHtmlParser.cs` | — | `ParseAsync` seam |
| `Engine/ICssCascadeEngine.cs` | — | `CascadeAsync` seam |
| `Engine/IImageDecoder.cs` | — | `Decode` seam |
| `Engine/IPositionedPageList.cs` | 4 | Opaque handle returned by LayoutEngine |
| `IFontResolver.cs` | — | Optional adapter; no default registration |
| `IResourceResolver.cs` | — | Resource fetch seam |
| `Policy/IPdfCssPolicy.cs` | — | `ValidateAsync` seam |
| `Exceptions/PdfSecurityException.cs` | — | Phase 5 output; thrown by `ThrowingResourceResolver` |

**NOT YET CREATED (Phase 6 must create):**
- `Telemetry/PdfTelemetryDescriptor.cs` — does not exist; only `PdfTelemetryNames.cs` is present

### Muonroi.Pdf (net8.0) — Internal implementations

| File | Lines | Provides to Phase 6 |
|------|-------|---------------------|
| `Internal/Layout/LayoutEngine.cs` | 178 | `Layout(IStyledDocument, PdfRenderOptions, ITextMetrics)` — instantiated directly in MPdfService |
| `Internal/Layout/PositionedPageList.cs` | 11 | Carries `EmbeddedFonts` + `Images` for downstream stages |
| `Internal/Font/FontPipeline.cs` | 48 | `ResolveAsync(IStyledDocument, IFontResolver, PdfLimits, ct)` — instantiated directly |
| `Internal/Image/ImagePipeline.cs` | 82 | `ResolveAsync(IStyledDocument, IResourceResolver, IImageDecoder, PdfLimits, ct)` — instantiated directly |
| `Internal/Image/PureImageDecoder.cs` | 95 | Default `IImageDecoder` — registered via DI |
| `Internal/Security/ThrowingResourceResolver.cs` | 28 | Default `IResourceResolver` — registered via DI |

**NOT YET CREATED (Phase 5 remaining + Phase 6 must create):**
- `Internal/Writer/PdfSharpCoreWriter.cs` — **Phase 5 output; does not exist yet** — blocks `IPdfWriter` default DI registration
- `Internal/Writer/PdfSharpFontResolverAdapter.cs` — **Phase 5 output; does not exist yet**
- `Internal/Telemetry/PdfMetrics.cs` — Phase 6 creates
- `Internal/Service/MPdfService.cs` — Phase 6 creates
- `Extensions/PdfServiceCollectionExtensions.cs` — Phase 6 creates (directory exists, empty)

### Muonroi.Pdf.Governance (net8.0)

| File | Lines | Provides to Phase 6 |
|------|-------|---------------------|
| `Parsing/AngleSharpHtmlParser.cs` | 56 | Default `IHtmlParser` implementation — registered via DI |
| `Cascade/AngleSharpCascadeEngine.cs` | 24 | Default `ICssCascadeEngine` implementation — registered via DI |
| `Policies/DefaultStrictPolicy.cs` | 139 | Default `IPdfCssPolicy` — registered via DI |

---

## What Phase 6 Must Create

| File | Location | Purpose |
|------|----------|---------|
| `PdfTelemetryDescriptor.cs` | `src/Muonroi.Pdf.Abstractions/Telemetry/` | `ITelemetryDescriptor` impl; discovery token for OTel registration |
| `PdfMetrics.cs` | `src/Muonroi.Pdf/Internal/Telemetry/` | Static `ActivitySource` + `Meter` + `Counter` + `Histogram` |
| `MPdfService.cs` | `src/Muonroi.Pdf/Internal/Service/` | `IMPdfService` implementation; full pipeline orchestration |
| `PdfServiceCollectionExtensions.cs` | `src/Muonroi.Pdf/Extensions/` | `AddPdf()` extension method with all DI registrations |

**Directories to create:**
- `src/Muonroi.Pdf/Internal/Telemetry/` — new directory
- `src/Muonroi.Pdf/Internal/Service/` — new directory

---

## Standard Stack

All versions verified from `Directory.Packages.props` (CPM — no version attributes needed in .csproj).

| Package | Version | Used By | Purpose |
|---------|---------|---------|---------|
| `PdfSharpCore` | 1.3.65 | `Muonroi.Pdf` | PDF generation (Phase 5 writer) |
| `SixLabors.Fonts` | 2.1.0 | `Muonroi.Pdf` | Font metrics + subsetting (already referenced in csproj) |
| `AngleSharp` | 1.3.0 | `Muonroi.Pdf.Governance` | HTML parser |
| `AngleSharp.Css` | 1.0.0-beta.147 | `Muonroi.Pdf.Governance` | CSS cascade |
| `Microsoft.Extensions.DependencyInjection` | `$(MicrosoftExtensionsVersion)` | `Muonroi.Pdf` (new) | `IServiceCollection`, `TryAddSingleton` |
| `Microsoft.Extensions.Options` | `$(MicrosoftExtensionsVersion)` | `Muonroi.Pdf` (new) | `IOptions<T>`, `AddOptions`, `ValidateOnStart` |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `$(MicrosoftExtensionsVersion)` | `Muonroi.Pdf` (new) | `BindConfiguration()` |
| `OpenTelemetry` | 1.9.0 | observability layer only | OTel SDK — NOT needed in Muonroi.Pdf; `ActivitySource` is BCL |
| `xunit` | 2.9.2 | test projects | Test framework (inherited via Directory.Build.props) |
| `FluentAssertions` | 7.2.0 | test projects | Assertions (inherited) |
| `NSubstitute` | 5.3.0 | test projects | Mocking (inherited) |

**New package references required in `Muonroi.Pdf.csproj`:**
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
<PackageReference Include="Microsoft.Extensions.Options" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
```
The `PdfSharpCore` reference must also be added as part of Phase 5 completion (not yet in the csproj).

**OTel clarification:** `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` are .NET BCL types — no OTel NuGet package needed in `Muonroi.Pdf`. The OTel packages (`OpenTelemetry` 1.9.0) are host-layer concerns registered by consuming applications, not by the library.

---

## Architecture Patterns

### Pipeline Orchestration Pattern

From CONTEXT.md Decision 2:

```csharp
// src/Muonroi.Pdf/Internal/Service/MPdfService.cs
public async Task<PdfRenderResult> RenderAsync(
    string html,
    Stream destination,
    PdfRenderOptions options,
    CancellationToken cancellationToken = default)
{
    // Step 1: Validate HTML length
    if (Encoding.UTF8.GetByteCount(html) > PdfConfigs.PdfLimits.MaxHtmlBytes)
        throw new PdfInputLimitException("HTML-MAX-BYTES", ...);

    // Step 2: Linked CTS with timeout
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(TimeSpan.FromMilliseconds(_configs.Limits.MaxRenderDurationMs));

    // Step 3: OTel activity
    using Activity? activity = PdfMetrics.Source.StartActivity("pdf.render", ActivityKind.Internal);
    activity?.SetTag(PdfTelemetryNames.TemplateIdTag, options.TemplateId ?? "");
    string tenantId = _serviceProvider.GetService<ITenantContext>()?.TenantId ?? "unknown";
    activity?.SetTag(PdfTelemetryNames.TenantIdTag, tenantId);

    var sw = Stopwatch.StartNew();
    try
    {
        // Steps 4–10: pipeline chain
        IParsedDocument parsed  = await _htmlParser.ParseAsync(html, cts.Token);
        IStyledDocument styled  = await _cascadeEngine.CascadeAsync(parsed, cts.Token);
        PolicyValidationResult policy = await _cssPolicy.ValidateAsync(styled, cts.Token);
        if (!policy.IsValid) throw new PdfPolicyException(policy.Violations);

        IPositionedPageList pages = _layoutEngine.Layout(styled, options);
        await _fontPipeline.RunAsync((PositionedPageList)pages, _fontResolver, _configs.Limits, cts.Token);
        await _imagePipeline.RunAsync((PositionedPageList)pages, _resourceResolver, _imageDecoder, _configs.Limits, cts.Token);

        long byteCount = await _writer.WriteAsync(pages, options, destination, cts.Token);

        // Step 11: metrics + return
        sw.Stop();
        PdfMetrics.OperationCounter.Add(1, new TagList {
            { PdfTelemetryNames.TenantIdTag, tenantId },
            { "pdf.status", "ok" }
        });
        PdfMetrics.PageCountHistogram.Record(pages.PageCount, new TagList {
            { PdfTelemetryNames.TenantIdTag, tenantId }
        });

        return new PdfRenderResult(pages.PageCount, byteCount, sw.Elapsed, templateHash, policyId, []);
    }
    catch (OperationCanceledException)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "timeout_or_cancelled");
        throw; // propagate unmodified per SC4
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        PdfMetrics.OperationCounter.Add(1, new TagList {
            { PdfTelemetryNames.TenantIdTag, tenantId },
            { "pdf.status", "error" }
        });
        throw;
    }
}
```

### Telemetry Pattern — DistributedCacheRuntimeTelemetry (exact codebase model)

File: `src/Muonroi.Caching.Abstractions/Distributed/DistributedCacheRuntimeTelemetry.cs` (68 lines)

```csharp
// Verified pattern — PdfMetrics must mirror this exactly
public static class DistributedCacheRuntimeTelemetry
{
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _operationCounter =
        _meter.CreateCounter<long>("distributed_cache_operations_total", ...);
    private static readonly Histogram<double> _durationHistogram =
        _meter.CreateHistogram<double>("distributed_cache_operation_duration_ms", unit: "ms", ...);
}
```

`PdfMetrics` uses `internal static` (not `public static`) per Decision 5, and uses `PdfTelemetryNames` constants for metric names.

### Telemetry Pattern — MuonroiMetrics.cs (simplified variant)

File: `src/Muonroi.Observability/OpenTelemetry/MuonroiMetrics.cs` (39 lines)

```csharp
// Simpler variant — single Meter, all Counter<long>
public static class MuonroiMetrics
{
    private const string MeterName = "Muonroi.Ecosystem.Core";
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> GuardViolations = Meter.CreateCounter<long>(
        "muonroi.guard.violations", unit: "{violation}", description: "...");
}
```

Key difference for PdfMetrics: uses `Histogram<int>` for page count (not `Counter<long>`), so the DistributedCache pattern is the better reference.

### ITelemetryDescriptor Pattern

File: `src/Muonroi.Caching.Abstractions/Distributed/DistributedCacheTelemetryDescriptor.cs` (15 lines)

```csharp
// Exact pattern to follow for PdfTelemetryDescriptor
public class DistributedCacheTelemetryDescriptor : ITelemetryDescriptor
{
    public IEnumerable<string> ActivitySourceNames => [DistributedCacheRuntimeTelemetry.ActivitySourceName];
    public IEnumerable<string> MeterNames => [DistributedCacheRuntimeTelemetry.ActivitySourceName];
}
```

For `PdfTelemetryDescriptor`, both `ActivitySourceNames` and `MeterNames` return `[PdfTelemetryNames.ActivitySourceName]` (= `"Muonroi.BuildingBlock.Pdf"`).

Note: `RuleEngineTelemetryDescriptor` (16 lines) uses inline string literals rather than constants. The caching pattern (using the constants class) is preferred for PdfTelemetryDescriptor.

### Options Validation Pattern

Verified usage in `ServiceCollectionExtensions.cs` (line 49): `.AddOptions<MultiTenantOptions>().BindConfiguration(MultiTenantOptions.SectionName)`

No `ValidateOnStart()` exists in the repo yet — Phase 6 will be the first user of it. The pattern from CONTEXT.md Decision 8:

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

**Important caveats on `PdfLimits` constants:** `PdfConfigs.PdfLimits` properties are declared as `const` members (verified from `PdfConfigs.cs`), not instance properties. This means the `.Validate(cfg => ...)` lambda above validates that the *configuration-bound* `Limits` object (if someone overrides the defaults) has positive values. The constants serve as default values but a misconfigured `appsettings.json` can supply invalid values — `ValidateOnStart` catches that case.

### AddPdf() Pattern — TryAddSingleton

Verified from `RedisExtensions.cs` and `ServiceCollectionExtensions.cs`:

```csharp
// src/Muonroi.Pdf/Extensions/PdfServiceCollectionExtensions.cs
namespace Muonroi.Pdf.Extensions;

public static class PdfServiceCollectionExtensions
{
    public static IServiceCollection AddPdf(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options validation at startup
        services
            .AddOptions<PdfConfigs>()
            .BindConfiguration(PdfConfigs.SectionName)
            .Validate(cfg => /* all 7 limits > 0 */, "PdfConfigs: all limits must be positive integers")
            .ValidateOnStart();

        // Default service registrations — TryAdd so callers can override
        services.TryAddSingleton<IHtmlParser, AngleSharpHtmlParser>();
        services.TryAddSingleton<ICssCascadeEngine, AngleSharpCascadeEngine>();
        services.TryAddSingleton<IImageDecoder, PureImageDecoder>();
        services.TryAddSingleton<IPdfWriter, PdfSharpCoreWriter>(); // Phase 5 prerequisite
        services.TryAddSingleton<IResourceResolver, ThrowingResourceResolver>();
        services.TryAddSingleton<IPdfCssPolicy, DefaultStrictPolicy>();
        services.TryAddSingleton<IMPdfService, MPdfService>();
        // No default for IFontResolver

        return services;
    }
}
```

---

## PdfSharpCore Multi-Document Merge

**Current state:** `PdfSharpCoreWriter` does **not yet exist** in the codebase — it is the main remaining Phase 5 deliverable.

**What Phase 5 research documented (05-RESEARCH.md):** Decision 9 in CONTEXT.md states `RenderMultiPageAsync` uses `PdfDocument.Import` pattern. However, the Phase 5 research explicitly tagged the FileIdentifier API as `[ASSUMED]` (Assumptions Log A1) — the exact PdfSharpCore 1.3.65 API for document merge is not verified from source.

**PdfSharpCore 1.3.65 merge options (from Phase 5 research):**
- `PdfDocument.Import(PdfDocument)` — adds all pages from one document to another
- The Phase 5 research did not independently verify this method exists in 1.3.65 specifically

**Safe implementation strategy for Phase 6 Decision 9:**
```csharp
// RenderMultiPageAsync implementation outline
public async Task<PdfRenderResult> RenderMultiPageAsync(
    IReadOnlyList<string> htmlPages,
    Stream destination,
    PdfRenderOptions options,
    CancellationToken cancellationToken = default)
{
    if (htmlPages.Count == 0)
        return new PdfRenderResult(0, 0, TimeSpan.Zero, "", "", []);

    // Render each fragment to its own MemoryStream
    var tempStreams = new List<MemoryStream>(htmlPages.Count);
    var results = new List<PdfRenderResult>(htmlPages.Count);
    foreach (string html in htmlPages)
    {
        var ms = new MemoryStream();
        tempStreams.Add(ms);
        results.Add(await RenderAsync(html, ms, options, cancellationToken));
    }

    // Merge via PdfSharpCore PdfDocument.Import (verify API at Phase 5 completion)
    // ... merge logic depends on PdfSharpCoreWriter exposing a merge helper or
    //     MPdfService directly using PdfSharpCore to merge the MemoryStream fragments
    long byteCount = /* merged stream length */ 0;
    return new PdfRenderResult(results.Sum(r => r.PageCount), byteCount, ...);
}
```

**Recommendation:** Phase 6 planner should treat the merge approach as a **Phase 5/6 boundary item**. Two options:
1. `PdfSharpCoreWriter` exposes a `MergeAsync(IEnumerable<Stream> fragments, Stream destination)` method — preferred
2. `MPdfService` directly imports PdfSharpCore and uses `PdfDocument.Import` — couples MPdfService to PdfSharpCore

The plan should default to option 1 and note the dependency.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ActivitySource + Meter lifecycle | Custom telemetry adapter | `System.Diagnostics.ActivitySource` + `System.Diagnostics.Metrics.Meter` (BCL) | Static singletons per .NET 8 BCL pattern; no extra packages needed |
| Scoped-from-singleton resolution | `IHttpContextAccessor` or scoped inject | `IServiceProvider.GetService<ITenantContext>()` (Decision 6) | Only safe pattern for ambient per-request state in a singleton |
| CancellationToken timeout | `Task.WhenAny(task, Task.Delay(...))` | `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter` | Canonical .NET pattern; propagates as `OperationCanceledException` through all await points |
| Config validation | Manual validation in constructor | `AddOptions<T>().Validate().ValidateOnStart()` | Host throws at startup with descriptive message before first request |
| Multi-page merge | Manual PDF stream concatenation | `PdfSharpCore` `PdfDocument.Import` | PdfSharpCore maintains cross-reference table integrity |

---

## Common Pitfalls

### Pitfall 1: Scoped service captured in singleton
`MPdfService` is a singleton. `ITenantContext` is typically scoped (HTTP request scope). Direct constructor injection of `ITenantContext` into `MPdfService` **throws at runtime** with `InvalidOperationException: Cannot consume scoped service from singleton`.
**Mitigation (Decision 6):** Inject `IServiceProvider` (safe as singleton) and call `_serviceProvider.GetService<ITenantContext>()` per render call. Returns `null` when not in an HTTP context.

### Pitfall 2: Static Meter/ActivitySource disposal
`Meter` and `ActivitySource` are static readonly fields in `PdfMetrics`. They must **never** be disposed during the process lifetime. Wrapping them in `using` blocks (e.g., inside a method) will cause `ObjectDisposedException` on subsequent render calls.
**Mitigation:** Declare as `static readonly` class-level fields — same pattern as `DistributedCacheRuntimeTelemetry` and `MuonroiMetrics`.

### Pitfall 3: PdfConfigs PdfLimits constants vs. instance properties
`PdfLimits` members (`MaxHtmlBytes`, `MaxDomDepth`, etc.) are **compile-time `const` values**, not instance properties (verified from `PdfConfigs.cs`). An `appsettings.json` section `PdfConfigs.Limits.*` will be bound to a fresh `PdfLimits()` instance — if the JSON key spellings are wrong, the constants silently retain their defaults. `ValidateOnStart()` catches explicitly bad values but not missing overrides. Document this for operators.

### Pitfall 4: CancellationToken propagation — OperationCanceledException
All async methods in the pipeline chain accept `CancellationToken` and throw `OperationCanceledException` on cancellation. SC4 requires this exception to propagate **unmodified**. A bare `catch (Exception)` block in `RenderAsync` must rethrow `OperationCanceledException` before logging, or at minimum not swallow it.
**Mitigation:** Structure catch blocks as `catch (OperationCanceledException) { throw; }` before the general `catch (Exception ex)` handler.

### Pitfall 5: PdfSharpCoreWriter not available at DI registration time
Since Phase 5 is incomplete (no `PdfSharpCoreWriter`), the `services.TryAddSingleton<IPdfWriter, PdfSharpCoreWriter>()` line in `AddPdf()` will not compile. Phase 6 plan must sequence `PdfSharpCoreWriter` delivery (Phase 5 wave 2) before Phase 6 wave 2.

### Pitfall 6: LayoutEngine.Layout is synchronous
`LayoutEngine.Layout()` (verified: 178 lines) is a **synchronous** method, not `async`. The pipeline chain in Decision 2 shows `LayoutEngine.LayoutAsync` but the actual method signature is `Layout(IStyledDocument, PdfRenderOptions)`. `MPdfService` must call it synchronously (or wrap in `Task.Run` if needed for responsiveness, but this adds overhead). Verify the actual signature against the file before implementing.

### Pitfall 7: FontPipeline.ResolveAsync signature mismatch
`FontPipeline.ResolveAsync` (verified, 48 lines) accepts `IFontResolver` (required parameter, not optional). When `IFontResolver` is not registered (Decision 7 — no default), `MPdfService` must null-check and skip font resolution rather than calling `FontPipeline.ResolveAsync` with null.

---

## Code Examples

### PdfTelemetryDescriptor (verified pattern)
```csharp
// src/Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryDescriptor.cs
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Pdf.Abstractions.Telemetry;

public sealed class PdfTelemetryDescriptor : ITelemetryDescriptor
{
    public IEnumerable<string> ActivitySourceNames => [PdfTelemetryNames.ActivitySourceName];
    public IEnumerable<string> MeterNames         => [PdfTelemetryNames.ActivitySourceName];
}
```

### PdfMetrics (verified pattern from DistributedCacheRuntimeTelemetry)
```csharp
// src/Muonroi.Pdf/Internal/Telemetry/PdfMetrics.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Muonroi.Pdf.Abstractions.Telemetry;

namespace Muonroi.Pdf.Internal.Telemetry;

internal static class PdfMetrics
{
    internal static readonly ActivitySource Source =
        new(PdfTelemetryNames.ActivitySourceName);

    private static readonly Meter _meter =
        new(PdfTelemetryNames.ActivitySourceName);

    internal static readonly Counter<long> OperationCounter =
        _meter.CreateCounter<long>(
            PdfTelemetryNames.OperationMetric,
            unit: "{render}",
            description: "Counts PDF render operations.");

    internal static readonly Histogram<int> PageCountHistogram =
        _meter.CreateHistogram<int>(
            PdfTelemetryNames.PageCountMetric,
            unit: "{page}",
            description: "Distribution of page count per PDF render.");
}
```

### Activity usage in render loop (verified from DistributedCacheRuntimeTelemetry pattern)
```csharp
using Activity? activity = PdfMetrics.Source.StartActivity("pdf.render", ActivityKind.Internal);
activity?.SetTag(PdfTelemetryNames.TemplateIdTag, options.TemplateId ?? "");
activity?.SetTag(PdfTelemetryNames.TenantIdTag, tenantId);
// ... pipeline work ...
activity?.SetStatus(ActivityStatusCode.Ok);
// Disposed by 'using' — ends the span
```

### ITenantContext null-safe resolution (from CONTEXT.md Decision 6)
```csharp
// In MPdfService.RenderAsync — per-call, never cached
string tenantId = _serviceProvider.GetService<ITenantContext>()?.TenantId ?? "unknown";
```
`ITenantContext` is in `Muonroi.Tenancy.Abstractions` namespace (verified):
```csharp
public interface ITenantContext { string? TenantId { get; set; } }
```

---

## Environment Availability

| Dependency | Required By | Available | Version | Notes |
|------------|------------|-----------|---------|-------|
| .NET SDK | Compilation | Yes | 10.0.201 (host); target `net8.0` | No `global.json` found — SDK version is host machine SDK |
| `Muonroi.Pdf.csproj` target | Phase 6 | Yes | `net8.0` | Confirmed from csproj |
| `Muonroi.Pdf.Abstractions.csproj` target | Phase 6 | Yes | `netstandard2.0` | PdfTelemetryDescriptor goes here |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `AddPdf()` | In CPM | `$(MicrosoftExtensionsVersion)` | Not yet in `Muonroi.Pdf.csproj` |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `BindConfiguration()` | In CPM | `$(MicrosoftExtensionsVersion)` | Not yet in `Muonroi.Pdf.csproj` |
| `System.Diagnostics.ActivitySource` | `PdfMetrics` | BCL (.NET 8) | built-in | No NuGet needed |
| `System.Diagnostics.Metrics.Meter` | `PdfMetrics` | BCL (.NET 8) | built-in | No NuGet needed |
| `PdfSharpCore` | `PdfSharpCoreWriter` (Phase 5) | In CPM | 1.3.65 | Not yet in `Muonroi.Pdf.csproj` — Phase 5 must add it |
| `xunit` + `FluentAssertions` + `NSubstitute` | Tests | In CPM | 2.9.2 / 7.2.0 / 5.3.0 | Auto-inherited via `Directory.Build.props` for test projects |
| `dotnet test` | Test runner | Yes | net8.0 | `tests/Muonroi.Pdf.Tests/` exists; no service/integration test project |

---

## Validation Architecture

### Existing Test Infrastructure

| Project | Files | What It Tests |
|---------|-------|---------------|
| `tests/Muonroi.Pdf.Tests/` | `Layout/*.cs` (6 files), `Font/*.cs` (3 files), `Image/ImagePipelineTests.cs` | Phases 2–4 unit tests |
| `tests/Muonroi.Pdf.Governance.Tests/` | — | Governance/Policy tests |
| `tests/Muonroi.Pdf.Abstractions.Tests/` | — | Contracts tests |

The `Muonroi.Pdf.Tests.csproj` references both `Muonroi.Pdf` and `Muonroi.Pdf.Abstractions`. Phase 6 integration tests can be added to this project.

### Phase 6 Test Needs

| Req | Behavior | Test Type | Suggested Location |
|-----|----------|-----------|-------------------|
| DI-01/02 | `AddPdf()` registers expected services; IFontResolver absent | unit | `tests/Muonroi.Pdf.Tests/Service/DependencyInjectionTests.cs` |
| DI-03 | `ValidateOnStart` throws on invalid limits | unit | `tests/Muonroi.Pdf.Tests/Service/ConfigValidationTests.cs` |
| DI-04 | `PdfTelemetryDescriptor` returns correct source + meter names | unit | `tests/Muonroi.Pdf.Abstractions.Tests/Telemetry/PdfTelemetryDescriptorTests.cs` |
| PIPE-07/08 | End-to-end `RenderAsync` produces non-empty PDF; timeout fires | integration | `tests/Muonroi.Pdf.Tests/Service/MPdfServiceTests.cs` |
| TEL-02/03 | `PdfMetrics.OperationCounter` incremented; `PageCountHistogram` recorded | unit | `tests/Muonroi.Pdf.Tests/Service/MPdfServiceTests.cs` |
| TEL-04 | Activity tags present with correct keys | unit | Same |

**Test prerequisites:** `PdfSharpCoreWriter` must exist before any integration test that exercises the full `RenderAsync` path. Unit tests for DI registration, config validation, and `PdfTelemetryDescriptor` can proceed independently.

---

## Sources

All files read and verified from `D:/sources/Core/muonroi-building-block/`:

| File | Lines | Key Finding |
|------|-------|-------------|
| `src/Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryNames.cs` | 18 | 5 constants confirmed; `PdfTelemetryDescriptor` does NOT exist |
| `src/Muonroi.Pdf.Abstractions/IMPdfService.cs` | 49 | 3 overloads; singleton-safe documented |
| `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` | 28 | `SectionName`, `PdfLimits` with 7 `const` members |
| `src/Muonroi.Pdf.Abstractions/PdfRenderResult.cs` | 16 | Sealed record; 6 properties |
| `src/Muonroi.Pdf.Abstractions/Engine/IPdfWriter.cs` | 12 | `WriteAsync` returns `ValueTask<long>` |
| `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` | 178 | `Layout()` is synchronous — not async |
| `src/Muonroi.Pdf/Internal/Font/FontPipeline.cs` | 48 | `ResolveAsync` requires non-null `IFontResolver` |
| `src/Muonroi.Pdf/Internal/Image/ImagePipeline.cs` | 82 | `ResolveAsync` signature confirmed |
| `src/Muonroi.Pdf/Internal/Image/PureImageDecoder.cs` | 95 | Default `IImageDecoder` available |
| `src/Muonroi.Pdf/Internal/Security/ThrowingResourceResolver.cs` | 28 | Exists; default `IResourceResolver` ready |
| `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` | 11 | `EmbeddedFonts` + `Images` properties confirmed |
| `src/Muonroi.Pdf.Governance/Parsing/AngleSharpHtmlParser.cs` | 56 | Default `IHtmlParser` ready |
| `src/Muonroi.Pdf.Governance/Cascade/AngleSharpCascadeEngine.cs` | 24 | Default `ICssCascadeEngine` ready |
| `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` | 139 | Default `IPdfCssPolicy` ready |
| `src/Muonroi.Observability/OpenTelemetry/MuonroiMetrics.cs` | 39 | Static Meter + Counter pattern |
| `src/Muonroi.Caching.Abstractions/Distributed/DistributedCacheRuntimeTelemetry.cs` | 68 | Static ActivitySource + Meter + Histogram — primary PdfMetrics model |
| `src/Muonroi.Caching.Abstractions/Distributed/DistributedCacheTelemetryDescriptor.cs` | 15 | Primary `PdfTelemetryDescriptor` model |
| `src/Muonroi.RuleEngine.Abstractions/Telemetry/RuleEngineTelemetryDescriptor.cs` | 16 | Secondary descriptor model |
| `src/Muonroi.Core.Abstractions/Interfaces/ITelemetryDescriptor.cs` | 17 | Interface shape confirmed |
| `src/Muonroi.Tenancy.Abstractions/ITenantContext.cs` | 12 | `TenantId` property shape confirmed |
| `src/Muonroi.RuleEngine.EntityFrameworkCore/ServiceCollectionExtensions.cs` | 108 | `BindConfiguration()` usage verified |
| `src/Muonroi.Caching.Redis/Redis/RedisExtensions.cs` | 528 | `TryAddSingleton` pattern verified |
| `Directory.Packages.props` | 152 | All NuGet versions confirmed |
| `Directory.Build.props` | 91 | Target framework, test project detection |
| `src/Muonroi.Pdf/Muonroi.Pdf.csproj` | 20 | Only `SixLabors.Fonts` referenced; DI packages missing |
| `src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj` | 18 | `netstandard2.0`; only BCL polyfills |
| `.planning/phases/06-di-telemetry-integration/06-CONTEXT.md` | 175 | All 10 locked decisions |
| `.planning/phases/05-pdf-writer-determinism-security/05-RESEARCH.md` | 550 | Phase 5 status; `PdfSharpCoreWriter` not yet built |
