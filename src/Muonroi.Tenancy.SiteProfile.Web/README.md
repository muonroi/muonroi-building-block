# Muonroi.Tenancy.SiteProfile.Web

> ASP.NET Core middleware, EF Core / Dapper per-site data access, and SignalR hot-reload for the Muonroi multi-site tenancy system.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Tenancy.SiteProfile.Web.svg)](https://www.nuget.org/packages/Muonroi.Tenancy.SiteProfile.Web/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is the ASP.NET Core integration layer for `Muonroi.Tenancy.SiteProfile`. It adds the
request pipeline (state middleware, telemetry), per-site EF Core DbContext and Dapper data access
that avoids the Autofac non-generic `DbContextOptions` conflict, a SignalR-backed hot-reload client
that pushes profile changes from the Control Plane without a restart, and a one-call
`AddSiteInfrastructure` umbrella that collapses the typical 5–7 per-consumer setup calls.

## Installation

```bash
dotnet add package Muonroi.Tenancy.SiteProfile.Web --prerelease
```

## Quick Start

The example below reflects the pattern used in `samples/TestProject.Service`.

```csharp
// Program.cs

// 1. Declare per-site EF Core infrastructure (Autofac-safe — no non-generic DbContextOptions conflict)
services.AddSiteDbInfrastructure(o =>
{
    o.TenantId        = sp => accessor.MultiTenantContext?.TenantInfo?.TenantId;
    o.ConnectionString = sp => accessor.MultiTenantContext?.TenantInfo?.ConnectionString!;
    o.ConnectionStringTransform = cs => Cryptography.Decrypt(secretKey, cs);
    o.ConfigureDbContext = (b, cs) => b.UseSqlServer(cs, opt => opt.UseCompatibilityLevel(120));
});

// 2. Register each per-site DbContext (does NOT register the base DbContextOptions)
services.AddSiteDbContext<MySiteDbContext>();

// 3. Auto-run EF Core migrations at startup across all registered site DbContexts
services.AddSiteMigrationRunner(o =>
{
    o.Strategy      = MigrationStrategy.AutoMigrate;
    o.MaxParallelism = 4;
});

// 4. SignalR hot-reload — pushes profile changes from Control Plane without restart
services.AddSiteProfileHotReload(o =>
{
    o.ControlPlaneUrl    = "https://control-plane.example.com";
    o.AccessTokenFactory = () => Task.FromResult<string?>(tokenProvider.GetToken());
    o.ReconnectDelay     = TimeSpan.FromSeconds(10);
});

// 5. Middleware pipeline
app.UseSiteProfileStateMiddleware();   // returns 503 for disabled sites
app.UseSiteProfileTelemetry();         // Activity tags, IMLog scope, request counter
```

### One-call umbrella

For services that combine multi-site profiles, per-site configuration, and controller discovery,
`AddSiteInfrastructure` replaces the individual calls above:

```csharp
services.AddSiteInfrastructure(configuration, o =>
{
    o.SiteCodeAccessor         = sp => sp.GetRequiredService<IWorkContextAccessor>().WorkContext?.SiteCode;
    o.SiteAssemblies           = [typeof(TciSiteProfile).Assembly, typeof(DefaultSiteProfile).Assembly];
    o.EnableControllerDiscovery = true;   // registers MVC ApplicationParts from site assemblies
    o.SkipStartupValidation    = true;    // aggregate services without DbContext
});
```

## Features

- **Autofac-safe EF Core**: `AddSiteDbContext<T>()` registers only the generic `DbContextOptions<T>`,
  leaving the non-generic base untouched — multiple site DbContexts coexist without "last wins" conflicts.
- **Per-site connection strings**: tenant ID and connection string resolvers are injected at request
  scope via `ITenantContext` and `ITenantConnectionStringFactory` adapters.
- **Optional connection string transform**: decrypt encrypted strings before they reach EF Core or Dapper.
- **Migration runner**: `AddSiteMigrationRunner` discovers all registered site DbContext types at
  startup and runs EF Core migrations in parallel with configurable concurrency.
- **Dapper support**: `AddSiteDapperInfrastructure` registers a scoped `IConnectionStringProvider`,
  a write `IDapper`, and an optional read-replica `IDapperRead` per site.
- **State middleware**: `UseSiteProfileStateMiddleware` / `SiteProfileStateMiddleware` short-circuits
  requests for disabled sites with HTTP 503.
- **Telemetry middleware**: `UseSiteProfileTelemetry` / `SiteProfileTelemetryMiddleware` stamps
  `Activity.Current` with `site.id`, adds an IMLog scope, and increments `site_profile_requests_total`.
- **SignalR hot-reload**: `AddSiteProfileHotReload` starts `SiteProfileHotReloadClient` as a hosted
  service that subscribes to the Control Plane hub and propagates changes via `ISiteProfileStateRegistry`.
- **Per-site command dispatch**: `AddSiteCommandHandler<TCmd, TResp>` registers a keyed-DI dispatch
  factory so MediatR routes commands to the correct site-specific handler at runtime.
- **Schema validation**: `AddSiteSchemaValidation` runs `SiteSchemaValidator` on startup with
  configurable severity (`FailFast`, `WarnOnMismatch`).
- **Behavior pipeline**: `AddSiteProfileBehaviors` reads `[SiteProfileBehavior]` attributes from a
  profile and applies built-in behaviors — `SiteCachingBehavior`, `SiteQuotaBehavior`,
  `SiteAuditBehavior`, `SiteObservabilityBehavior`.
- **Controller discovery**: `AddSiteControllers` / `AddSiteInfrastructure` registers MVC
  `ApplicationParts` from site assemblies so per-site REST controllers are discovered automatically.

## Configuration

### EF Core — `SiteDbInfrastructureOptions`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `TenantId` | `Func<IServiceProvider, string?>` | Yes | Resolves current tenant ID per scope |
| `ConnectionString` | `Func<IServiceProvider, string>` | Yes | Resolves raw connection string per scope |
| `ConnectionStringTransform` | `Func<string, string>?` | No | Decrypts/transforms raw connection string |
| `ConfigureDbContext` | `Action<DbContextOptionsBuilder, string>?` | No | Provider setup; defaults to `UseSqlServer` |

### Hot-reload — `SiteProfileHotReloadOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ControlPlaneUrl` | `string?` | — | Base URL or full SignalR hub URL |
| `AccessTokenFactory` | `Func<Task<string?>>?` | `null` | Bearer token for authenticated hubs |
| `ReconnectDelay` | `TimeSpan` | 10 s | Delay before retrying a failed connection |

### Umbrella — `SiteInfrastructureOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SiteCodeAccessor` | `Func<IServiceProvider, string?>` | Required | Per-request site code resolver |
| `SiteAssemblies` | `Assembly[]` | `[]` | Assemblies scanned for `ISiteProfile` implementations |
| `ManifestProfiles` | `ISiteProfile[]?` | `null` | AOT path — pre-instantiated profiles; bypasses reflection |
| `EnableControllerDiscovery` | `bool` | `false` | Registers MVC ApplicationParts from `SiteAssemblies` |
| `SkipStartupValidation` | `bool` | `false` | Suppresses `SiteProfileStartupValidator` (aggregate services) |

### Dapper — `SiteDapperInfrastructureOptions`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `WriteConnectionString` | `Func<IServiceProvider, string>` | Yes | Write (primary) connection string resolver |
| `ReadConnectionString` | `Func<IServiceProvider, string?>?` | No | Read-replica resolver; falls back to write if absent |
| `ConnectionStringTransform` | `Func<string, string>?` | No | Optional decrypt/transform |

### Migration — `SiteMigrationRunnerOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Strategy` | `MigrationStrategy` | `AutoMigrate` | `AutoMigrate` or `ValidateOnly` |
| `MaxParallelism` | `int` | unbounded | Limits concurrent per-site migrations |

### Schema validation — `SiteSchemaValidationOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Severity` | `SchemaValidationSeverity` | — | `FailFast` (throws) or `WarnOnMismatch` (logs) |

## API Reference

| Type | Purpose |
|------|---------|
| `SiteProfileWebExtensions` | `AddSiteInfrastructure`, `AddSiteProfileHotReload`, `AddSiteProfileBehaviors`, `AddSiteControllers`, `UseSiteProfileStateMiddleware`, `UseSiteProfileTelemetry` |
| `SiteProfileDbContextExtensions` | `AddSiteDbInfrastructure`, `AddSiteDbContext<T>`, `AddSiteMigrationRunner`, `AddSiteConfiguration`, `AddSiteCommandHandler<TCmd, TResp>` |
| `SiteProfileDapperExtensions` | `AddSiteDapperInfrastructure`, `AddSiteSqlBuilder` |
| `SiteProfileStateMiddleware` | Returns HTTP 503 for sites with disabled state |
| `SiteProfileTelemetryMiddleware` | Activity tag, IMLog scope, `site_profile_requests_total` counter |
| `SiteProfileHotReloadClient` | `IHostedService` — SignalR subscriber for live profile changes |
| `ISiteProfileStateRegistry` / `SiteProfileStateRegistry` | Mutable bridge between hot-reload events and state middleware |
| `ISiteProfileChangeHandler` | Implement to receive hot-reload notifications |
| `SiteInfrastructureOptions` | Options for `AddSiteInfrastructure` |
| `SiteDbInfrastructureOptions` | Options for `AddSiteDbInfrastructure` |
| `SiteProfileHotReloadOptions` | Options for `AddSiteProfileHotReload` |
| `SiteDapperInfrastructureOptions` | Options for `AddSiteDapperInfrastructure` |
| `SiteMigrationRunnerOptions` | Options for `AddSiteMigrationRunner` |
| `SiteSchemaValidationOptions` | Options for `AddSiteSchemaValidation` |
| `MSiteRepository<TContext, TEntity>` | Abstract EF Core repository base; `DbContext` resolved per-request via `ISiteProfileResolver` |
| `MSiteService<TContext, TEntity>` | Abstract service base; inherits per-site context resolution |
| `IMSiteRepository<TContext, T>` | Repository contract — extends `IMRepository<T>` |
| `IMSiteCommandHandler<TRequest, TResponse>` | MediatR command handler contract for per-site handlers |
| `IDapperRead` | Marker interface for read-replica Dapper implementations |
| `ISiteColumnMap` / `DefaultSiteColumnMap` | Column name mapping contract and default base class for per-site Dapper queries |
| `ISiteConfiguration` / `SiteConfiguration` | Per-site `appsettings.json` overlay (`Sites:{SiteId}:*`); live hot-reload via `IConfiguration` |
| `SiteSchemaValidator` | `IHostedService` that validates DB schema against site profiles at startup |
| `SiteQuotaBehavior` / `ISiteQuotaEnforcer` | Built-in quota enforcement behavior |
| `SiteCachingBehavior` / `ISiteCacheKeyPrefix` | Built-in distributed cache behavior |
| `SiteAuditBehavior` | Built-in audit-trail behavior |
| `SiteObservabilityBehavior` / `ISiteActivityEnricher` | Built-in OTel enrichment behavior |

## Samples

- [TestProject.Service](../../samples/TestProject.Service/) — gRPC multi-site service with EF Core per-site DbContexts, Dapper, schema validation, and pipeline hooks
- [TestProject.Aggregate](../../samples/TestProject.Aggregate/) — aggregate gRPC service with handler-based dispatch and per-site gRPC service discovery

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Tenancy.SiteProfile`](../Muonroi.Tenancy.SiteProfile/) — core site profile contracts (`ISiteProfile`, `ISiteProfileResolver`, `AddMultiSiteProfiles`); required dependency
- [`Muonroi.Tenancy.SiteProfile.Grpc`](../Muonroi.Tenancy.SiteProfile.Grpc/) — gRPC interceptor and `AddSiteGrpcServices` for server-side site code extraction
- [`Muonroi.Tenancy.SiteProfile.SourceGenerators`](../Muonroi.Tenancy.SiteProfile.SourceGenerators/) — source generator that emits `RegisterServices` partials and the `SiteProfileManifest` for AOT
- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — EF Core base types used by `MSiteRepository`
- [`Muonroi.Caching.Abstractions`](../Muonroi.Caching.Abstractions/) — cache contracts consumed by `SiteCachingBehavior`
- [`Muonroi.Quota.Abstractions`](../Muonroi.Quota.Abstractions/) — quota contracts consumed by `SiteQuotaBehavior`
- [`Muonroi.Mediator`](../Muonroi.Mediator/) — MediatR integration used by `AddSiteCommandHandler`

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
