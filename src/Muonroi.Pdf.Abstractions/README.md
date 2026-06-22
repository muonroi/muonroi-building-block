# Muonroi.Pdf.Abstractions

> Contracts-only package for the Muonroi HTML/CSS-to-PDF pipeline — the shared interfaces, options, and exceptions that both the OSS engine and your application code depend on.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Pdf.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships the stable contracts (`IMPdfService`, `IMPdfRenderer<TModel>`, `IPdfCssPolicy`, and the low-level adapter seams) that the rendering engine is built on. It carries no runtime rendering behavior — it exists so that application code, custom policies, and the source-generator companion can all depend on the same interface assembly without taking a transitive dependency on the full engine.

To actually render PDFs, depend on [`Muonroi.Pdf`](../Muonroi.Pdf/) and call `AddPdf()`. Add [`Muonroi.Pdf.Governance`](../Muonroi.Pdf.Governance/) for the built-in CSS policy implementations, or [`Muonroi.Pdf.Enterprise`](../Muonroi.Pdf.Enterprise/) for the enterprise extensions.

## Installation

```bash
dotnet add package Muonroi.Pdf.Abstractions --prerelease
```

## Quick Start

This package is contracts-only — there is no `AddXxx` registration here. The typical usage pattern is:

**1. Consume `IMPdfService` (registered by `Muonroi.Pdf`)**

```csharp
using Muonroi.Pdf.Abstractions;

// IMPdfService is registered by Muonroi.Pdf's AddPdf() extension.
// Inject it wherever you need to render HTML to PDF.
public class ReportService(IMPdfService pdf)
{
    public async Task<byte[]> GenerateAsync(string html, CancellationToken ct = default)
    {
        var (bytes, result) = await pdf.RenderToBytesAsync(
            html,
            new PdfRenderOptions(),
            ct);

        Console.WriteLine($"Pages: {result.PageCount}  Bytes: {result.ByteCount}");
        return bytes;
    }
}
```

**2. Stream directly to a file (recommended for production — avoids buffering)**

```csharp
await using FileStream output = File.Create("report.pdf");
PdfRenderResult result = await pdf.RenderAsync(html, output, new PdfRenderOptions());
```

**3. Implement a custom CSS policy**

```csharp
using Muonroi.Pdf.Abstractions.Policy;

public sealed class MyPolicy : IPdfCssPolicy
{
    public string Id => "my-policy-v1";
    public PdfPolicyLimits Limits => new();

    public ValueTask<PolicyValidationResult> ValidateAsync(
        IPdfDocumentContext context,
        CancellationToken cancellationToken = default)
    {
        // Inspect context.ElementCount, context.MaxDepth, etc.
        // Return PolicyValidationResult.Accept() or PolicyValidationResult.Reject(violations).
        return ValueTask.FromResult(PolicyValidationResult.Accept());
    }
}
```

See [`Muonroi.Pdf.Samples`](../../samples/Muonroi.Pdf.Samples/) for a full working host using `AddPdf()` and all rendering scenarios.

## Features

- **`IMPdfService`** — primary rendering contract: stream-out `RenderAsync`, multi-page `RenderMultiPageAsync`, and buffer `RenderToBytesAsync`
- **`IMPdfRenderer<TModel>` / `IMPdfRendererFactory`** — strongly-typed per-template renderer seam; populated at compile time by `Muonroi.Pdf.SourceGenerators`
- **`IPdfCssPolicy` / `IPdfDocumentContext`** — extensible CSS policy gate: implement to enforce custom HTML/CSS subset rules before layout begins
- **`IFontResolver`** — bytes-only font resolution contract (path-traversal-safe); implement for custom font stores
- **`IResourceResolver`** — resolves external resources (images, stylesheets) during rendering
- **`PdfConfigs`** — `IConfiguration`-bound options class covering input limits, font resolver, and policy tunables; bound from the `"PdfConfigs"` section
- **`PdfTemplateAttribute`** — compile-time marker for source-generator–driven renderer emission
- **Engine adapter seams** — `IHtmlParser`, `ICssCascadeEngine`, `IPdfWriter`, `IImageDecoder`, and related interfaces for swapping engine internals
- **Structured exception hierarchy** — `PdfException` (base), `PdfFormatException`, `PdfPolicyException`, `PdfSecurityException`, `PdfInputLimitException`
- Targets `netstandard2.0`; polyfills `ReadOnlyMemory<T>` and `ValueTask<T>` for broad host compatibility

## Configuration

`PdfConfigs` is the single `IConfiguration`-bound options class. Bind it via the implementation package's `AddPdf()` call — no direct binding is needed here.

`appsettings.json` shape:

