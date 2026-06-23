# Muonroi.Pdf.Enterprise

> Enterprise-tier extensions for `Muonroi.Pdf`: per-tenant quota metering, license-backed capability gates, an HTTP template registry client, polling hot-reload, and an SSIM visual-quality scorer.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.Enterprise.svg)](https://www.nuget.org/packages/Muonroi.Pdf.Enterprise/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

`Muonroi.Pdf.Enterprise` sits on top of the OSS `Muonroi.Pdf` engine and adds capabilities that require a commercial activation proof. It decorates `IMPdfService` with a metering wrapper that records per-tenant `PdfRendersPerDay` quota via `ITenantQuotaTracker`, replaces the no-op feature gate with a governance-backed `LicenseFeatureGate`, and ships an HTTP client for the Muonroi PDF template registry together with a transport-agnostic polling hot-reload service. A pure-managed SSIM scorer is included for visual-regression quality gates. The package has a one-way dependency on the OSS engine — the OSS engine has zero awareness of this package.

## Installation

```bash
dotnet add package Muonroi.Pdf.Enterprise --prerelease
```

## Quick Start

Call order matters: `AddPdf` must run before `AddPdfEnterprise`, and `AddMEnterpriseGovernance` must run before both (it registers `ILicenseGuard`).

```csharp
using Muonroi.Pdf.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. OSS PDF engine — registers IMPdfService (MPdfService) via TryAddSingleton.
builder.Services.AddPdf(builder.Configuration);

// 2. Governance layer — registers ILicenseGuard (required by LicenseFeatureGate).
builder.Services.AddMEnterpriseGovernance(builder.Configuration);

// 3. Enterprise decorator — replaces IMPdfService with EnterprisePdfServiceWrapper
//    (quota metering) and registers LicenseFeatureGate as IFeatureGate.
builder.Services.AddPdfEnterprise();
```

For **development / OSS** environments where no license activation is needed, skip `AddPdfEnterprise` and register the no-op gate directly:

```csharp
// dev / OSS: all capability keys are always allowed; metering is skipped.
builder.Services.AddSingleton(AlwaysAllowFeatureGate.Instance);
```

Guard a feature in any service:

```csharp
public class MyService(IFeatureGate gate, IMPdfService pdf)
{
    public async Task<byte[]> RenderWithRegistryAsync(string html, CancellationToken ct)
    {
        // Throws FeatureNotLicensedException if pdf.registry is not in the activation proof.
        gate.EnsureFeatureOrThrow(CapabilityKeys.PdfRegistry);

        (byte[] bytes, _) = await pdf.RenderToBytesAsync(html, new PdfRenderOptions(), ct);
        return bytes;
    }
}
```

## Features

- **Per-tenant quota metering** — `EnterprisePdfServiceWrapper` decorates `IMPdfService` and increments `QuotaType.PdfRendersPerDay` via `ITenantQuotaTracker` after every render. Metering failures are caught, logged, and swallowed so the render result is always returned.
- **License-backed capability gate** — `LicenseFeatureGate` delegates to `ILicenseGuard.HasFeature`, which reads the RSA-verified `ActivationProof.Features[]` supplied by the governance layer.
- **HTTP template registry client** — `HttpPdfTemplateRegistry` fetches template descriptors and version payloads from the control-plane REST API (`GET /api/v1/pdf-templates/{id}` and `/versions/{version}`). Uses the named `HttpClient` `"PdfTemplateRegistry"`.
- **Polling hot-reload** — `PdfTemplateHotReload` polls a configured list of template IDs via `IMPdfTemplateRegistry.LookupAsync` and notifies an `IAsyncObserver<TemplateChange>` whenever a version changes or a template disappears. Works against any registry implementation; no push transport required.
- **SSIM visual-quality scorer** — `SsimScorer.Compare` computes mean structural similarity (Wang/Bovik 2004, Rec.709 luma) between two interleaved 8-bit RGB pixel buffers. Returns 1.0 for identical buffers; use as a canary gate threshold.
- **Capability key constants** — `CapabilityKeys` defines `pdf.designer`, `pdf.registry`, and `pdf.canary` as typed constants for use with `IFeatureGate`.

## Configuration

### DI registration

```csharp
// Register the named HttpClient the registry resolves from the factory.
builder.Services.AddHttpClient(PdfTemplateRegistryOptions.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://control-plane.example.com/");
});

// Register HttpPdfTemplateRegistry as IMPdfTemplateRegistry.
builder.Services.AddSingleton<PdfTemplateRegistryOptions>(new PdfTemplateRegistryOptions
{
    AccessTokenFactory = async ct => await tokenProvider.GetTokenAsync(ct)
});
builder.Services.AddSingleton<IMPdfTemplateRegistry, HttpPdfTemplateRegistry>();
```

### Hot-reload

```csharp
var options = new PdfTemplateHotReloadOptions
{
    PollInterval = TimeSpan.FromSeconds(30),   // default: 30 s
    TemplateIds  = ["invoice-v2", "receipt-v1"]
};

// PdfTemplateHotReload.StartAsync is the loop entry point; wrap in a hosted service.
var hotReload = new PdfTemplateHotReload(registry, myObserver, options);
_ = Task.Run(() => hotReload.StartAsync(appLifetime.ApplicationStopping));
```

### Options reference

| Type | Property | Default | Purpose |
|------|----------|---------|---------|
| `PdfTemplateRegistryOptions` | `HttpClientName` (const) | `"PdfTemplateRegistry"` | Named `HttpClient` key |
| `PdfTemplateRegistryOptions` | `AccessTokenFactory` | `null` (no auth) | Per-request bearer token factory |
| `PdfTemplateHotReloadOptions` | `PollInterval` | `30 s` | How often tracked templates are re-checked |
| `PdfTemplateHotReloadOptions` | `TemplateIds` | `[]` (idle) | Template identifiers to watch |

## API Reference

| Type | Purpose |
|------|---------|
| `IFeatureGate` | Contract for capability checks (`IsEnabled`, `EnsureFeatureOrThrow`) |
| `FeatureNotLicensedException` | Thrown by `EnsureFeatureOrThrow` when a capability is not licensed |
| `CapabilityKeys` | String constants `pdf.designer`, `pdf.registry`, `pdf.canary` |
| `AlwaysAllowFeatureGate` | No-op gate for OSS/dev — every capability returns `true` |
| `LicenseFeatureGate` | Production gate backed by `ILicenseGuard` (registered by `AddPdfEnterprise`) |
| `EnterprisePdfServiceWrapper` | `IMPdfService` decorator that records per-tenant quota after each render |
| `IMPdfTemplateRegistry` | Client contract: `LookupAsync`, `ResolveAsync`, `SubscribeAsync` |
| `HttpPdfTemplateRegistry` | REST implementation of `IMPdfTemplateRegistry` |
| `PdfTemplateRegistryOptions` | Configuration for `HttpPdfTemplateRegistry` |
| `PdfTemplateHotReload` | Polling-based hot-reload over any `IMPdfTemplateRegistry` |
| `PdfTemplateHotReloadOptions` | Configuration for `PdfTemplateHotReload` |
| `IMPdfTemplateHotReload` | Contract implemented by `PdfTemplateHotReload` |
| `IAsyncObserver<T>` | Async observer contract used by registry subscription and hot-reload |
| `TemplateDescriptor` | Summary record: `TemplateId`, `Name`, `LatestVersion`, `Tags` |
| `TemplateVersion` | Full record with `Content` payload and `PublishedAt` timestamp |
| `TemplateChange` | Change notification: `TemplateId`, `NewVersion`, `ChangeKind` |
| `TemplateChangeKind` | `Updated` or `Deleted` |
| `SsimScorer` | Static SSIM scorer: `Compare(rgbA, rgbB, width, height) → double` |

## Samples

- [Quickstart.Pdf.Advanced](../../samples/Quickstart.Pdf.Advanced/) — ASP.NET Core API demonstrating `AlwaysAllowFeatureGate`, `CapabilityKeys`, `SsimScorer`, and `IMPdfTemplateRegistry` alongside the OSS PDF engine and the design-system template provider.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.Pdf`](../Muonroi.Pdf/) — OSS HTML/CSS-to-PDF engine; must be registered before this package
- [`Muonroi.Pdf.Abstractions`](../Muonroi.Pdf.Abstractions/) — `IMPdfService`, `PdfRenderOptions`, and `PdfRenderResult` contracts shared by both layers
- [`Muonroi.Governance.Enterprise`](../Muonroi.Governance.Enterprise/) — provides `ILicenseGuard` and `AddMEnterpriseGovernance`; required before calling `AddPdfEnterprise`

## License

Commercial license — requires a valid Muonroi activation proof. Contact [leanhphi1706@gmail.com](mailto:leanhphi1706@gmail.com) or see [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL) for terms.
