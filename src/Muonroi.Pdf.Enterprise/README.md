# Muonroi.Pdf.Enterprise

> Enterprise extensions for Muonroi.Pdf: template registry, hot-reload, quality scoring, and capability gates.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.Enterprise.svg)](https://www.nuget.org/packages/Muonroi.Pdf.Enterprise/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Pdf.Enterprise` package extends the OSS Muonroi PDF engine with features tailored for large-scale, multi-tenant enterprise deployments. It introduces dynamic capability enforcement based on tenant licenses, remote template registries with live hot-reloading, high-quality PNG decoding routines, and automated visual quality verification.

This package wraps the core `IMPdfService` inside the `EnterprisePdfServiceWrapper`, ensuring that usage quotas, advanced image processing, and external template resolutions are managed transparently.

## Features

- **Template Registry Client**: `HttpPdfTemplateRegistry` implements `IMPdfTemplateRegistry` to fetch PDF HTML templates from a centralized HTTP registry instead of relying on local disk or embedded strings.
- **Hot-Reload Integration**: `PdfTemplateHotReload` implements `IMPdfTemplateHotReload` to hook into Redis pub/sub to automatically invalidate cached templates across distributed worker nodes the moment a designer publishes an update.
- **Capability Gates**: Integrates with the broader Muonroi License system via `LicenseFeatureGate` to lock/unlock PDF features (like high-res rendering or custom fonts) per tenant based on `CapabilityKeys`.
- **Visual Quality Scorer**: `SsimScorer` provides Structural Similarity Index Measure (SSIM) algorithms to programmatically compare generated PDFs against golden baselines during CI/CD.
- **PNG Decoding**: `PngDecoder` provides advanced lossless image parsing for embedded graphics, bypassing native GDI+ dependencies in containerized Linux environments.
- **Service Wrapping**: `EnterprisePdfServiceWrapper` injects metering and telemetry on top of all render calls.

## Installation

```bash
dotnet add package Muonroi.Pdf.Enterprise
```

## Quick Start

Enable enterprise features during your application's service registration:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Extensions;
using Muonroi.Pdf.Enterprise.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Add OSS PDF features
builder.Services.AddMPdfService(builder.Configuration);

// 2. Wrap and inject Enterprise capabilities
builder.Services.AddEnterprisePdfServices(builder.Configuration);

var app = builder.Build();

// The IMPdfService resolved here is now the EnterprisePdfServiceWrapper
app.MapPost("/generate-from-registry", async (IMPdfService pdfService, IMPdfTemplateRegistry registry) => 
{
    // Fetch a template from the remote registry using its slug
    string html = await registry.GetTemplateAsync("monthly-invoice-v2");
    
    using var output = new MemoryStream();
    
    // Renders the template, logging metering metrics to OTel 
    // and validating tenant feature gates.
    await pdfService.RenderAsync(html, output, new PdfRenderOptions());
    
    return Results.File(output.ToArray(), "application/pdf");
});
```

## API Reference

### `EnterprisePdfServiceWrapper`
Implements `IMPdfService`. This decorator wraps the inner OSS layout engine. It intercepts `RenderAsync` calls to increment metering telemetry, validate tenant license feature limits (via `IFeatureGate`), and enforce structural policies.

### `IMPdfTemplateRegistry` / `HttpPdfTemplateRegistry`
Fetches and caches templates from a remote HTTP origin.

### `IMPdfTemplateHotReload` / `PdfTemplateHotReload`
Connects to a message broker (Redis) to listen for cache invalidation events. When a designer edits a template in the web UI, this service drops the local cache, ensuring the next PDF generation uses the freshest HTML.

### `SsimScorer`
A utility primarily used in test projects and CI/CD pipelines to ensure PDF visual regressions do not slip into production. It compares rasterized outputs of PDFs.

### `CapabilityKeys`
Static strings representing license gates, such as `"Pdf.ModernLayout"`, `"Pdf.Watermarking"`, or `"Pdf.CustomFonts"`. Checked against `IFeatureGate`.

## Ecosystem Combinations

### + Muonroi.Governance.Enterprise â†’ Licensed Feature Gating
Enterprise respects the governance license tier through `LicenseFeatureGate`. It automatically blocks access to `CapabilityKeys.PdfModernLayout` or `CapabilityKeys.PdfWatermarking` if the active tenant's license lacks the required entitlements.

### + Muonroi.Tenancy.Core â†’ Multi-Tenant Policy Enforcement
`EnterprisePdfServiceWrapper` intercepts rendering calls to apply per-tenant PDF quality settings, quota consumption limits, and branding, ensuring isolation.

### + Muonroi.Caching.Redis â†’ Multi-Pod Template Sync
The HTTP template registry is cached in memory by `HttpPdfTemplateRegistry`. By adding Redis, `PdfTemplateHotReload` listens for real-time invalidation messages to synchronize template cache invalidation across a multi-pod cluster.

### Full PDF Production Stack
```csharp
builder.Services
    .AddMPdfService(config)             
    .AddPdfGovernance(config)           
    .AddPdfDesignSystem(config)         
    .AddEnterprisePdfServices(config);  
```

## Samples
- [`Muonroi.Pdf.Samples`](../../samples/Muonroi.Pdf.Samples)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
