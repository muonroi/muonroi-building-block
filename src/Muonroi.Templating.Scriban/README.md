# Muonroi.Templating.Scriban
> High-performance template rendering for the Muonroi ecosystem using the Scriban engine.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Templating.Scriban.svg)](https://www.nuget.org/packages/Muonroi.Templating.Scriban/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Templating.Scriban` provides a concrete implementation of the `Muonroi.Templating.Abstractions.ITemplateEngine` interface using [Scriban](https://github.com/scriban/scriban), a fast, powerful, safe, and lightweight text templating language and engine for .NET.

This package bridges dynamic context models (like deeply nested dictionaries, JSON elements, or arbitrary POCOs) with Scriban's execution environment. It automatically manages deep conversion of these objects into `ScriptObject` forms that Scriban can query efficiently, making it an excellent choice for dynamic document generation, email templating, or integrating text generation with rule engine outputs (e.g., `FactBag`).

## Features

- **Standardized Implementation**: Seamlessly plugs into components requiring `ITemplateEngine`.
- **Automatic Type Conversion**: The custom `ScribanFactBagScriptObject` recursively unpacks:
  - `IDictionary<string, object?>`
  - `System.Text.Json.JsonElement`
  - `IReadOnlyDictionary` and nested lists/arrays
  - Deep POCO graphs (up to 3 levels deep to prevent cycles)
- **Extensibility**: Inject multiple `IScribanFunctionProvider` instances to seamlessly register custom pipeline functions into the templating context.
- **Safety**: Configured with a default cancellation token source (5-second timeout) and a strict `LoopLimit` (10,000) to protect against infinite loops or resource exhaustion in untrusted templates.

## Installation

```bash
dotnet add package Muonroi.Templating.Scriban
```

## Quick Start

### 1. Registering the Engine

Register the Scriban engine in your dependency injection container:

```csharp
using Muonroi.Templating.Abstractions;
using Muonroi.Templating.Scriban;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();

// Optional: Register custom functions
// services.AddSingleton<IScribanFunctionProvider, MyCustomScribanFunctions>();
```

### 2. Using the Engine

Inject `ITemplateEngine` into your service. Scriban's liquid-like syntax is evaluated securely.

```csharp
using Muonroi.Templating.Abstractions;

public class ReportGenerator
{
    private readonly ITemplateEngine _templateEngine;

    public ReportGenerator(ITemplateEngine templateEngine)
    {
        _templateEngine = templateEngine;
    }

    public async Task<string> GenerateReportAsync(CancellationToken ct)
    {
        // Liquid-style Scriban template
        string templateText = @"
        <h1>Weekly Report for {{ user.name }}</h1>
        <p>Total Revenue: {{ revenue | math.round 2 }}</p>
        
        <h2>Active Items:</h2>
        <ul>
        {{ for item in items }}
            <li>{{ item.title }} - {{ item.status }}</li>
        {{ end }}
        </ul>
        ";

        var context = new Dictionary<string, object?>
        {
            ["user"] = new { Name = "Alice" },
            ["revenue"] = 1450.559,
            ["items"] = new[]
            {
                new { Title = "Project A", Status = "Active" },
                new { Title = "Project B", Status = "Pending" }
            }
        };

        // RenderAsync automatically adapts the nested POCOs and arrays
        return await _templateEngine.RenderAsync(templateText, context, ct);
    }
}
```

## Custom Function Providers

To extend the capabilities of the template engine, implement `IScribanFunctionProvider` and register it in the DI container. The engine will automatically pick it up and attach your functions to the global script object.

```csharp
using Scriban.Runtime;
using Muonroi.Templating.Scriban;

public class StringFormattingFunctions : IScribanFunctionProvider
{
    public void Register(ScriptObject scriptObject)
    {
        // Registers a function usable as 'shout "hello"' -> "HELLO!!!"
        scriptObject.Import("shout", new Func<string, string>(text => $"{text.ToUpperInvariant()}!!!"));
    }
}
```

## API Reference

### `ScribanTemplateEngine`
Implementation of `ITemplateEngine`. Instantiated with an optional list of `IScribanFunctionProvider`.

### `IScribanFunctionProvider`
Interface specifying a single `Register(ScriptObject scriptObject)` method, giving developers direct access to inject .NET delegates into the Scriban execution environment.

### `ScribanFactBagScriptObject`
An internal mapping class that bridges standard .NET dictionaries, collections, and POCOs (including `System.Text.Json.JsonElement` instances) seamlessly into Scriban's type system natively at runtime.

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Templating.Abstractions -> Scriban is the ITemplateEngine implementation
Acts as the concrete implementation of the abstractions, allowing seamless DI injection.

### + Pdf -> Render Scriban -> HTML -> Pdf pipeline for PDF generation
Output from Scriban can be fed directly to the Muonroi.Pdf engine to handle layout and rendering.

### + Tenancy -> Template model includes TenantId for tenant-specific branding
Inject the ITenantContext into the template variables to conditionally render logos or theme colors based on the tenant.

### + Pdf.DesignSystem.Default -> Design system templates are Scriban syntax
The default Muonroi PDF design system uses Scriban syntax for its core templates.

### + BackgroundJobs -> Batch email/PDF rendering via background jobs
Pair with Muonroi.BackgroundJobs.Hangfire to batch render templates asynchronously in the background.

### Full Stack
`csharp
// combined registration
builder.Services.AddScribanTemplating();
builder.Services.AddMPdfService();
builder.Services.AddTenantContext();
builder.Services.AddHangfireBackgroundJobs();
`

## Samples
- samples/PdfGeneration/
- samples/BackgroundJobs/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
