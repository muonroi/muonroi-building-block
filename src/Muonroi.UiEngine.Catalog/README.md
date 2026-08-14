# Muonroi.UiEngine.Catalog

> Runtime catalog service that scans ASP.NET Core APIs and rule engine rules, builds API-to-rule bindings and a dependency graph, and exposes them through REST endpoints consumed by the Muonroi Rule Studio UI.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.UiEngine.Catalog.svg)](https://www.nuget.org/packages/Muonroi.UiEngine.Catalog/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

`Muonroi.UiEngine.Catalog` is a commercial ASP.NET Core library that auto-discovers API endpoints and `IRule<TContext>` implementations from the running application, links them into a typed catalog graph, and serves the result over dedicated REST routes (`/api/v1/ui-engine/catalog/*`). Snapshots of the graph can be persisted in PostgreSQL or SQL Server for historical diffing and UI-driven navigation. The package is consumed by the `mu-rule-flow-designer` frontend palette and by the Muonroi Rule Studio.

## Installation

```bash
dotnet add package Muonroi.UiEngine.Catalog --prerelease
```

## Quick Start

Call `AddUiEngineCatalog` inside `Program.cs`, then add the controllers to the MVC pipeline. Without a connection string the catalog uses an in-memory snapshot store (suitable for development).

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddUiEngineCatalog(options =>
{
    // Optional: persist snapshots to PostgreSQL
    options.PostgresConnectionString =
        builder.Configuration.GetConnectionString("Catalog");
    options.Schema = "catalog";
    options.AutoMigrateDatabase = true;
});

var app = builder.Build();
app.MapControllers();
 await app.RunAsync();
```

Once the app is running the following endpoints are available:

| Verb | Route | Description |
|------|-------|-------------|
| `GET` | `/api/v1/ui-engine/catalog/apis` | All discovered API descriptors |
| `GET` | `/api/v1/ui-engine/catalog/rules` | All discovered rule descriptors |
| `GET` | `/api/v1/ui-engine/catalog/bindings` | API-to-rule bindings |
| `GET` | `/api/v1/ui-engine/catalog/graph` | Full dependency graph |
| `GET` | `/api/v1/ui-engine/catalog/snapshots` | Stored snapshot history |
| `GET` | `/api/v1/ui-engine/catalog/snapshots/latest` | Most recent snapshot |
| `POST` | `/api/v1/ui-engine/catalog/snapshots/capture` | Persist the current graph |
| `GET` | `/api/v1/ui-engine/catalog/palette` | Rule palette for Rule Studio UI |
| `GET` | `/api/v1/ui-engine/connectors/catalog` | Connector type list |

## Features

- **API scanning** — reflects over all registered `ApiDescription` groups to produce typed `MUiEngineCatalogApiDescriptor` records with route, HTTP method, auth schemes, request/response types, and tenant-requirement flag.
- **Rule scanning** — walks all loaded assemblies for concrete `IRule<TContext>` and `ICompensatableRule<TContext>` implementations and captures code, order, hook point, rule type, and dependency list.
- **Binding resolution** — links APIs to the rules they invoke via `[BindRuleContext]` attribute metadata.
- **Dependency graph** — builds a `MUiEngineCatalogGraph` from scanned APIs and rules for visualisation in Rule Studio.
- **Snapshot persistence** — saves and retrieves graph snapshots per tenant via `ICatalogSnapshotStore`; backed by PostgreSQL (Npgsql), SQL Server (EF Core), or an in-memory store when no connection string is configured.
- **Auto-migration** — `UiEngineCatalogDatabaseMigrator` runs `EnsureCreatedAsync` on startup when `AutoMigrateDatabase = true`.
- **Rule Studio palette** — `MRuleCatalogCompatController` returns rule groups in the shape expected by `MRuleCatalogService` on the frontend, with optional `category` and `search` query filters.
- **Connector catalog** — `MConnectorCatalogController` exposes `IConnectorRegistry.ListAvailable()` without authentication (the flow designer calls this endpoint directly).
- **Per-tenant caching** — catalog responses are cached in `IMemoryCache` with a 5-minute absolute expiry, keyed by tenant ID.

## Configuration

### DI registration

```csharp
services.AddUiEngineCatalog(options =>
{
    // Use PostgreSQL for persistent snapshots
    options.PostgresConnectionString = "Host=...;Database=catalog;...";

    // — or — SQL Server
    options.SqlServerConnectionString = "Server=...;Database=catalog;...";

    // Database schema (default: "dbo")
    options.Schema = "dbo";

    // Run EF Core migrations on startup (default: true)
    options.AutoMigrateDatabase = true;
});
```

When neither connection string is set, `ICatalogSnapshotStore` is bound to `InMemoryCatalogSnapshotStore` — no database is required.

### `UiEngineCatalogOptions` properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PostgresConnectionString` | `string?` | `null` | Npgsql connection string for snapshot persistence |
| `SqlServerConnectionString` | `string?` | `null` | SQL Server connection string for snapshot persistence |
| `Schema` | `string` | `"dbo"` | Database schema used by the snapshot table |
| `AutoMigrateDatabase` | `bool` | `true` | Run `EnsureCreatedAsync` on startup |

## API Reference

| Type | Purpose |
|------|---------|
| `UiEngineCatalogExtensions` | Extension class — `AddUiEngineCatalog(Action<UiEngineCatalogOptions>?)` |
| `UiEngineCatalogOptions` | Configuration options for the catalog (connection strings, schema, auto-migrate) |
| `ICatalogScanService` | Scans APIs (`ScanApisAsync`), rules (`ScanRulesAsync`), bindings (`BuildBindingsAsync`), and graph (`BuildGraphAsync`) |
| `ICatalogSnapshotStore` | Persists and retrieves catalog graph snapshots per tenant |
| `UiEngineCatalogController` | REST controller — `GET /api/v1/ui-engine/catalog/*` and `POST .../snapshots/capture` |
| `MRuleCatalogCompatController` | REST controller — palette endpoint for `mu-rule-flow-designer` at `GET /api/v1/ui-engine/catalog/palette` |
| `MConnectorCatalogController` | REST controller — anonymous connector catalog at `GET /api/v1/ui-engine/connectors/catalog` |
| `BindRuleContextAttribute` | Method attribute that binds a rule context type (and optional workflow name) to an endpoint |
| `MUiEngineCatalogApiDescriptor` | Descriptor record for a discovered API endpoint |
| `MUiEngineCatalogRuleDescriptor` | Descriptor record for a discovered rule implementation |
| `MUiEngineCatalogBinding` | Links an API descriptor to its associated rule descriptors |
| `MUiEngineCatalogGraph` | Full catalog graph combining APIs, rules, and bindings |
| `MUiEngineCatalogSnapshot` | Persisted point-in-time graph snapshot |
| `CatalogSnapshotSummary` | Lightweight summary returned by the snapshot list endpoint |

## Samples

No dedicated sample exists for this package. The Quick Start above is grounded directly in the public API.

## Compatibility

- Target framework: `net8.0`
- Requires: `Microsoft.AspNetCore.App` framework reference
- License: Commercial — requires a valid Muonroi license. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `ICatalogScanService`, `ICatalogSnapshotStore`, and all catalog model types
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — defines `IRule<TContext>` and `ICompensatableRule<TContext>` that the scanner discovers
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — rule runtime whose `RuleOptions` the scanner reads for runtime-registered rules
- [`Muonroi.Integration.Abstractions`](../Muonroi.Integration.Abstractions/) — defines `IConnectorRegistry` consumed by the connector catalog endpoint
- [`Muonroi.AspNetCore`](../Muonroi.AspNetCore/) — host package that wires the catalog into the broader UI engine manifest pipeline

## Ecosystem Combinations

### + Tenancy.SiteProfile → Per-Site UI Catalogs
Each site profile can define its own UI component catalog. `UiManifestBuilder` reads the active site profile and serves only that site's components to the frontend.

### + SignalR → Real-Time Catalog Updates
When the UI catalog changes (hot-reload), `MUiEngineHub` broadcasts the updated manifest to all connected clients — the frontend updates without a page refresh.

### + Governance → License-Gated UI Components
Premium UI components are only included in the manifest if the active tenant's license tier includes them. Free-tier users see a reduced component set.

### + Caching.Memory → Cached Manifests
UI manifests are cached per-tenant per-site. Cache is invalidated on hot-reload events.

### Full UI Engine Stack
```csharp
builder.Services
    .AddUiEngineCatalog(config)            // component catalog
    .AddSiteProfile<MySiteProfile>()       // per-site manifest
    .AddSignalRWithTenant()                // real-time push
    .AddGovernance(config);               // license-gated components
```

## Samples
- [`Quickstart.UiEngine.Catalog`](../../samples/Quickstart.UiEngine.Catalog)

## License

This package requires a commercial Muonroi license. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL) for terms. Contact [leanhphi1706@gmail.com](mailto:leanhphi1706@gmail.com) for activation.
