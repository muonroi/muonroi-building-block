# Muonroi.Templating.Abstractions

> Abstractions for the Muonroi Templating Engine. Defines standard contracts for template rendering across the ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Templating.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Templating.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package contains the core abstractions used for rendering templates within the Muonroi ecosystem. It allows services to depend on an `ITemplateEngine` interface to render templates asynchronously or synchronously without coupling to a specific rendering engine (like Scriban or Liquid).

## Installation

```bash
dotnet add package Muonroi.Templating.Abstractions --prerelease
```

## Features

- **`ITemplateEngine` Contract**: Provides `Render` and `RenderAsync` methods to evaluate templates against a dictionary of variables.
- **Provider Agnostic**: Enables swapping underlying templating technologies without altering business logic.

## Usage

Inject `ITemplateEngine` into your services to process templates:

```csharp
using Muonroi.Templating.Abstractions;

public class NotificationService(ITemplateEngine templateEngine)
{
    public async Task<string> GenerateMessageAsync(string template, IDictionary<string, object?> context)
    {
        return await templateEngine.RenderAsync(template, context);
    }
}
```

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Templating.Scriban`](../Muonroi.Templating.Scriban/) — concrete implementation of `ITemplateEngine` powered by Scriban.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
