# Muonroi.Templating.Scriban

> Scriban-powered implementation of the Muonroi Templating Engine. Renders templates safely and efficiently using the Scriban engine.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Templating.Scriban.svg)](https://www.nuget.org/packages/Muonroi.Templating.Scriban/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package provides `ScribanTemplateEngine`, a concrete implementation of `ITemplateEngine` from `Muonroi.Templating.Abstractions`. It utilizes the highly performant [Scriban](https://github.com/scriban/scriban) text templating language for evaluating complex templates, supporting custom function providers and advanced FactBag integrations.

## Installation

```bash
dotnet add package Muonroi.Templating.Scriban --prerelease
```

## Quick Start

Register the Scriban templating services in your DI container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Templating.Scriban;

var builder = WebApplication.CreateBuilder(args);

// Registers ITemplateEngine mapped to ScribanTemplateEngine
builder.Services.AddScribanTemplating();

var app = builder.Build();
```

## Features

- **`ScribanTemplateEngine`**: Implementation of `ITemplateEngine` with full Scriban feature support.
- **FactBag Integration**: Built-in `ScribanFactBagScriptObject` to natively interface with Muonroi `FactBag` dictionaries during rendering.
- **Custom Functions**: Extensible via `IScribanFunctionProvider` for domain-specific functions.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Templating.Abstractions`](../Muonroi.Templating.Abstractions/) — core interfaces and contracts for templating.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
