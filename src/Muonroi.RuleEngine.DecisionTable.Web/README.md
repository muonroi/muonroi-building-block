# Muonroi.RuleEngine.DecisionTable.Web

> REST API controllers and UiEngine manifest for the Muonroi Decision Table Designer — mount the full decision-table management surface into any ASP.NET Core application with a single call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.DecisionTable.Web.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.DecisionTable.Web/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

This package wires the decision table REST API (`/api/v1/decision-tables`) and the UiEngine manifest contributor into your application. It depends on `Muonroi.RuleEngine.DecisionTable` for the engine, store, executor, and FEEL evaluator, and on `Muonroi.Core.Abstractions` for the UiEngine manifest contracts. Activation requires a **Licensed** tier Muonroi license; the registration call enforces this at startup.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.DecisionTable.Web --prerelease
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDecisionTableWeb(options =>
{
    // Choose one backend:
    options.PostgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
    // options.SqlServerConnectionString = builder.Configuration.GetConnectionString("SqlServer");
    options.Schema = "rules";          // default: "dbo"
    options.AutoMigrateDatabase = true; // default: true
});

var app = builder.Build();
app.MapControllers();
app.Run();
```

`AddDecisionTableWeb` internally calls:
1. `services.RequireMinimumTierFromProof(LicenseTier.Licensed, "decision-table.web")` — throws at startup without a valid license.
2. `services.AddDecisionTableEngine(configure)` — registers the store, executor, validator, and FEEL evaluator.
3. `services.TryAddEnumerable(...)` — registers `DecisionTableManifestContributor` as `IUiEngineManifestContributor`.
4. `services.AddControllers().AddApplicationPart(...)` — exposes all decision-table controllers.

## Features

- **CRUD REST API** for decision tables at `GET/POST/PUT/DELETE /api/v1/decision-tables` (also aliased under `/api/v1/rule-engine/decision-tables`)
- **Paginated list** with search, tenant, hit-policy, and soft-delete filters
- **Bulk upsert and bulk delete** via `POST /api/v1/decision-tables/bulk/upsert` and `/bulk/delete`
- **Row reorder** via `POST /api/v1/decision-tables/{id}/rows/reorder`
- **Execution endpoint** at `POST /api/v1/decision-tables/{id}/execute` — evaluates input facts against the table and returns matched rows, outputs, hit policy, and evaluation time
- **Import** (Excel/CSV, JSON, DMN XML) via `POST /api/v1/decision-tables/import` (up to 25 MB)
- **Export** in `json`, `xml`, `dmn`, and `excel` (CSV) formats via `GET /api/v1/decision-tables/{id}/export/{format}`
- **FEEL expression validation** at `POST /api/v1/decision-tables/{id}/feel/validate-expression`
- **Per-table validation** at `POST /api/v1/decision-tables/{id}/validate`
- **UiEngine manifest contribution** — registers Decision Table list and editor screens, navigation nodes, data sources, and actions into the `IUiEngineManifestContributor` pipeline (requires Professional tier in the manifest)

## Configuration

`AddDecisionTableWeb` accepts an optional `Action<DecisionTableEngineOptions>`:

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `PostgresConnectionString` | `string?` | `null` | PostgreSQL backend connection string |
| `SqlServerConnectionString` | `string?` | `null` | SQL Server backend connection string |
| `Schema` | `string` | `"dbo"` | Database schema for decision-table tables |
| `AutoMigrateDatabase` | `bool` | `true` | Create or migrate the schema on startup |

Configure via `appsettings.json` and bind manually, or pass inline as shown in the Quick Start.

## API Reference

| Type | Purpose |
|------|---------|
| `DecisionTableWebExtensions` | `AddDecisionTableWeb(IServiceCollection, Action<DecisionTableEngineOptions>?)` — single DI registration entry point |
| `DecisionTableController` | CRUD, bulk, execute, import, reorder, version history, diff, and audit endpoints |
| `DecisionTableExportController` | Export a table to JSON, XML, DMN, or CSV |
| `DecisionTableFeelController` | Validate a FEEL expression against a column data type |
| `DecisionTableValidationController` | Validate an existing stored table by id |
| `DecisionTableManifestContributor` | Contributes list/editor screens, actions, data sources, and navigation to the UiEngine manifest |
| `DecisionTableExecuteRequest` | `Dictionary<string, object?> Inputs` — facts passed to the executor |
| `DecisionTableExecuteResponse` | `Matched`, `HitPolicy`, `EvaluationTimeMs`, `MatchedRowIds`, `Outputs` |
| `DecisionTableOutputItem` | `RowId` + `IReadOnlyDictionary<string, object?> Outputs` for one matched row |
| `DecisionTableBulkUpsertRequest` | `Tables`, `Actor`, `Reason` |
| `DecisionTableBulkDeleteRequest` | `Ids`, `Actor`, `Reason` |
| `DecisionTableRowReorderRequest` | `RowIds`, `Actor`, `Reason` |
| `FeelValidateRequest` | `Expression`, `ColumnDataType` |
| `FeelValidateResponse` | `IsValid`, `Error` |
| `ValidationResultViewModel` | Returned from validation endpoints |

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires a Muonroi Licensed (or higher) tier activation. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.RuleEngine.DecisionTable`](../Muonroi.RuleEngine.DecisionTable/) — core engine, store, executor, FEEL evaluator, and `DecisionTableEngineOptions`; this package is a required dependency
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — `IUiEngineManifestContributor` and manifest model contracts

## License

This package is released under the **Muonroi Commercial License**. A valid license activating the `decision-table.web` feature is required at runtime. Contact [muonroi.com](https://muonroi.com) for licensing details.
