# Muonroi.RuleEngine.Proliferation.Persistence

> EF Core / PostgreSQL persistence layer for the Muonroi Rule Proliferation Engine — replaces the default in-memory store with a durable, tenant-isolated Postgres backend.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Proliferation.Persistence.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Proliferation.Persistence/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.RuleEngine.Proliferation` ships with an in-memory `IProliferationStore` that is sufficient for tests and single-node experimentation, but does not survive process restarts or support multi-tenant deployments. This package wires in a Postgres-backed implementation — `PostgresProliferationStore` — using Npgsql/EF Core. Scenarios, execution results, and rule-lineage trees are stored under a dedicated `proliferation` schema with tenant-id query filters applied automatically via `Muonroi.Tenancy.Core`.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Proliferation.Persistence --prerelease
```

## Quick Start

Call `AddMProliferationPostgres` after `AddMProliferation` (or any other proliferation setup that registers `IProliferationStore`). The extension removes the in-memory store and substitutes the Postgres implementation.

```csharp
builder.Services
    .AddMProliferation()                            // from Muonroi.RuleEngine.Proliferation
    .AddMProliferationPostgres(                     // from this package
        builder.Configuration.GetConnectionString("Proliferation")!);
```

Apply EF Core migrations before first use:

```bash
dotnet ef migrations add InitProliferation \
  --project src/Muonroi.RuleEngine.Proliferation.Persistence \
  --startup-project src/YourHost

dotnet ef database update \
  --project src/Muonroi.RuleEngine.Proliferation.Persistence \
  --startup-project src/YourHost
```

## Features

- **One-call registration** — `AddMProliferationPostgres(connectionString)` removes the in-memory store and registers `PostgresProliferationStore` as scoped.
- **Three-table schema** — `proliferation.neuron_scenarios`, `proliferation.scenario_results`, and `proliferation.rule_lineages` with `jsonb` columns for input/output facts and rule-flow graphs.
- **Tenant isolation** — `NeuronScenarioEntity` carries a `TenantId` column; `ProliferationDbContext.OnModelCreating` applies a global EF Core query filter using `TenantContext.CurrentTenantId` from `Muonroi.Tenancy.Core`.
- **Full `IProliferationStore` coverage** — saves scenarios and results, queries pending scenarios, filters by seed-rule code, retrieves lineage trees, and aggregates `ProliferationStats`.
- **Postgres-native types** — `jsonb` for facts and flow graphs; status and type enums stored as `int`; indexes on `seed_rule_code`, `status`, and `tenant_id`.
- **Upsert for results** — `SaveResultAsync` checks for an existing `ScenarioResultEntity` by `ScenarioId` and updates in place, preventing duplicate rows.

## Configuration

No options class is required. Pass the Npgsql connection string directly:

```csharp
builder.Services.AddMProliferationPostgres(
    "Host=localhost;Port=5432;Database=muonroi;Username=app;Password=secret");
```

`ProliferationDbContext` is registered as a standard EF Core `DbContext` and participates in the normal `IDesignTimeDbContextFactory` / `dotnet ef` tooling.

**appsettings.json example**

```json
{
  "ConnectionStrings": {
    "Proliferation": "Host=localhost;Port=5432;Database=muonroi;Username=app;Password=secret"
  }
}
```

## API Reference

| Type | Purpose |
|------|---------|
| `ProliferationPersistenceExtensions.AddMProliferationPostgres` | DI extension — registers `ProliferationDbContext` and replaces `IProliferationStore` with `PostgresProliferationStore`. |
| `PostgresProliferationStore` | Scoped `IProliferationStore` implementation backed by Npgsql/EF Core. |
| `ProliferationDbContext` | `DbContext` owning the three persistence tables under the `proliferation` schema. |
| `NeuronScenarioEntity` | Row type for `proliferation.neuron_scenarios`; carries `TenantId` for RLS-style filtering. |
| `ScenarioResultEntity` | Row type for `proliferation.scenario_results`; stores output facts and errors as `jsonb`. |
| `RuleLineageEntity` | Row type for `proliferation.rule_lineages`; records parent-child depth for tracing rule ancestry. |

## Samples

No dedicated sample exists for this package yet. See the proliferation engine's own usage in [`Muonroi.RuleEngine.Proliferation`](../Muonroi.RuleEngine.Proliferation/).

## Compatibility

- Target framework: `net8.0`
- Requires: ASP.NET Core App framework reference (`Microsoft.AspNetCore.App`)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Proliferation`](../Muonroi.RuleEngine.Proliferation/) — core proliferation engine and `IProliferationStore` abstraction; required dependency.
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — provides `TenantContext.CurrentTenantId` consumed by the query filter in `ProliferationDbContext`.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) at the repository root.
