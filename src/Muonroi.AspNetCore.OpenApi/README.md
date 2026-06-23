# Muonroi.AspNetCore.OpenApi

> Swashbuckle operation filters that add standardized error-response documentation and clean up parameter defaults for every endpoint in your Muonroi ASP.NET Core API.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AspNetCore.OpenApi.svg)](https://www.nuget.org/packages/Muonroi.AspNetCore.OpenApi/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

Muonroi APIs return a consistent `MErrorResponse` shape for validation failures and unhandled exceptions. Without extra setup, Swashbuckle omits those responses from generated OpenAPI documents and leaves parameter default values blank. This package ships two `IOperationFilter` implementations that correct both gaps automatically at Swagger-gen time — no per-endpoint attributes required.

## Installation

```bash
dotnet add package Muonroi.AspNetCore.OpenApi --prerelease
```

## Quick Start

Register both filters inside `AddSwaggerGen`. `SwaggerDefaultValues` depends on `IMJsonSerializeService`; register its implementation from `Muonroi.Core` first.

```csharp
using Muonroi.AspNetCore.OpenApi.OpenApi;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;

// Concrete IMJsonSerializeService lives in Muonroi.Core.
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "My API",
        Version = "v1"
    });

    // Auto-document 400 + 500 MErrorResponse on every endpoint.
    options.OperationFilter<MErrorResponseFilter>();

    // Fill parameter defaults/descriptions and prune unsupported content types.
    options.OperationFilter<SwaggerDefaultValues>();
});

// ...
app.UseSwagger();
app.UseSwaggerUI();
```

## Features

- **Automatic 400/500 documentation** — `MErrorResponseFilter` adds `Bad Request` and `Internal Server Error` response entries (with the `MErrorResponse` schema) to every operation that does not already declare them.
- **Parameter default propagation** — `SwaggerDefaultValues` reads `ApiParameterDescription.DefaultValue` via the ASP.NET Core API explorer and serializes it into the OpenAPI schema using `IMJsonSerializeService`.
- **Content-type pruning** — removes content types from response entries that the endpoint's formatters do not actually support, keeping the generated document accurate.
- **Parameter description fill-in** — copies `ModelMetadata.Description` to the OpenAPI parameter description when none is set explicitly.
- **Required flag enforcement** — marks parameters as required when `ApiParameterDescription.IsRequired` is true.

## Configuration

There are no options classes or `appsettings` keys. Registration is entirely through Swashbuckle's `SwaggerGenOptions`:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<MErrorResponseFilter>();
    options.OperationFilter<SwaggerDefaultValues>();
});
```

`SwaggerDefaultValues` requires `IMJsonSerializeService` to be registered in the DI container. The concrete `MJsonSerializeService` is in `Muonroi.Core`.

## API Reference

| Type | Purpose |
|------|---------|
| `MErrorResponseFilter` | `IOperationFilter` — appends 400 and 500 `MErrorResponse` entries to operations that lack them |
| `SwaggerDefaultValues` | `IOperationFilter` — propagates parameter defaults/descriptions and prunes unsupported content types; depends on `IMJsonSerializeService` |

## Samples

- [Quickstart.OpenApi](../../samples/Quickstart.OpenApi/) — minimal ASP.NET Core API demonstrating both filters with a catalog controller

## Compatibility

- Target framework: net8.0
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `IMJsonSerializeService` consumed by `SwaggerDefaultValues`
- [`Muonroi.Core`](../Muonroi.Core/) — provides the `MJsonSerializeService` implementation and `MErrorResponse` type
- [`Muonroi.Mediator`](../Muonroi.Mediator/) — mediator integration used alongside this package in API projects

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
