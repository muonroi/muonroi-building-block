# Muonroi.Secrets

> Lightweight secret-access abstraction backed by `IConfiguration` — lets services consume named secrets without coupling to a concrete vault provider.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Secrets.svg)](https://www.nuget.org/packages/Muonroi.Secrets/)
[![Commercial License](https://img.shields.io/badge/commercial-required-blue.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-COMMERCIAL)

`Muonroi.Secrets` provides `ISecretProvider`, a single-method contract for retrieving named secrets, together with `ConfigurationSecretProvider` — a built-in implementation that reads values straight from the .NET `IConfiguration` pipeline. Any configuration source supported by .NET (environment variables, Azure Key Vault, AWS Secrets Manager, `appsettings.json`) feeds the provider automatically; no custom plumbing is required for the common case.

## Installation

```bash
dotnet add package Muonroi.Secrets --prerelease
```

## Quick Start

Register `ConfigurationSecretProvider` as the `ISecretProvider` implementation:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Secrets.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Wire the built-in configuration-backed provider
builder.Services.AddSingleton<ISecretProvider>(sp =>
    new ConfigurationSecretProvider(sp.GetRequiredService<IConfiguration>()));

var app = builder.Build();
```

Consume the secret in any service:

```csharp
public class OrderService(ISecretProvider secrets)
{
    public void Process()
    {
        string? apiKey = secrets.GetSecret("ExternalPayment:ApiKey");
        // apiKey comes from whatever IConfiguration sources are registered
    }
}
```

## Features

- **Provider abstraction** — depend on `ISecretProvider`, swap the backing store without touching consumer code.
- **Configuration-backed default** — `ConfigurationSecretProvider` reads from `IConfiguration[name]`, covering all standard .NET configuration sources out of the box.
- **Zero added dependencies** — only `Microsoft.Extensions.Configuration.Abstractions` is required at runtime.

## Configuration

No extension method is shipped; register the provider directly:

```csharp
// Option A — singleton backed by IConfiguration (most common)
services.AddSingleton<ISecretProvider>(sp =>
    new ConfigurationSecretProvider(sp.GetRequiredService<IConfiguration>()));

// Option B — replace with a custom vault-backed implementation
services.AddSingleton<ISecretProvider, MyVaultSecretProvider>();
```

Secret keys follow standard `IConfiguration` key syntax:

```json
{
  "ExternalPayment": {
    "ApiKey": "sk-live-..."
  }
}
```

```csharp
// Reads "ExternalPayment:ApiKey" using colon-delimited hierarchy
string? key = secrets.GetSecret("ExternalPayment:ApiKey");
```

## API Reference

| Type | Purpose |
|------|---------|
| `ISecretProvider` | Contract: `string? GetSecret(string name)` — retrieve a secret by key |
| `ConfigurationSecretProvider` | `ISecretProvider` backed by `IConfiguration`; constructed with an `IConfiguration` instance |

## Samples

No dedicated sample project exists for this package. See the Quick Start snippet above for a minimal integration.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation (see `LICENSE-COMMERCIAL`)

## Related Packages

- [`Muonroi.BuildingBlock.All`](../Muonroi.BuildingBlock.All/) — meta-package that bundles all commercial extensions, including `Muonroi.Secrets`

## License

This package is distributed under the **Muonroi Commercial License**. A valid commercial license is required to use it in production. See [`LICENSE-COMMERCIAL`](../../LICENSE-COMMERCIAL) for terms.
