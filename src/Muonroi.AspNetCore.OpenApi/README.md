# Muonroi.AspNetCore.OpenApi
> Core primitives for Muonroi.AspNetCore.OpenApi in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AspNetCore.OpenApi.svg)](https://www.nuget.org/packages/Muonroi.AspNetCore.OpenApi/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.AspNetCore.OpenApi enriches OpenAPI/Swagger generation for Muonroi applications. It provides standard filters such as `MErrorResponseFilter` and configures `SwaggerDefaultValues` for consistent API documentation.

## Features

- **Error Response Standardization**: Automatically maps domain errors using `MErrorResponseFilter`.
- **Swagger Defaults**: Preconfigures robust Swagger definitions via `SwaggerDefaultValues`.

## Quick Start

```csharp
using Muonroi.AspNetCore.OpenApi;

builder.Services.AddSwaggerGen(c => {
    c.OperationFilter<MErrorResponseFilter>();
});
```

## Installation

```bash
dotnet add package Muonroi.AspNetCore.OpenApi
```

## Ecosystem Combinations

Combine with `Muonroi.AspNetCore` to automatically expose standard HTTP problem details schemas in the generated Swagger UI for all `MControllerBase` endpoints.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.AspNetCore.OpenApi components.
