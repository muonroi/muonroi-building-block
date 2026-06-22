# Muonroi.ServiceDiscovery.Consul

> Consul-backed service registration and discovery for ASP.NET Core, with safe no-op behaviour in Development environments.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.ServiceDiscovery.Consul.svg)](https://www.nuget.org/packages/Muonroi.ServiceDiscovery.Consul/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

This package integrates HashiCorp Consul into any ASP.NET Core service via two extension methods: `AddServiceDiscovery` wires configuration and registers the Consul client, and `UseServiceDiscovery` registers the service instance with the Consul agent at startup and deregisters it on shutdown. Both calls are no-ops in the `Development` environment, so the package can be included in every environment without special build flags.

## Installation

```bash
dotnet add package Muonroi.ServiceDiscovery.Consul --prerelease
```

## Quick Start

```csharp
using Muonroi.ServiceDiscovery.Consul.Consul;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Binds "ConsulConfigs" from appsettings and registers IConsulClient
// (skipped in Development or when ServiceName / ConsulAddress are absent).
builder.Services.AddServiceDiscovery(builder.Configuration, builder.Environment);

builder.Services.AddControllers();

WebApplication app = builder.Build();

// Registers this instance with the Consul agent; deregisters on shutdown.
// No-op in Development / when IConsulClient is not in DI.
app.UseServiceDiscovery(app.Environment);

app.MapControllers();
app.Run();
```

`appsettings.json`:

```json
{
  "ConsulConfigs": {
    "Enable": true,
    "UseDiscovery": true,
    "ServiceName": "my-service",
    "ConsulAddress": "http://localhost:8500",
    "ServiceAddress": "",
    "ServicePort": 0,
    "ServiceMetadata": {
      "version": "1.0.0"
    }
  }
}
```

Leave `ServiceAddress` blank and `ServicePort` at `0` to let the package derive the address and port from the server's listening address at startup.

Set `ASPNETCORE_ENVIRONMENT=Production` (or any non-Development value) and a reachable `ConsulAddress` to actually register with Consul.

## Features

- Binds `ConsulConfigs` from configuration and registers it as a singleton — always available in DI even when discovery is disabled.
- Registers `IConsulClient` only when `Enable`, `UseDiscovery`, and both `ServiceName` and `ConsulAddress` are set, and the environment is not Development.
- `UseServiceDiscovery` deregisters then re-registers the service instance with the Consul agent at application start, and schedules deregistration on `IHostApplicationLifetime.ApplicationStopping`.
- Auto-derives `ServiceAddress` and `ServicePort` from `IServerAddressesFeature` when not explicitly configured.
- Throws `MConfigurationException` with a precise configuration key when address or port cannot be determined, preventing silent misconfiguration.
- Both extension methods short-circuit safely in the Development environment — no Consul agent required for local development.

## Configuration

### DI Registration

```csharp
// Program.cs
builder.Services.AddServiceDiscovery(builder.Configuration, builder.Environment);

// ...

app.UseServiceDiscovery(app.Environment);
```

### `ConsulConfigs` Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enable` | `bool` | `true` | Master switch for the Consul integration. |
| `UseDiscovery` | `bool` | `true` | When `false`, the Consul client is not registered and the service is not registered with Consul. |
| `Id` | `string?` | `null` | Optional custom service instance ID. A unique suffix is appended at runtime. |
| `ServiceName` | `string?` | `null` | Required. Consul service name. |
| `ConsulAddress` | `string?` | `null` | Required. Consul agent address (e.g. `http://localhost:8500`). |
| `ServiceAddress` | `string?` | `null` | Address advertised to Consul. Derived from the server listener when blank. |
| `ServicePort` | `int` | `0` | Port advertised to Consul. Derived from the server listener when `0`. |
| `ServiceMetadata` | `Dictionary<string,string>?` | `null` | Optional key-value metadata attached to the Consul registration. |

Configuration section name: `ConsulConfigs` (value of `ConsulConfigs.SectionName`).

## API Reference

| Type | Purpose |
|------|---------|
| `ConsulHandler` | Static class that provides `AddServiceDiscovery` and `UseServiceDiscovery` / `UseServiceDiscoveryAsync` extension methods. |
| `ConsulConfigs` | POCO bound from the `ConsulConfigs` configuration section; registered as a singleton by `AddServiceDiscovery`. |

## Samples

- [Quickstart.ServiceDiscovery](../../samples/Quickstart.ServiceDiscovery/) — End-to-end sample showing registration, configuration binding, and a controller that exposes the resolved `ConsulConfigs` and reports whether `IConsulClient` was wired.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — Core exception types used by this package (e.g. `MConfigurationException`).
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — Structured logging abstraction (`IMLog<T>`) used internally by the middleware.

## License

This package is distributed under a **Commercial license**. A valid Muonroi license is required for production use. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL) for terms.
