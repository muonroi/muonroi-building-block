# Muonroi.Bff
> Core primitives for Muonroi.Bff in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Bff.svg)](https://www.nuget.org/packages/Muonroi.Bff/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.Bff enables the Backend-for-Frontend (BFF) security pattern for modern SPAs. It provides abstractions like `ITokenStore` and implementations such as `RedisTokenStore` and `InMemoryTokenStore` to securely manage OAuth tokens server-side.

## Features

- **Token Storage Abstractions**: `ITokenStore` defines how access and refresh tokens are securely stored.
- **Distributed Token Store**: `RedisTokenStore` provides highly-available token persistence.
- **Seamless Extensions**: Configure cookie schemes easily using `BffAuthenticationExtensions`.

## Quick Start

```csharp
using Muonroi.Bff;

builder.Services.AddBffAuthentication();
```

## Installation

```bash
dotnet add package Muonroi.Bff
```

## Ecosystem Combinations

Combine with `Muonroi.Auth` to seamlessly intercept and store JWT tokens in Redis via `RedisTokenStore`, ensuring that the browser only ever sees encrypted HTTP-only cookies.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.Bff components.
