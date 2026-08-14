# Muonroi.Pdf.Abstractions

> HTML/CSS to PDF rendering contracts and configuration abstractions.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Pdf.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Pdf.Abstractions` package provides the fundamental contracts, interfaces, and data types used across the Muonroi PDF rendering ecosystem. By isolating abstractions from the core engine, it ensures a decoupled architecture allowing developers to replace or mock various parts of the rendering pipeline.

This package is essential for any library or application that wishes to integrate with, configure, or extend the `Muonroi.Pdf` generator without carrying the heavy footprint of the full layout engine.

## Features

- **Core Service Contracts**: Defines `IMPdfService`, the primary entry point for all HTML-to-PDF rendering operations.
- **Rendering Configuration Models**: Provides models for page layout configuration including `PdfRenderOptions`, `PdfPageSize`, `PdfMargins`, and `PdfOrientation`.
- **Policy Definitions**: Contains the `IPdfCssPolicy` contract and `PdfPolicySettings` to enforce security and styling constraints.
- **Engine Extensibility Seams**: Exposes interfaces (`IHtmlParser`, `ICssCascadeEngine`, `IPdfWriter`, `IFontResolver`, `IResourceResolver`) that dictate how the engine fetches resources, computes styles, and writes binary PDF output.
- **Domain Exceptions**: Standardized PDF exception types like `PdfPolicyException` and `PdfInputLimitException` for consistent error handling.

## Installation

```bash
dotnet add package Muonroi.Pdf.Abstractions
```

## Quick Start

You will typically use these abstractions when configuring options for the main engine or when injecting the service interface into your classes.

```csharp
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Exceptions;

public class InvoiceGenerator
{
    private readonly IMPdfService _pdfService;

    public InvoiceGenerator(IMPdfService pdfService)
    {
        _pdfService = pdfService;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceHtml)
    {
        PdfRenderOptions options = new PdfRenderOptions
        {
            PageSize = PdfPageSize.Letter,
            Orientation = PdfOrientation.Portrait,
            Margins = PdfMargins.Uniform(20),
            Header = new PdfHeaderFooter(
                RightHtml: "Invoice #12345",
                HeightMm: 15,
                ShowLine: true)
        };

        using var memoryStream = new MemoryStream();

        try 
        {
            // The actual service implementation is provided by Muonroi.Pdf
            PdfRenderResult result = await _pdfService.RenderAsync(invoiceHtml, memoryStream, options);
            return memoryStream.ToArray();
        }
        catch (PdfPolicyException ex)
        {
            Console.WriteLine($"Generation failed due to blocked CSS properties: {ex.Message}");
            throw;
        }
    }
}
```

## Configuration

The `PdfConfigs` class maps to the `PdfConfigs` section in `appsettings.json` and controls systemic behaviors:

```json
{
  "PdfConfigs": {
    "Policy": {
      "AllowModernLayout": true,
      "SoftDegradeUnknownDisplay": true
    },
    "ResourceLimits": {
      "MaxImageSizeInBytes": 10485760,
      "MaxPages": 500
    }
  }
}
```

## Ecosystem Combinations

### + Muonroi.Pdf â†’ Core Engine Binding
Provides the concrete `MPdfService` which implements the `IMPdfService` contract defined here. Swapping implementations requires zero changes to consuming application code.

### + Muonroi.Pdf.Enterprise â†’ Enterprise Capability Wrappers
The Enterprise package provides a decorator over `IMPdfService` that injects commercial features like digital signing and hot-reload, transparently upgrading existing usages of the abstraction.

### + Muonroi.Pdf.SourceGenerators â†’ Type-Safe Rendering
Source generators rely on these abstractions to emit typed renderer interfaces (`IMPdfRenderer<TModel>`) that internally orchestrate `PdfRenderOptions` and `IMPdfService` calls.

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
