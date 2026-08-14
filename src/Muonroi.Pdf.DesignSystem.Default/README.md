# Muonroi.Pdf.DesignSystem.Default

> Default HTML/CSS design system templates for Muonroi PDF rendering.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.DesignSystem.Default.svg)](https://www.nuget.org/packages/Muonroi.Pdf.DesignSystem.Default/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Pdf.DesignSystem.Default` package provides a set of pre-built, aesthetically pleasing HTML/CSS templates optimized specifically for the `Muonroi.Pdf` layout engine. Since the core PDF engine adheres to a strict subset of CSS for security and determinism, designing documents from scratch that look great and render reliably can take time.

This package ships standardized templatesâ€”such as invoices, receipts, and reporting dashboardsâ€”that are guaranteed to bypass policy constraints and render perfectly using either block-level layout or modern grid/flex paradigms.

## Features

- **Pre-certified Layouts**: All templates are fully compliant with `Muonroi.Pdf.Governance` strict and legacy policies.
- **Invoice Template**: A professional billing template featuring tables, floating summary boxes, and running headers.
- **Receipt Template**: A minimal width point-of-sale receipt layout.
- **Report Template**: Multi-page report formats utilizing `counter(page)` for automated table of contents and footers.
- **Centralized Provider**: Resolves all templates via `DesignSystemTemplateProvider`.

## Installation

```bash
dotnet add package Muonroi.Pdf.DesignSystem.Default
```

## Quick Start

The templates are embedded resources exposed via the `DesignSystemTemplateProvider`.

```csharp
using Muonroi.Pdf.DesignSystem;
using Muonroi.Pdf.Abstractions;
using Muonroi.Templating.Abstractions;

// 1. Retrieve a certified HTML string for an Invoice
string rawTemplate = DesignSystemTemplateProvider.GetTemplate("invoice");

// 2. Hydrate the template with real data (e.g. using a templating engine like Scriban)
var data = new { 
    CompanyName = "ACME Corp",
    InvoiceNumber = "INV-2026-0042",
    Total = "$2,310.00"
};
string hydratedHtml = await templatingService.RenderAsync(rawTemplate, data);

// 3. Render it using the Muonroi PDF Engine
PdfRenderOptions options = new PdfRenderOptions { PageSize = PdfPageSize.A4 };
using var output = File.Create("my_invoice.pdf");
await pdfService.RenderAsync(hydratedHtml, output, options);
```

## Available Templates

### `Invoice`
A standard corporate invoice. It uses block floats or flexbox (if requested) to arrange the header logo and metadata, followed by a well-formatted CSS `table` representing the line items.

### `Receipt`
Designed to be printed on continuous thermal paper widths (e.g., 80mm). Ideal for POS integrations.

### `Report`
A formal document structure featuring title pages, repeating header banners, and page counters on footers. Optimized for multi-page iteration.

## Configuration

If you invoke templates that use modern constructs, make sure that your main `Muonroi.Pdf` host is configured to allow flexbox and grid:

```json
{
  "PdfConfigs": {
    "Policy": {
      "AllowModernLayout": true
    }
  }
}
```
Otherwise, the engine will block the CSS directives and degrade gracefully or throw a `PdfPolicyException`.

## API Reference

### `DesignSystemTemplateProvider`
A static utility class that resolves and parses the embedded templates into ready-to-render strings.
- `GetTemplate(string name)`: Returns the raw HTML string for the named template ("invoice", "receipt", "report"). The HTML string contains `{{TokenName}}` placeholders.

## Ecosystem Combinations

### + Muonroi.Pdf â†’ Certified Generation
`DesignSystemTemplateProvider` provides the raw HTML payloads which the `MPdfService` natively renders into high-quality PDFs without any CSS layout issues.

### + Muonroi.Tenancy.Core â†’ Per-Tenant Customizations
Tenants can provide their own localized variations of `invoice.html` which are intercepted and served instead of the embedded defaults.

### + Muonroi.Templating.Scriban â†’ Dynamic Variable Binding
`GetTemplate("invoice")` returns HTML containing `{{TokenName}}` placeholders. By passing this template to the Scriban templating engine, you can dynamically hydrate the layout with your concrete domain data before passing it to the PDF renderer.

### Full PDF Production Stack
```csharp
builder.Services
    .AddMPdfService(config)             
    .AddPdfGovernance(config)           
    .AddPdfDesignSystem(config);        
```

## Samples
- [`Muonroi.Pdf.Samples`](../../samples/Muonroi.Pdf.Samples)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
