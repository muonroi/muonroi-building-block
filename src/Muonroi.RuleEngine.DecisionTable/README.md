# Muonroi.RuleEngine.DecisionTable

> DMN-style decision table engine with FEEL expressions, multi-backend persistence, structural validation, and import/export for the Muonroi Rule Engine.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.DecisionTable.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.DecisionTable/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

Decision tables let you encode business rules as a matrix of input conditions and output values — no code changes needed to update rule logic at runtime. This package provides the models, FEEL expression evaluator, validators, converters, serializers, and a pluggable store (in-memory, SQL Server, or PostgreSQL) that power the Muonroi decision table subsystem. The optional companion package `Muonroi.RuleEngine.DecisionTable.Web` exposes REST endpoints and a live-edit UI on top of this core engine.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.DecisionTable --prerelease
```

## Quick Start

Register the engine in `Program.cs`. No connection string = in-memory store (ideal for testing and small deployments):

```csharp
builder.Services.AddDecisionTableEngine();
```

Persist tables to PostgreSQL:

```csharp
builder.Services.AddDecisionTableEngine(options =>
{
    options.PostgresConnectionString =
        builder.Configuration.GetConnectionString("RuleDb");
});
```

Execute a table once retrieved from the store:

```csharp
using Muonroi.RuleEngine.DecisionTable;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Stores;

// IDecisionTableStore and IDecisionTableExecutor are injected via DI.
DecisionTable? table = await store.GetByIdAsync("loan-approval");

DecisionTableExecutionResult result = await executor.ExecuteAsync(
    table!,
    new Dictionary<string, object?>
    {
        ["age"]    = 35,
        ["income"] = 75_000m,
        ["score"]  = 720
    });

foreach (DecisionTableOutputRow row in result.MatchedRows)
{
    Console.WriteLine(row.Outputs["decision"]);   // e.g. "Approved"
}
```

See the full working example in [Quickstart.DecisionTable](../../samples/Quickstart.DecisionTable/).

## Features

- **FEEL expression evaluation** — full FEEL cell evaluator (`FullFeelCellEvaluator`) with a lightweight fallback (`SimplifiedFeelCellEvaluator`); numeric, string, range, and boolean expressions supported.
- **Nine hit policies** — `First`, `Unique`, `Collect`, `Priority`, `OutputOrder`, `CollectSum`, `CollectMin`, `CollectMax`, `CollectCount`.
- **Multi-backend persistence** — in-memory (default), SQL Server, or PostgreSQL via EF Core; automatic schema migration on startup.
- **Audit trail and versioning** — every `SaveAsync` records an audit entry and increments a version snapshot; full history queryable through `IDecisionTableStore`.
- **Structural validation** — `DecisionTableValidator` checks expression syntax, overlap, gaps, and multi-column redundancy before rules are persisted or executed.
- **Excel import** — `ExcelToDecisionTableConverter` reads Excel files with `in:` / `out:` column headers directly into a `DecisionTable` model.
- **DMN 1.3 import/export** — `DmnExporter.ExportToDmnXml` and `DmnImporter.ImportFromDmnXml` provide bidirectional spec-compliant DMN 1.3 XML exchange.
- **JSON and XML serializers** — `DecisionTableJsonSerializer` and `DecisionTableXmlSerializer` for custom serialization needs.
- **Rule conversion** — `DecisionTableToRuleConverter` bridges a decision table into the Muonroi Rule Engine's `IRule` pipeline.
- **Structural diffing** — `DecisionTableDiffer` produces a `DecisionTableDiff` (row adds/removes, cell changes, column schema changes) between two table versions.
- **Bulk operations** — `IDecisionTableStore.BulkUpsertAsync` and `BulkDeleteAsync` for batch management.

## Configuration

### DI registration

```csharp
builder.Services.AddDecisionTableEngine(options =>
{
    // SQL Server persistence (optional)
    options.SqlServerConnectionString = "Server=...";

    // PostgreSQL persistence (optional, takes priority over SqlServer if both are set)
    options.PostgresConnectionString = "Host=...";

    // Database schema (default: "dbo")
    options.Schema = "rules";

    // Automatically create/migrate the schema on startup (default: true)
    options.AutoMigrateDatabase = true;
});
```

### `DecisionTableEngineOptions`

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `SqlServerConnectionString` | `string?` | `null` | SQL Server connection string for EF Core persistence |
| `PostgresConnectionString` | `string?` | `null` | PostgreSQL connection string for EF Core persistence |
| `Schema` | `string` | `"dbo"` | Database schema name |
| `AutoMigrateDatabase` | `bool` | `true` | Run EF migrations on `IHostedService` startup |

When neither connection string is set, `InMemoryDecisionTableStore` is registered.

## API Reference

| Type | Purpose |
|------|---------|
| `IDecisionTableExecutor` | Evaluates a `DecisionTable` against input facts; returns `DecisionTableExecutionResult` |
| `IDecisionTableStore` | CRUD, bulk ops, versioning, audit trail, and row reordering |
| `DecisionTable` | Core model — columns, rows, hit policy, version, tenant ID |
| `DecisionTableColumn` | Describes an input or output column |
| `DecisionTableRow` | One row of conditions and outputs (list of `DecisionTableCell`) |
| `DecisionTableCell` | A single cell value, including a parsed `CellExpression` |
| `HitPolicy` | Enum: `First`, `Unique`, `Collect`, `Priority`, `OutputOrder`, `CollectSum`, `CollectMin`, `CollectMax`, `CollectCount` |
| `DecisionTableExecutionResult` | Execution output: matched `DecisionTableOutputRow` list |
| `DecisionTableEngineOptions` | DI configuration for storage backend |
| `DecisionTableValidator` | Validates structure, FEEL expressions, overlaps, gaps, and redundancy |
| `ExcelToDecisionTableConverter` | Imports from an Excel file or stream |
| `DecisionTableToRuleConverter` | Converts a decision table into `IRule` pipeline rules |
| `DecisionTableDiffer` | Diffs two `DecisionTable` versions into a `DecisionTableDiff` |
| `DecisionTableJsonSerializer` | Serializes/deserializes tables to JSON |
| `DecisionTableXmlSerializer` | Serializes/deserializes tables to DMN-compatible XML |
| `DmnExporter` | Static — exports `DecisionTable` to DMN 1.3 XML string |
| `DmnImporter` | Static — parses DMN 1.3 XML into `DecisionTable` |
| `IFeelCellEvaluator` | FEEL expression evaluator contract |
| `FeelParser` | Parses FEEL expressions into `FeelType`-tagged `CellExpression` nodes |
| `IDecisionTableStore` | Store contract (query, get, save, bulk, reorder, history, audit, delete) |
| `InMemoryDecisionTableStore` | Volatile in-memory implementation |

## Samples

- [Quickstart.DecisionTable](../../samples/Quickstart.DecisionTable/) — minimal ASP.NET Core API wiring `AddDecisionTableEngine` with a PostgreSQL backend and Swagger UI

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — shared contracts (`IRule`, `FactBag`, `RuleResult`) that this package builds on
- [`Muonroi.RuleEngine.DecisionTable.Web`](../Muonroi.RuleEngine.DecisionTable.Web/) — REST controllers and live-edit UI layer over this engine
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — rule orchestrator that can consume decision tables via `DecisionTableToRuleConverter`
- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — EF Core base infrastructure used by the persistent store

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
