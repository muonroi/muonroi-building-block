# Muonroi.Templating.Abstractions
> Abstract definitions and contracts for template rendering engines within the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Templating.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Templating.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Templating.Abstractions` provides the core interfaces required for template rendering within the Muonroi Building Blocks architecture. It abstracts away the underlying templating engine implementation (like Scriban, Liquid, or Razor), allowing developers to rely on standard contracts throughout their codebase without tightly coupling to any specific rendering technology.

This package is designed for library authors building components that require dynamic content generation, such as email dispatchers, document generators, and notification systems, enabling seamless substitution of the templating engine at the application composition root.

By relying on this abstraction, the wider Muonroi ecosystem guarantees that rendering is handled predictably and interchangeably. It promotes the SOLID principles, specifically the Dependency Inversion Principle, where high-level modules (your business logic) do not depend on low-level modules (the rendering engine), but both depend on abstractions.

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Templating.Scriban -> Scriban implements ITemplateEngine; swap without changing calling code
Muonroi.Templating.Scriban provides the robust implementation. Simply swap your DI registration to change engines without touching your application logic.

### + Pdf -> Templating renders HTML templates; Pdf renders the HTML to PDF
By combining with Muonroi.Pdf, you can use ITemplateEngine to construct a dynamic HTML string, then pipe it into the PDF layout engine to generate rich reports.

### + Tenancy -> Per-tenant template resolution: different tenants get different templates
Using Muonroi.Tenancy, you can dynamically select which template string to render based on ITenantContext.CurrentTenantId.

### + Caching -> Template compilation results cached: GetOrSetAsync("tpl:{name}", () => engine.CompileAsync(...))
Integrate Muonroi.Caching.Abstractions to cache the compiled AST of your templates for blazing fast execution.

### Full Stack

```csharp
builder.Services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
builder.Services.AddMPdfService();
builder.Services.AddTenantContext();
```

## Installation

```bash
dotnet add package Muonroi.Templating.Abstractions
```

## Samples
- samples/PdfGeneration/ - HTML to PDF generation using templates.
- samples/MultiTenantSaaS/ - Per-tenant template resolution.


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