```json
{
  "PdfConfigs": {
    "RequirePolicySignature": false,
    "Limits": {
      "MaxHtmlBytes": 8388608,
      "MaxDomDepth": 256,
      "MaxElementCount": 100000,
      "MaxImagePixels": 25000000,
      "MaxPages": 1000,
      "MaxRenderDurationMs": 15000,
      "MaxFontFiles": 32
    },
    "FontResolver": {
      "FallbackToFirstRegistered": true,
      "GenericFamilyMap": {
        "sans-serif": "Arial",
        "serif": "Times New Roman",
        "monospace": "Courier New"
      },
      "Fonts": [
        { "Family": "Arial", "Path": "fonts/arial.ttf", "Weight": 400, "Style": "Normal" }
      ]
    },
    "Policy": {
      "SoftDegradeUnknownDisplay": false,
      "AllowModernLayout": false
    }
  }
}
```

`AllowModernLayout: true` enables real CSS Flexbox and CSS Grid layout engines (opt-in, default off). `SoftDegradeUnknownDisplay: true` downgrades unsupported `display` values to `block` instead of aborting with `PdfPolicyException`.

## API Reference

| Type | Purpose |
|------|---------|
| `IMPdfService` | Primary rendering service: `RenderAsync`, `RenderMultiPageAsync`, `RenderToBytesAsync` |
| `IMPdfRenderer<TModel>` | Strongly-typed template renderer; `TemplateId` + `RenderAsync(model, stream, options, ct)` |
| `IMPdfRendererFactory` | Resolves renderers by template id: `Get<TModel>`, `TryGet<TModel>` |
| `IPdfCssPolicy` | CSS policy gate: `Id`, `Limits`, `ValidateAsync(documentContext, ct)` |
| `IPdfDocumentContext` | Opaque document context passed to policies: `ElementCount`, `MaxDepth`, `TotalStylesheetBytes`, `SourceHtmlBytes` |
| `IFontResolver` | Font bytes resolution: `ResolveAsync(FontRequest, ct)` |
| `IResourceResolver` | External resource resolution during rendering |
| `IHtmlParser` | HTML parsing adapter seam |
| `ICssCascadeEngine` | CSS cascade adapter seam |
| `IPdfWriter` | Low-level PDF byte-writing adapter seam |
| `IImageDecoder` | Image decoding adapter seam |
| `PdfConfigs` | `IConfiguration`-bound options; section name `"PdfConfigs"` |
| `PdfConfigs.PdfLimits` | Input and rendering resource limits |
| `PdfFontResolverConfig` | Font registration list and generic-family map |
| `PdfPolicySettings` | Policy tunables: `AllowModernLayout`, `SoftDegradeUnknownDisplay` |
| `PdfTemplateAttribute` | Compile-time attribute for source-generator renderer emission |
| `FontRequest` | Font selection record: `Family`, `Weight`, `Style` |
| `FontWeight` | Enum: `Thin`(100) through `Black`(900) |
| `FontStyle` | Enum: `Normal`, `Italic`, `Oblique` |
| `PdfException` | Base exception: `RuleId`, `Detail` |
| `PdfFormatException` | Malformed HTML/CSS input |
| `PdfPolicyException` | CSS policy gate rejection (carries `IReadOnlyList<PolicyViolation>`) |
| `PdfSecurityException` | Security rule violation |
| `PdfInputLimitException` | Input exceeded a configured `PdfLimits` threshold |

## Samples

- [Muonroi.Pdf.Samples](../../samples/Muonroi.Pdf.Samples/) — full Generic Host wiring with `AddPdf()`, all rendering scenarios (minimal, invoice, header/footer, watermark, Flexbox, CSS Grid, multi-page, policy rejection)
- [Muonroi.Pdf.AotSample](../../samples/Muonroi.Pdf.AotSample/) — AOT-compatible setup with a pre-registered `IFontResolver`

## Compatibility

- Target framework: `netstandard2.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Pdf`](../Muonroi.Pdf/) — OSS rendering engine; registers `IMPdfService` via `AddPdf()`
- [`Muonroi.Pdf.Governance`](../Muonroi.Pdf.Governance/) — built-in `IPdfCssPolicy` implementations (`LegacyPrintPolicy`, `DefaultStrictPolicy`)
- [`Muonroi.Pdf.Enterprise`](../Muonroi.Pdf.Enterprise/) — enterprise extensions (template registry, audit, compliance)
- [`Muonroi.Pdf.SourceGenerators`](../Muonroi.Pdf.SourceGenerators/) — compile-time `IMPdfRenderer<TModel>` generation from `[PdfTemplate]`-decorated model classes
- [`Muonroi.Pdf.DesignSystem.Default`](../Muonroi.Pdf.DesignSystem.Default/) — default CSS design tokens for the rendering pipeline

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) in the repository root.
