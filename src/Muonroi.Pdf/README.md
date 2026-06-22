# Muonroi.Pdf

> Pure-managed HTML/CSS to PDF layout engine — box-tree construction, pagination, and rendering coordination for Muonroi applications.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.svg)](https://www.nuget.org/packages/Muonroi.Pdf/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Pdf` converts HTML + CSS into PDF without a browser, headless Chromium, or any native binary dependency. It ships an HTML parser (AngleSharp), a CSS cascade engine, a block-stack / Flexbox / CSS Grid layout engine, and a PDF writer — all pure managed code, AOT-compatible. The engine integrates with Muonroi.Logging, Muonroi.Tenancy, and OTel telemetry automatically when registered via `AddPdf`.

## Installation

```bash
dotnet add package Muonroi.Pdf --prerelease
```

## Quick Start

```csharp
// Program.cs — Generic Host (required: DefaultFontResolver needs IHostEnvironment)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
// AddPdf wires the full pipeline and binds PdfConfigs from appsettings.json.
builder.Services.AddPdf(builder.Configuration);

IHost host = builder.Build();
IMPdfService pdf = host.Services.GetRequiredService<IMPdfService>();

string html = """
    <!DOCTYPE html>
    <html><head><style>
      body { font-family: Arial, sans-serif; font-size: 12pt; color: #222; }
      h1   { color: #0c6b6b; }
    </style></head>
    <body>
      <h1>Hello, Muonroi.Pdf</h1>
      <p>Pure-managed HTML to PDF. No browser, no native binary.</p>
    </body></html>
    """;

await using FileStream output = File.Create("output.pdf");
PdfRenderResult result = await pdf.RenderAsync(html, output, new PdfRenderOptions
{
    PageSize    = PdfPageSize.A4,
    Orientation = PdfOrientation.Portrait,
    Margins     = PdfMargins.Uniform(15),
    TemplateId  = "hello-world",
});
Console.WriteLine($"{result.PageCount} page(s), {result.ByteCount} bytes, {result.Elapsed.TotalMilliseconds:F0} ms");
```

### Enable Flexbox and CSS Grid

Both layout engines are opt-in. Set the flag in `appsettings.json`:

```json
{
  "PdfConfigs": {
    "Policy": {
      "AllowModernLayout": true
    }
  }
}
```

Or via in-memory configuration for testing:

```csharp
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["PdfConfigs:Policy:AllowModernLayout"] = "true",
});
builder.Services.AddPdf(builder.Configuration);
```

With `AllowModernLayout: false` (the default), `display:flex` and `display:grid` are rejected at the policy gate and throw `PdfPolicyException` before any rendering begins — fail-loud per charter.

## Features

- **Block-stack layout** — margins, padding, borders, floats, tables, percentage widths
- **Real CSS Flexbox** — `flex-direction`, `flex-grow/shrink/basis`, `gap`, `align-items`, `justify-content` (opt-in via `AllowModernLayout`)
- **Real CSS Grid** — `grid-template-columns/rows`, named areas, `repeat(auto-fill | auto-fit)`, `fr` tracks, `gap` (opt-in via `AllowModernLayout`)
- **Running header and footer** — three-column (`LeftHtml`, `CenterHtml`, `RightHtml`), configurable height, optional separator line, `counter(page)` / `counter(pages)` counters
- **Gradients** — `linear-gradient` (axial PDF shading) and `radial-gradient` (ShadingType 3)
- **Transforms** — `transform: rotate(deg)` (watermarks and diagonal overlays)
- **Multi-page rendering** — `RenderMultiPageAsync` merges HTML fragments into one PDF; each fragment starts on a new page
- **Policy gate** — CSS subset enforcement before rendering; `PdfPolicyException` carries per-violation `RuleId`, `CssSelector`, `RejectedValue`, and `SuggestedAlternative`
- **Input limits** — HTML size, DOM depth, element count, image pixels, page count, render duration, and font files — all validated at startup via `ValidateOnStart()`
- **Bundled fonts** — Liberation fonts (SIL OFL-1.1) embedded as resources; no OS font installation required
- **Custom font resolver** — implement `IFontResolver` and pre-register before `AddPdf` to override
- **NativeAOT compatible** — hot path has no reflection-emit; trim warnings on the AngleSharp CSS path are suppressed via `TrimmerRootDescriptor` in the AOT sample
- **Idempotent DI registration** — all registrations use `TryAdd*`; pre-registering an override (e.g. a custom `IFontResolver`) wins

## Configuration

`AddPdf` binds configuration from the `"PdfConfigs"` section and validates it at startup. All limit values must be positive.

```json
{
  "PdfConfigs": {
    "Limits": {
      "MaxHtmlBytes": 8388608,
      "MaxDomDepth": 256,
      "MaxElementCount": 100000,
      "MaxImagePixels": 25000000,
      "MaxPages": 1000,
      "MaxRenderDurationMs": 15000,
      "MaxFontFiles": 32
    },
    "Policy": {
      "AllowModernLayout": false,
      "SoftDegradeUnknownDisplay": false
    },
    "FontResolver": {
      "FallbackToFirstRegistered": true,
      "GenericFamilyMap": {
        "serif": "Times New Roman",
        "sans-serif": "Arial",
        "monospace": "Courier New"
      },
      "Fonts": [
        { "Family": "MyFont", "Path": "fonts/MyFont-Regular.ttf", "Weight": 400, "Style": "Normal" },
        { "Family": "MyFont", "Path": "fonts/MyFont-Bold.ttf",    "Weight": 700, "Style": "Normal" }
      ]
    },
    "RequirePolicySignature": false
  }
}
```

`PdfPolicySettings.SoftDegradeUnknownDisplay`: when `true`, `display:flex/grid` emits a `Warning` violation instead of an `Error` and the element is rendered as `display:block` (no abort). Default `false` (fail-loud).

## API Reference

| Type | Purpose |
|------|---------|
| `IMPdfService` | Primary rendering service; resolved from DI |
| `IMPdfService.RenderAsync` | Renders HTML to a caller-owned `Stream`; returns `PdfRenderResult` metadata |
| `IMPdfService.RenderMultiPageAsync` | Merges a list of HTML fragments into one PDF |
| `IMPdfService.RenderToBytesAsync` | Convenience overload; buffers output into `byte[]` |
| `PdfRenderOptions` | Per-call options: `PageSize`, `Orientation`, `Margins`, `Header`, `Footer`, `UserStyleSheet`, `Policy`, `FontResolver`, `ResourceResolver`, `CorrelationId`, `TemplateId` |
| `PdfRenderResult` | Render metadata: `PageCount`, `ByteCount`, `Elapsed`, `TemplateHash`, `PolicyId`, `Diagnostics` |
| `PdfHeaderFooter` | Running header or footer: `LeftHtml`, `CenterHtml`, `RightHtml`, `HeightMm`, `ShowLine` |
| `PdfMargins` | Page margins in millimetres; `PdfMargins.Default10mm` and `PdfMargins.Uniform(mm)` |
| `PdfPageSize` | Enum: `A4`, `A3`, `Letter`, `Legal`, and others |
| `PdfOrientation` | Enum: `Portrait`, `Landscape` |
| `PdfConfigs` | IConfiguration-bound options; section key `"PdfConfigs"` |
| `PdfConfigs.PdfLimits` | Nested limits sub-class; section key `"PdfConfigs:Limits"` |
| `PdfPolicySettings` | CSS policy tunables (`AllowModernLayout`, `SoftDegradeUnknownDisplay`); section key `"PdfConfigs:Policy"` |
| `PdfFontResolverConfig` | Font registration list and generic-family map; section key `"PdfConfigs:FontResolver"` |
| `IFontResolver` | Override font resolution; pre-register before `AddPdf` to take effect |
| `IPdfCssPolicy` | Override the CSS policy gate; default is `LegacyPrintPolicy` |
| `IMPdfRenderer<TModel>` | Strongly-typed per-template renderer (populated by source generator v0.2+) |
| `IMPdfRendererFactory` | Resolves `IMPdfRenderer<TModel>` by template id |
| `PdfTemplateAttribute` | Marks a model class with a compile-time template id for the source generator |
| `PdfPolicyException` | Thrown when the CSS policy gate rejects the document; `Violations` lists each rule breach |
| `PdfFormatException` | Thrown on structural PDF format errors |
| `PdfInputLimitException` | Thrown when an input limit (`MaxHtmlBytes`, `MaxPages`, etc.) is exceeded |
| `PdfSecurityException` | Thrown on security violations (e.g. disallowed resource schemes) |
| `AddPdf(IServiceCollection, IConfiguration)` | Extension method in `Muonroi.Pdf.Extensions`; composition root for the pipeline |

## Samples

- [Muonroi.Pdf.Samples](../../samples/Muonroi.Pdf.Samples/) — Worked examples: minimal, invoice with tables, running header/footer with page counters, watermark + gradient, Flexbox cards, CSS Grid dashboard, multi-page merge, and policy-rejection demonstration. Run with `dotnet run`.
- [Muonroi.Pdf.AotSample](../../samples/Muonroi.Pdf.AotSample/) — NativeAOT smoke test with an embedded font and no OS dependencies.
- [Quickstart.Pdf.Advanced](../../samples/Quickstart.Pdf.Advanced/) — Advanced integration sample for ASP.NET Core hosts.

## Compatibility

- Target framework: `net8.0`
- NativeAOT: compatible (hot path verified AOT-clean; AngleSharp CSS trim warnings suppressed in the AOT sample)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Pdf.Abstractions`](../Muonroi.Pdf.Abstractions/) — Public contracts (`IMPdfService`, `PdfRenderOptions`, `IFontResolver`, `IPdfCssPolicy`, etc.); this package depends on it
- [`Muonroi.Pdf.Governance`](../Muonroi.Pdf.Governance/) — CSS policy implementations (`LegacyPrintPolicy`, `DefaultStrictPolicy`) and the cascade engine
- [`Muonroi.Pdf.SourceGenerators`](../Muonroi.Pdf.SourceGenerators/) — Compile-time source generator that populates `IMPdfRenderer<TModel>` from `[PdfTemplate]`-annotated model types
- [`Muonroi.Pdf.Enterprise`](../Muonroi.Pdf.Enterprise/) — Enterprise features: HTTP template registry, polling hot-reload transport, control-plane audit integration

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
