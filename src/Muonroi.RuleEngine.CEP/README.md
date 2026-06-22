# Muonroi.RuleEngine.CEP

> Complex Event Processing (CEP) for the Muonroi Rule Engine — pattern-based event correlation and temporal windowing over arbitrary payload types.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.CEP.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.CEP/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package adds sliding and tumbling windows to the Muonroi Rule Engine ecosystem. Events are aggregated by a correlation key, out-of-order arrivals are handled via sorted insertion, and expired events are evicted automatically based on a configurable TTL. Window configurations can be persisted to PostgreSQL or SQL Server (auto-migrated on startup) or kept entirely in-memory for development. A built-in REST controller (`/api/v1/rule-engine/cep`) exposes CRUD and a simulation endpoint so operators can inspect and test windows without code changes.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.CEP --prerelease
```

## Quick Start

### 1. Register in the DI container

```csharp
// Program.cs
builder.Services.AddCepWeb(options =>
{
    // PostgreSQL persistence (auto-migrates schema on startup)
    options.PostgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
    options.Schema = "cep";
    options.AutoMigrateDatabase = true;

    // Or SQL Server:
    // options.SqlServerConnectionString = "...";

    // Omit both connection strings to use the built-in in-memory store.
});
```

### 2. Build a window configuration and evaluate events

```csharp
using Muonroi.RuleEngine.CEP;
using Muonroi.RuleEngine.CEP.Builder;

// Define a 30-second sliding window correlated by sensor ID.
CepConfig config = CepWindowBuilder
    .Named("sensor-anomaly")
    .Sliding(TimeSpan.FromSeconds(30))
    .KeepEventsFor(TimeSpan.FromMinutes(2))
    .CorrelateBy("sensorId")
    .Describe("Detects anomalies across the last 30 s per sensor.")
    .Build();

// Create a runtime window bound to the config.
CepWindow<SensorReading> window = CepWindowBuilder
    .For<SensorReading>(config)
    .CorrelateBy(reading => reading.SensorId)
    .Build();

// Feed events; each call returns every event currently inside the active window.
IReadOnlyList<CepEvent<SensorReading>> inWindow = window.Add(reading, DateTime.UtcNow);

if (inWindow.Count >= 5)
{
    Console.WriteLine($"Anomaly: {inWindow.Count} readings in the last 30 s for {reading.SensorId}");
}
```

## Features

- **Sliding windows** — continuous overlap; each new event returns all events within `[now - windowSize, now]`.
- **Tumbling windows** — non-overlapping fixed-size buckets; events are grouped by aligned time boundaries.
- **TTL-based eviction** — events older than the configured TTL are removed on every `AddEvent` call, bounding memory usage.
- **Out-of-order handling** — sorted binary insertion keeps the window consistent regardless of arrival order.
- **Fluent builder** — `CepWindowBuilder.Named(...).Sliding(...).CorrelateBy(...).Build()` constructs validated `CepConfig` and `CepWindow<T>` instances.
- **Dual persistence backends** — PostgreSQL (Npgsql) and SQL Server via EF Core; auto-migration on startup; in-memory fallback for tests or development.
- **REST management API** — `GET/PUT/DELETE /api/v1/rule-engine/cep/{id}` and `POST /api/v1/rule-engine/cep/{id}/simulate` for operational management.
- **OpenTelemetry integration** — `ActivitySource` spans and `Meter` counters (`cep.events_processed`, `cep.window_evaluations`, `cep.window_event_count`) on every window evaluation.
- **Multi-tenant aware** — `TenantId` is stored on each `CepConfig`; the repository filters by the current execution context.

## Configuration

`AddCepWeb` accepts an optional `Action<CepOptions>`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PostgresConnectionString` | `string?` | `null` | Npgsql connection string; activates EF Core + auto-migration. |
| `SqlServerConnectionString` | `string?` | `null` | SQL Server connection string; activates EF Core + auto-migration (only one DB provider is used; Postgres takes precedence). |
| `Schema` | `string?` | `"dbo"` | Database schema for CEP tables. |
| `AutoMigrateDatabase` | `bool` | `true` | When `true`, `CepConfigDatabaseMigrator` runs `MigrateAsync` on startup. |

When neither connection string is set, `InMemoryCepConfigRepository` is registered instead of EF Core and no hosted service is started.

## API Reference

| Type | Purpose |
|------|---------|
| `CepEngine<T>` | Core in-memory engine; manages sorted event lists, TTL eviction, and sliding/tumbling window reads per correlation key. |
| `CepWindow<T>` | High-level runtime wrapper that binds a `CepConfig` to a typed payload and a key selector; delegates to `CepEngine<T>`. |
| `CepWindowBuilder` | Static entry point; `Named(...)` starts a `CepConfigBuilder`, `For<T>(config)` starts a `CepWindowRuntimeBuilder<T>`. |
| `CepConfigBuilder` | Fluent builder for `CepConfig`; methods: `Sliding`, `Tumbling`, `KeepEventsFor`, `CorrelateBy`, `ForTenant`, `Describe`, `WithMetadata`, `Build`. |
| `CepWindowRuntimeBuilder<T>` | Fluent builder for `CepWindow<T>`; requires `CorrelateBy(Func<T, string>)` before calling `Build`. |
| `CepConfig` | Immutable record describing a persisted window: `Id`, `TenantId`, `Name`, `WindowType`, `WindowSize`, `TimeToLive`, `CorrelationKey`, `Metadata`. |
| `CepEvent<T>` | Positional record returned from window evaluations: `Key`, `Timestamp`, `Value`. |
| `WindowType` | Enum — `Sliding` or `Tumbling`. |
| `CepOptions` | DI options class; see Configuration table above. |
| `ICepConfigRepository` | Persistence abstraction: `ListAsync`, `GetAsync`, `SaveAsync`, `DeleteAsync`. |
| `CepController` | ASP.NET Core controller registered automatically by `AddCepWeb`; routes under `api/v1/rule-engine/cep`. |

## Samples

No dedicated sample project exists for this package. Use the Quick Start snippet above as a starting point. The controller simulation endpoint (`POST /api/v1/rule-engine/cep/{id}/simulate`) can be called directly once configurations are stored.

## Compatibility

- Target framework: `net8.0`
- Requires `Microsoft.AspNetCore.App` (ASP.NET Core shared framework)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core`](../Muonroi.Core/) — Core helpers and services consumed internally (`MDateTimeService`, `MJsonSerializeService`, `MGuard`)
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — Shared interfaces (`ISystemExecutionContextAccessor`, `IUiEngineManifestContributor`) used by the CEP controller and contributor
- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — EF Core base infrastructure used by `CepConfigDbContext`
- [`Muonroi.RuleEngine.SourceGenerators`](../Muonroi.RuleEngine.SourceGenerators/) — Source generators and analyzers for the broader rule engine; complement CEP-driven rules with generated rule classes

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) at the repository root.
