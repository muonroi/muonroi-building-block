# Muonroi.Pdf.SourceGenerators

> Source generator for Muonroi.Pdf: compile-time IMPdfRenderer<TModel> emission.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Pdf.SourceGenerators.svg)](https://www.nuget.org/packages/Muonroi.Pdf.SourceGenerators/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Pdf.SourceGenerators` package is a Roslyn-based C# Source Generator designed to boost performance and improve the developer experience when rendering PDFs from strongly-typed models. 

By decorating a standard C# record or class with the `[PdfTemplate]` attribute, this generator automatically analyzes the type at compile time and emits a highly optimized implementation of `IMPdfRenderer<TModel>`. This generated class avoids runtime reflection, safely escapes strings for HTML injection, and constructs the DOM tree efficiently.

## Features

- **Compile-Time Code Generation**: Generates `IMPdfRenderer<T>` implementations during the build process, catching template errors before runtime.
- **Zero-Reflection Data Binding**: Directly maps properties of your C# objects into HTML templates without `PropertyInfo.GetValue` overhead.
- **Auto-Escaping**: Automatically HTML-encodes strings to prevent injection vulnerabilities (XSS) in generated PDFs.
- **Diagnostics Validation**: Emits build errors if templates contain invalid variable names.

## Installation

Because this is a source generator, you should reference it with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"` to prevent it from bleeding into your runtime dependencies.

```bash
dotnet add package Muonroi.Pdf.SourceGenerators
```

In your `.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Muonroi.Pdf.SourceGenerators" 
                    Version="1.0.0" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
  <PackageReference Include="Muonroi.Pdf.Abstractions" Version="1.0.0" />
</ItemGroup>
```

## Quick Start

1. **Define a Model**: Create a C# class or record and decorate it with `[PdfTemplate]`. Supply the HTML layout as a string argument or reference an external file.

```csharp
using Muonroi.Pdf.Abstractions;

namespace MyApp.Invoicing;

[PdfTemplate("""
    <!DOCTYPE html>
    <html>
      <head><style>body { font-family: Arial; }</style></head>
      <body>
        <h1>Invoice for {{CustomerName}}</h1>
        <p>Amount Due: {{AmountDue}}</p>
      </body>
    </html>
""")]
public partial record InvoiceModel(string CustomerName, decimal AmountDue);
```

2. **Build the Project**: Upon building, `PdfTemplateGenerator` generates an accompanying `InvoiceModelPdfRenderer` class.

3. **Use the Generated Renderer**:

```csharp
public class InvoiceService
{
    private readonly IMPdfRenderer<InvoiceModel> _renderer;

    public InvoiceService(IMPdfRenderer<InvoiceModel> renderer)
    {
        _renderer = renderer;
    }

    public async Task SendInvoiceAsync(Stream output)
    {
        var model = new InvoiceModel("Jane Doe", 450.00m);
        
        await _renderer.RenderAsync(model, output);
    }
}
```

## How It Works

`PdfTemplateGenerator` leverages the `IIncrementalGenerator` interface.
1. **Filtering**: It scans the syntax tree for classes/records possessing the `PdfTemplateAttribute`.
2. **Analysis**: It extracts the semantic model, resolving property names and their types.
3. **Template Parsing**: It reads the provided HTML template.
4. **Code Emission**: It generates a C# class named `{ModelName}PdfRenderer` which implements `IMPdfRenderer<T>`.

## API Reference

### `[PdfTemplateAttribute]`
Defined in `Muonroi.Pdf.Abstractions`, but analyzed here.
- `Template`: The raw HTML string.
- `FilePath`: A relative path to an HTML file (analyzed via MSBuild `AdditionalFiles`).

### `PdfTemplateGenerator`
The internal `IIncrementalGenerator` implementation.

## Ecosystem Combinations

### + Muonroi.Pdf â†’ Optimized Engine Integration
The generated `IMPdfRenderer<TModel>` implementation internally calls `MPdfService.RenderAsync()`, ensuring that the strongly-typed workflow enjoys the same layout and pagination capabilities of the core engine.

### + Muonroi.Pdf.DesignSystem.Default â†’ Type-Safe Standard Templates
Apply `[PdfTemplate(FilePath = "Invoice.html")]` pointing to the embedded resources exported by the DesignSystem to instantly gain a strongly-typed `IMPdfRenderer<InvoiceModel>` that renders certified design templates.

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
