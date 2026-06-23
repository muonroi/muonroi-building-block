# Muonroi.Tenancy

> ASP.NET Core runtime layer for multi-tenancy: request-scoped tenant resolution middleware, per-tenant Redis cache, and OpenTelemetry metrics for auth failures.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Tenancy.svg)](https://www.nuget.org/packages/Muonroi.Tenancy/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Tenancy` is the ASP.NET Core runtime package of the Muonroi multi-tenancy stack. It provides `TenantResolutionMiddleware` — which extracts the tenant ID from the HTTP request (header, path segment, or subdomain), validates it against the caller's JWT claim, and propagates it through `TenantContext.CurrentTenantId` for the duration of the request. The package also ships `RedisTenantCache`, a Redis wrapper that namespaces all keys under `tenant:{tenantId}:` for clean per-tenant isolation, and `TenantResolutionTelemetry`, which emits OTel-native counters for auth failures.

This package depends on `Muonroi.Tenancy.Abstractions` (contracts) and `Muonroi.Tenancy.Core` (context propagation and DI helpers).

## Installation

```bash
dotnet add package Muonroi.Tenancy --prerelease
```

## Quick Start

Register core tenancy services from `Muonroi.Tenancy.Core`, then add the middleware:

```csharp
// Program.cs
using Muonroi.Tenancy.Core.Legacy;   // AddTenantContext
using Muonroi.Tenancy;               // TenantResolutionMiddleware

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register tenant context and connection-string factory
builder.Services.AddTenantContext(options =>
{
    builder.Configuration.GetSection("MultiTenant").Bind(options);
});

WebApplication app = builder.Build();

// Place after UseAuthentication / UseAuthorization so the JWT claim is available
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.Run();
```

After the middleware runs, read the resolved tenant ID anywhere in the request pipeline:

```csharp
string? tenantId = TenantContext.CurrentTenantId;
```

### Per-tenant Redis cache

```csharp
// Inject IConnectionMultiplexer (registered via StackExchange.Redis)
IConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
var cache = new RedisTenantCache(redis, tenantId: "acme");

await cache.SetAsync("settings:theme", "dark", expiry: TimeSpan.FromHours(1));
RedisValue theme = await cache.GetAsync("settings:theme");
```

## Features

- **Multi-source tenant resolution**: checks the custom request header (`X-Tenant-Id` via `CustomHeader.TenantId`), then the first URL path segment, then the subdomain — in that order of precedence.
- **Format validation**: tenant IDs must match `^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$`; malformed input returns `400 Bad Request`.
- **JWT claim cross-check**: if a resolved tenant ID conflicts with the `tenant_id` JWT claim the middleware returns `401 Unauthorized`; a missing claim also yields `401`.
- **AsyncLocal propagation**: `TenantContext.CurrentTenantId` is set for the request scope and cleared in the `finally` block — safe for async code.
- **OpenTelemetry tracing**: tenant ID is written to `Activity.Current` as both a tag (`tenant.id`) and baggage for distributed trace correlation.
- **Per-tenant Redis isolation**: `RedisTenantCache` prefixes every key with `tenant:{tenantId}:` and uses `SCAN` (not `KEYS`) for bulk enumeration.
- **OTel metrics**: `TenantResolutionTelemetry` exposes a `Counter<long>` instrument (`muonroi.tenancy.auth_failures`) with `failure_reason`, `header_tenant_id`, and `claim_tenant_id` dimensions.

## Configuration

Tenant context options live in `Muonroi.Tenancy.Core`. Bind them from `appsettings.json`:

```json
{
  "MultiTenant": {
    "DefaultTenantId": "default",
    "ConnectionStrings": {
      "acme": "Server=acme-db;Database=acme;...",
      "beta": "Server=beta-db;Database=beta;..."
    }
  }
}
```

Register in DI using the extension from `Muonroi.Tenancy.Core`:

```csharp
builder.Services.AddTenantContext(options =>
    builder.Configuration.GetSection("MultiTenant").Bind(options));
```

## API Reference

| Type | Purpose |
|------|---------|
| `TenantResolutionMiddleware` | ASP.NET Core middleware that resolves, validates, and propagates the tenant ID per request |
| `RedisTenantCache` | Redis wrapper that namespaces all keys under `tenant:{tenantId}:` to enforce per-tenant isolation |
| `TenantResolutionTelemetry` | Static helper exposing `AuthFailureCounter` (`Counter<long>`) and `RecordAuthFailure(reason, header, claim)` |
| `TenantContext` *(from Muonroi.Tenancy.Core)* | `static string? CurrentTenantId` — AsyncLocal holder for the active tenant |

## Samples

- [MultiTenantSaaS](../../samples/MultiTenantSaaS/) — SaaS pricing API demonstrating per-tenant rule evaluation with license-tier enforcement

## Compatibility

- Target framework: `net8.0`
- Requires: `Microsoft.AspNetCore.App` framework reference, `StackExchange.Redis`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — contracts: `ITenantContext`, `ITenantIdResolver`, `MultiTenantOptions`, quota interfaces
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — core implementations: `TenantContext`, `DefaultTenantIdResolver`, `TenantSchemaSelector`, `AddTenantContext` DI extension
- [`Muonroi.Tenancy.SiteProfile`](../Muonroi.Tenancy.SiteProfile/) — site-profile layer for multi-site deployments with per-site EF Core / Dapper infrastructure

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
