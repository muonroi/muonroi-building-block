# Muonroi.Tenancy.Abstractions

> Contracts-only package that defines the multi-tenancy interfaces, options, and models shared across every Muonroi service.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Tenancy.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Tenancy.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships the contracts (`ITenantContext`, `ITenantIdResolver`, `ITenantConnectionStringFactory`, `ITenantScoped`) and configuration types (`MultiTenantOptions`, `TenantConnectionStringsOptions`) that the rest of the Muonroi tenancy stack depends on. It contains **no runtime behavior** — register it as a reference in libraries that need the interfaces; use [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) for the concrete implementations and [`Muonroi.Tenancy`](../Muonroi.Tenancy/) for ASP.NET middleware.

## Installation

```bash
dotnet add package Muonroi.Tenancy.Abstractions --prerelease
```

## Quick Start

Because this is a contracts package, the typical usage is either **consuming** an injected `ITenantContext` inside a service, or **implementing** one of the interfaces in your own class.

### Consuming the injected context

```csharp
// In a controller or service — ITenantContext is registered by Muonroi.Tenancy.Core
public class OrderService(ITenantContext tenantContext)
{
    public Task<IEnumerable<Order>> GetOrdersAsync()
    {
        string? tenantId = tenantContext.TenantId;
        // use tenantId to scope the query
    }
}
```

### Implementing a custom tenant-ID resolver

```csharp
using Muonroi.Tenancy.Abstractions.Interfaces;

public class JwtTenantIdResolver : ITenantIdResolver
{
    public Task<string?> ResolveTenantIdAsync(HttpContext context)
    {
        string? tenantId = context.User.FindFirst("tid")?.Value;
        return Task.FromResult(tenantId);
    }
}

// Registration (in your host project that references Muonroi.Tenancy.Core):
builder.Services.AddScoped<ITenantIdResolver, JwtTenantIdResolver>();
```

### Registering options from configuration

```csharp
builder.Services.Configure<MultiTenantOptions>(
    builder.Configuration.GetSection(MultiTenantOptions.SectionName)); // "MultiTenantConfigs"

builder.Services.Configure<TenantConnectionStringsOptions>(
    builder.Configuration.GetSection(TenantConnectionStringsOptions.SectionName)); // "TenantConnectionStrings"
```

```json
// appsettings.json
{
  "MultiTenantConfigs": {
    "Enabled": true,
    "RequireTenantClaimForAuthenticatedUser": true,
    "Strategy": "SharedSchema",
    "EnableRowLevelSecurity": false
  },
  "TenantConnectionStrings": {
    "ConnectionStrings": {
      "acme": "Host=db-acme;Database=acme;Username=app;Password=secret",
      "beta": "Host=db-beta;Database=beta;Username=app;Password=secret"
    }
  }
}
```

For a complete working example — including `TenantContext`, `DefaultTenantIdResolver`, `TenantSchemaSelector`, `MappingTenantConnectionStringFactory`, and `TenantResolutionMiddleware` — see the [Quickstart.Tenancy](../../samples/Quickstart.Tenancy/) sample.

## Features

- **`ITenantContext`** — get/set the ambient `TenantId` for the current execution scope.
- **`ITenantIdResolver`** — extract a tenant ID from an `HttpContext` asynchronously.
- **`ITenantConnectionStringFactory`** — resolve a connection string by tenant ID.
- **`ITenantScoped`** — marker interface for domain entities and services that belong to a single tenant.
- **`MultiTenantOptions`** — enable/disable multi-tenancy, pick isolation strategy (`SharedSchema`, `SeparateSchema`, `SeparateDatabase`), and opt in to PostgreSQL Row-Level Security.
- **`TenantConnectionStringsOptions`** — map tenant IDs to connection strings via `appsettings.json`.
- **`TenantIsolationStrategy`** enum — three isolation levels consumed by the runtime layer.
- **`NoOpTenantContext`** — null-object `ITenantContext` for single-tenant or test scenarios.
- **`ITenantLicenseFeatureGate`** — check whether a tenant has a named license feature enabled.
- **`TenantLicenseFeatures.Premium.MultiTenant`** — well-known feature-name constant for multi-tenancy license gating.
- **Legacy contracts** (`Muonroi.Tenancy.Abstractions.Legacy`) — `ITenantContext` and `ITenantIdResolver` kept for backward compatibility; prefer the canonical types above for new code.

## Configuration

`MultiTenantOptions` binds from the `"MultiTenantConfigs"` section:

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Master switch for multi-tenant features |
| `RequireTenantClaimForAuthenticatedUser` | `bool` | `true` | Enforce tenant claim on authenticated requests |
| `Strategy` | `TenantIsolationStrategy` | `SharedSchema` | Data isolation model |
| `EnableRowLevelSecurity` | `bool` | `false` | PostgreSQL RLS (`SET app.current_tenant_id`) on every connection |

`TenantConnectionStringsOptions` binds from the `"TenantConnectionStrings"` section:

| Property | Type | Description |
|---|---|---|
| `ConnectionStrings` | `Dictionary<string, string>` | Map of tenant ID → connection string |

## API Reference

| Type | Purpose |
|---|---|
| `ITenantContext` | Get/set `TenantId` for the ambient execution scope |
| `ITenantIdResolver` | Resolve a tenant ID from an `HttpContext` |
| `ITenantConnectionStringFactory` | Return a connection string for a given tenant ID |
| `ITenantScoped` | Marker for tenant-scoped types (exposes read-only `TenantId`) |
| `MultiTenantOptions` | Configuration: isolation strategy, RLS, claim enforcement |
| `TenantConnectionStringsOptions` | Per-tenant connection string map |
| `TenantIsolationStrategy` | Enum: `SharedSchema` / `SeparateSchema` / `SeparateDatabase` |
| `NoOpTenantContext` | Null-object `ITenantContext`; returns `null` TenantId |
| `ITenantLicenseFeatureGate` | Check if a tenant license feature is active |
| `TenantLicenseFeatures.Premium.MultiTenant` | Feature name constant `"multi-tenant"` |

## Samples

- [Quickstart.Tenancy](../../samples/Quickstart.Tenancy/) — end-to-end example: registers `TenantContext`, `DefaultTenantIdResolver`, `MappingTenantConnectionStringFactory`, and `TenantResolutionMiddleware`; exercises each via REST endpoints.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — concrete implementations: `TenantContext` (AsyncLocal), `DefaultTenantIdResolver`, `MappingTenantConnectionStringFactory`, `TenantSchemaSelector`.
- [`Muonroi.Tenancy`](../Muonroi.Tenancy/) — ASP.NET integration: `TenantResolutionMiddleware` and Redis tenant cache.
- [`Muonroi.Tenancy.SiteProfile`](../Muonroi.Tenancy.SiteProfile/) — site-profile multi-tenancy: `ISiteProfile`, `AddSiteProfile<T>()`, per-site DI isolation.
- [`Muonroi.Quota.Abstractions`](../Muonroi.Quota.Abstractions/) — quota contracts depended on by this package.
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — base platform contracts.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
