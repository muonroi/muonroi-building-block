# Muonroi.Tenancy.Core

> Runtime building blocks for shared-database multi-tenancy: `AsyncLocal`-backed tenant context, multi-source tenant ID resolution, per-tenant connection-string mapping, schema selection, quota tracking, and security validation.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Tenancy.Core.svg)](https://www.nuget.org/packages/Muonroi.Tenancy.Core/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Tenancy.Core` implements the core tenancy services consumed by `Muonroi.Tenancy` (the ASP.NET middleware layer) and your own application code. It provides `TenantContext` — an `AsyncLocal<string>`-backed store for the current tenant ID — alongside a five-source `DefaultTenantIdResolver` (claim → header → route → path → subdomain), `MappingTenantConnectionStringFactory` for per-tenant connection strings, `TenantSchemaSelector` for separate-schema isolation, and `TenantQuotaTracker` for distributed-cache-backed quota enforcement. It depends on `Muonroi.Tenancy.Abstractions` for contracts and `Muonroi.Quota.Abstractions` for quota types.

## Installation

```bash
dotnet add package Muonroi.Tenancy.Core --prerelease
```

## Quick Start

The services in this package can be registered individually without the license-gated `AddTenantContext()` helper. The `Quickstart.Tenancy` sample demonstrates this pattern:

```csharp
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Bind options consumed by TenantSchemaSelector and MappingTenantConnectionStringFactory.
builder.Services.Configure<MultiTenantOptions>(
    builder.Configuration.GetSection(MultiTenantOptions.SectionName)); // "MultiTenantConfigs"
builder.Services.Configure<TenantConnectionStringsOptions>(
    builder.Configuration.GetSection(TenantConnectionStringsOptions.SectionName)); // "TenantConnectionStrings"

// Ambient tenant context (AsyncLocal-backed).
builder.Services.AddScoped<ITenantContext, TenantContext>();

// HTTP-based tenant ID resolver: claim → header → route → path → subdomain.
builder.Services.AddScoped<ITenantIdResolver, DefaultTenantIdResolver>();

// Schema selector for SeparateSchema isolation.
builder.Services.AddSingleton<TenantSchemaSelector>();

// Per-tenant connection-string factory with a fallback.
builder.Services.AddSingleton<ITenantConnectionStringFactory>(sp =>
{
    IOptions<TenantConnectionStringsOptions> opts =
        sp.GetRequiredService<IOptions<TenantConnectionStringsOptions>>();
    string fallback = builder.Configuration.GetConnectionString("Default")
                      ?? "Host=localhost;Database=app;Username=app;Password=app";
    return new MappingTenantConnectionStringFactory(opts, fallback);
});

// Quota management (distributed-cache-backed tracker + in-memory store).
builder.Services.AddTenantQuotaManagement();
```

Read the current tenant ID anywhere in your application:

```csharp
// Via DI (request-scoped):
public class MyService(ITenantContext tenantContext)
{
    public void DoWork()
    {
        string? tenantId = tenantContext.TenantId;
    }
}

// Via static ambient (background jobs, EF filters):
string? tenantId = TenantContext.CurrentTenantId;
```

For admin/seeding operations that must bypass EF global query filters:

```csharp
using (new MSeedingScope())
{
    // TenantContext.AllowCrossTenantAccess == true here.
    // EF global filters for ITenantScoped entities are bypassed.
    await SeedDefaultRolesAsync(dbContext);
} // AllowCrossTenantAccess restored to previous value on dispose.
```

## Features

- **`TenantContext`** — `AsyncLocal<string?>`-backed `ITenantContext`; exposes `CurrentTenantId` (static, for EF filters) and `AllowCrossTenantAccess` (static, bypasses EF global filters when `true`)
- **`DefaultTenantIdResolver`** — resolves tenant ID from JWT claim → `X-Tenant-Id` header → route value (`tenantId`/`tenant`) → URL path segment → subdomain; skips reserved prefixes (`api`, `swagger`, `health`, `grpc`, `v1`–`v3`)
- **`TenantSchemaSelector`** — maps tenant ID to a database schema name for `SeparateSchema` isolation and optionally rewrites PostgreSQL connection strings with `SearchPath`
- **`MappingTenantConnectionStringFactory`** — looks up a per-tenant connection string from `TenantConnectionStringsOptions.ConnectionStrings`; falls back to a supplied default
- **`DefaultTenantConnectionStringFactory`** — single-connection-string factory for shared-database setups
- **`TenantSecurityValidator`** — static validator that cross-checks context, claim, and header tenant IDs; returns typed error codes (`missing-tenant-context`, `missing-tenant-claim`, `tenant-mismatch`, `header-claim-mismatch`)
- **`TenantQuotaTracker`** — distributed-cache-backed `ITenantQuotaTracker`; checks and increments usage counters keyed by tenant + `QuotaType` + time window; falls back to `TenantQuotaPresets.Free` when no quota record is found
- **`InMemoryTenantQuotaStore`** — in-process `ITenantQuotaStore` for development and testing
- **`MSeedingScope`** — `IDisposable` scope that sets `TenantContext.AllowCrossTenantAccess = true` for the duration of database seeding; restores the previous value on dispose
- **`ContextMirrorScope`** — `IDisposable` scope that mirrors an `ISystemExecutionContext` into `TenantContext.CurrentTenantId`, `UserContext`, and an optional log scope; used by background jobs and message consumers
- **`AddTenantQuotaManagement()`** — registers `InMemoryTenantQuotaStore` (singleton) and `TenantQuotaTracker` (scoped)
- **Legacy namespace** (`Muonroi.Tenancy.Core.Legacy`) — preserved `MultiTenantConfigs`, `TenantContextMiddleware`, and `AddTenantContext()` + `AddTenantIdResolver<Tr>()` helpers for projects that have not yet migrated to the current API

## Configuration

### `MultiTenantOptions` (section: `"MultiTenantConfigs"`)

Defined in `Muonroi.Tenancy.Abstractions`. Consumed by `TenantSchemaSelector`.

```json
{
  "MultiTenantConfigs": {
    "Strategy": "SharedDatabase"
  }
}
```

`Strategy` values: `SharedDatabase`, `SeparateSchema`. When `SeparateSchema` is set, `TenantSchemaSelector.ResolveSchema(tenantId)` returns a sanitized schema name and `ApplyToConnectionString` appends `SearchPath=<schema>` to PostgreSQL connection strings.

### `TenantConnectionStringsOptions` (section: `"TenantConnectionStrings"`)

Consumed by `MappingTenantConnectionStringFactory`.

```json
{
  "TenantConnectionStrings": {
    "ConnectionStrings": {
      "tenant-a": "Host=pg-a;Database=tenant_a;Username=app;Password=secret",
      "tenant-b": "Host=pg-b;Database=tenant_b;Username=app;Password=secret"
    }
  }
}
```

### Legacy `MultiTenantConfigs` (section: `"MultiTenantConfigs"`)

Used by `Legacy.AddTenantContext()` only:

```json
{
  "MultiTenantConfigs": {
    "Enabled": true,
    "RequireTenantClaimForAuthenticatedUser": true
  }
}
```

`AddTenantContext()` requires `AddLicenseProtection()` to be called first and enforces the `MultiTenant` license feature.

## API Reference

| Type | Purpose |
|------|---------|
| `TenantContext` | `ITenantContext` implementation; `AsyncLocal`-backed `TenantId` and `CurrentTenantId`; `AllowCrossTenantAccess` flag for EF filter bypass |
| `DefaultTenantIdResolver` | `ITenantIdResolver` — claim → header → route → path → subdomain resolution |
| `MappingTenantConnectionStringFactory` | `ITenantConnectionStringFactory` — dictionary lookup with fallback |
| `DefaultTenantConnectionStringFactory` | `ITenantConnectionStringFactory` — single connection string, shared-database |
| `TenantSchemaSelector` | Schema name resolution and PostgreSQL `SearchPath` injection for separate-schema isolation |
| `TenantSecurityValidator` | Static `TryValidate()` — cross-checks context, claim, and header; returns error codes |
| `TenantQuotaTracker` | `ITenantQuotaTracker` — `CheckQuotaAsync`, `IncrementUsageAsync`, `GetUsageAsync`, `ResetDailyQuotasAsync` |
| `InMemoryTenantQuotaStore` | `ITenantQuotaStore` — in-memory implementation for dev/test |
| `MSeedingScope` | `IDisposable` — enables cross-tenant EF access during database seeding |
| `ContextMirrorScope` | `IDisposable` — mirrors `ISystemExecutionContext` into ambient tenant/user context + log scope |
| `TenantQuotaServiceCollectionExtensions.AddTenantQuotaManagement()` | Registers `InMemoryTenantQuotaStore` (singleton) and `TenantQuotaTracker` (scoped) |
| `Legacy.TenantServiceCollectionExtensions.AddTenantContext()` | License-gated full registration: context, resolver, middleware (legacy API) |
| `Legacy.TenantServiceCollectionExtensions.AddTenantIdResolver<Tr>()` | Replaces `DefaultTenantIdResolver` with a custom resolver (legacy API) |

## Samples

- [Quickstart.Tenancy](../../samples/Quickstart.Tenancy/) — registers `TenantContext`, `DefaultTenantIdResolver`, `TenantSchemaSelector`, and `MappingTenantConnectionStringFactory` without the license gate; mounts `TenantResolutionMiddleware` on a branch

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — contracts: `ITenantContext`, `ITenantIdResolver`, `ITenantConnectionStringFactory`, `MultiTenantOptions`, `TenantConnectionStringsOptions`
- [`Muonroi.Tenancy`](../Muonroi.Tenancy/) — ASP.NET integration: `TenantResolutionMiddleware` and Redis tenant cache
- [`Muonroi.Quota.Abstractions`](../Muonroi.Quota.Abstractions/) — `ITenantQuotaTracker`, `ITenantQuotaStore`, `QuotaType`, `TenantQuota`


## Ecosystem Combinations

### + Tenancy (middleware) → Full ASP.NET Tenant Resolution
`Tenancy.Core` provides `TenantContext.CurrentTenantId` via `AsyncLocal<string>`; `Muonroi.Tenancy` provides the ASP.NET middleware that sets it from request headers/cookies.

### + Data.EntityFrameworkCore → Schema-Per-Tenant EF Core
`TenantSchemaSelector` sets the EF Core schema to the current `TenantId`. Every query goes to the correct tenant schema automatically:
```csharp
protected override void OnModelCreating(ModelBuilder mb)
    => mb.HasDefaultSchema(TenantSchemaSelector.Current);
```

### + Mediator → `MTenantValidationBehavior`
`TenantContext.CurrentTenantId` is read by `MTenantValidationBehavior` to block cross-tenant command execution.

### + Logging → `ContextMirrorScope`
`ContextMirrorScope` mirrors `CurrentTenantId` into the log scope automatically — every log entry is tagged with `tenantId` without any manual scope creation.

### Full Tenancy Core Stack
```csharp
builder.Services
    .AddTenantContext(config)                   // TenantContext + resolver + factory
    .AddMDbContext<AppDbContext>(config)         // schema-per-tenant EF
    .AddMMediator(opt => opt.AddMuonroiEcosystem()) // tenant validation in pipeline
    .AddMuonroiLogging(config);                // auto-enriched tenant logs
```

## Samples
- [`Quickstart.Tenancy.Core`](../../samples/Quickstart.Tenancy.Core)
- [`Quickstart.Tenancy`](../../samples/Quickstart.Tenancy)


## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
