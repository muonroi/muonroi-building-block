# Muonroi.Pdf

> HTML/CSS to PDF layout engine: box-tree construction, pagination, and rendering coordination.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.svg)](https://www.nuget.org/packages/Muonroi.Pdf/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Pdf` package is the core layout and rendering engine for converting HTML/CSS into portable PDF documents. Built entirely in managed C# without reliance on native browser binaries (like Puppeteer or wkhtmltopdf), it offers high-performance, deterministic PDF generation ideal for server-side environments.

The library implements a full CSS box-tree layout model via `MPdfService`, handling complex scenarios such as tables, floating elements, multi-page pagination, running headers/footers, and true inline text formatting. With recent additions, it also fully supports modern CSS layout paradigms like Flexbox and CSS Grid natively, enabled via an opt-in policy flag.

## Features

- **Managed Layout Engine**: Implements the CSS box model natively in C#. Parses HTML/CSS and builds a layout tree composed of block, inline, and table boxes.
- **Modern Layouts (Flexbox & Grid)**: True CSS Flexbox and CSS Grid layout algorithms (opt-in). Supports flex directions, wrapping, grid tracks, repeating tracks, and fractional (`fr`) units.
- **Pagination & Page Breaks**: Intelligent pagination that prevents awkward breaks inside tables or paragraphs when possible. Support for `page-break-before`, `page-break-after`, and `page-break-inside`.
- **Advanced Text Layout**: Real text measurement using embedded TrueType fonts, supporting line breaking, alignment, and justification.
- **Running Headers/Footers**: Dynamic headers and footers with programmatic page numbering (`counter(page)`).
- **Graphics & Colors**: Linear and radial gradients, image rendering (data URIs, PNGs, JPEGs), borders, and rounded corners.

## Installation

```bash
dotnet add package Muonroi.Pdf
```

## Quick Start

The following example demonstrates how to set up the engine, enable modern layouts, and generate a PDF with CSS Grid and Flexbox.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Extensions;

// 1. Setup DI container and configuration
HostApplicationBuilder builder = Host.CreateApplicationBuilder();

// Enable modern layouts (Flexbox/Grid) via configuration
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["PdfConfigs:Policy:AllowModernLayout"] = "true"
});

builder.Services.AddMPdfService(builder.Configuration);
using IHost host = builder.Build();

// 2. Retrieve the PDF service
IMPdfService pdfService = host.Services.GetRequiredService<IMPdfService>();

// 3. Define HTML content with modern layout
string html = """
<!DOCTYPE html>
<html><head><style>
    body { font-family: Arial, sans-serif; padding: 16px; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .card { display: flex; flex-direction: column; border: 1px solid #ccc; padding: 12px; }
    .card h3 { color: #0c6b6b; margin-top: 0; }
</style></head>
<body>
    <h1>Business Dashboard</h1>
    <div class="grid">
        <div class="card">
            <h3>Revenue</h3>
            <p>$2.64M</p>
        </div>
        <div class="card">
            <h3>Growth</h3>
            <p>12.4%</p>
        </div>
    </div>
</body></html>
""";

// 4. Render to a stream
string outPath = Path.Combine(AppContext.BaseDirectory, "dashboard.pdf");
using FileStream outputStream = File.Create(outPath);

PdfRenderOptions options = new PdfRenderOptions
{
    PageSize = PdfPageSize.A4,
    Margins = PdfMargins.Uniform(15)
};

PdfRenderResult result = await pdfService.RenderAsync(html, outputStream, options);
Console.WriteLine($"Generated PDF: {result.PageCount} pages, {result.ByteCount} bytes");
```

## Configuration

Configuration is managed via `PdfConfigs` bound to the `PdfConfigs` configuration section.

Key settings under `PdfConfigs:Policy`:
- `AllowModernLayout` (bool, default `false`): Enables parsing and rendering of `display: flex` and `display: grid`. When `false`, these properties fall back to block or are stripped by the policy gate.

## API Reference

### `IMPdfService`
The primary facade for rendering documents. Implemented by `MPdfService`.
- `Task<PdfRenderResult> RenderAsync(string html, Stream output, PdfRenderOptions options, CancellationToken ct)`: Renders a single HTML document to the output stream.

### `PdfRenderOptions`
Defines layout boundaries.
- `PageSize`: Standard sizes (`A4`, `Letter`, `Legal`).
- `Orientation`: `Portrait` or `Landscape`.
- `Margins`: Defines top, right, bottom, and left margins.

## Ecosystem Combinations

### + Muonroi.Pdf.Governance â†’ Secured PDF Generation Pipeline
When combined with Governance, the PDF engine can sanitize all user-supplied CSS payloads through `LegacyPrintPolicy`, stripping out potentially dangerous expressions or unsupported properties before they reach `MPdfService`, ensuring deterministic layout boundaries.

### + Muonroi.Pdf.DesignSystem.Default â†’ Pre-built Document Templating
Combines the raw engine power with out-of-the-box styled templates. Instead of hand-writing HTML layouts, you can supply your data model to a Design System template which dynamically generates the HTML payload for `IMPdfService`.

### Full PDF Production Stack
```csharp
builder.Services
    .AddMPdfService(config)
    .AddPdfGovernance(config)
    .AddPdfDesignSystem(config)
    .AddMuonroiObservability(config);
```

## Samples
- [`Muonroi.Pdf.Samples`](../../samples/Muonroi.Pdf.Samples)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
