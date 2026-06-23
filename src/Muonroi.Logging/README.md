# Muonroi.Logging

> Structured logging implementation for Muonroi: `IMLog<T>` wrapper, ambient property scopes, and a `Microsoft.Extensions.Logging` bridge that auto-enriches every log line with tenant, user, and correlation context.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Logging.svg)](https://www.nuget.org/packages/Muonroi.Logging/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Logging` wires a thin, opinionated layer on top of `Microsoft.Extensions.Logging`. It registers `IMLog<T>` — a drop-in structured logger with `Info`/`Warn`/`Error`/`Debug`/`InfoTrace` helpers — and automatically pushes `TenantId`, `UserId`, and `CorrelationId` from the ambient `ISystemExecutionContextAccessor` into every log scope. It also provides `IMLogContext` for pushing arbitrary key-value properties, `IMLogFactory` for creating loggers by type or category name, and `ILogScopeFactory` for building scopes from property dictionaries.

## Installation

```bash
dotnet add package Muonroi.Logging --prerelease
```

## Quick Start

```csharp
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register IMLogContext, IMLog<T>, IMLogFactory, and ILogScopeFactory.
builder.Logging.AddMuonroiLogging();

// IMLog<T> auto-enriches log lines with TenantId/UserId/CorrelationId from the
// ambient execution context. AddCoreServices() provides this automatically;
// register the default accessor explicitly when using logging in isolation.
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

builder.Services.AddControllers();
WebApplication app = builder.Build();
app.MapControllers();
app.Run();
```

Inject `IMLog<T>` into any service or controller:

```csharp
using Muonroi.Logging.Abstractions;

public sealed class OrderService(IMLog<OrderService> log)
{
    public void Process(int orderId)
    {
        log.Debug("Starting order {OrderId}", orderId);

        using IMLogContextScope scope = log.BeginProperty("OrderId", orderId);
        log.Info("Processing order inside scoped property");

        try
        {
            // ...
        }
        catch (Exception ex)
        {
            log.Error(ex, "Order {OrderId} failed", orderId);
        }
    }
}
```

Create a logger for an arbitrary category name via `IMLogFactory`:

```csharp
public sealed class PaymentProcessor(IMLogFactory logFactory)
{
    public void Charge(string provider)
    {
        IMLog providerLog = logFactory.CreateLogger(provider);
        providerLog.Info("Charging via {Provider}", provider);
    }
}
```

## Features

- `IMLog<T>` — category-typed structured logger with `Info`, `Warn`, `Error`, `Debug`, and `InfoTrace` helper methods, all delegating to the inner `ILogger<T>`
- Automatic ambient enrichment — every log call acquires a scope carrying `TenantId`, `UserId`, and `CorrelationId` from `ISystemExecutionContextAccessor`
- `IMLogContext.PushProperty` / `PushProperties` — push arbitrary key-value pairs as structured log scopes that are removed on `Dispose`
- `IMLog.BeginProperty` — shorthand on `IMLog` for `IMLogContext.PushProperty`
- `IMLogFactory` — creates `IMLog<T>` or `IMLog` by category name; useful for dynamic or per-provider loggers
- `ILogScopeFactory` — creates scopes from `IReadOnlyDictionary<string, object?>` for bulk property injection
- `LogPropertyConventions` — well-known property key constants (`TenantId`, `UserId`, `CorrelationId`, `TraceSessionId`, `RuleCode`, `RequestName`)
- Trace session integration — when `IMTraceContext` is present, `Info`/`Warn`/`Error`/`Debug`/`InfoTrace` also record messages to the active trace session

## Configuration

Call `AddMuonroiLogging()` on the `ILoggingBuilder` (typically `builder.Logging`):

```csharp
builder.Logging.AddMuonroiLogging();
```

This registers the following singletons:

| Registration | Implementation |
|---|---|
| `IMLogContext` | `MLogContext` |
| `IMLog<>` (open generic) | `MLog<>` |
| `IMLogFactory` | `MLogFactory` |
| `ILogScopeFactory` | `MLogScopeFactory` |

No `appsettings.json` section is required. Log level filtering and sinks are controlled by the standard `Microsoft.Extensions.Logging` configuration already present in your host.

`MLog<T>` requires `ISystemExecutionContextAccessor` to be registered. When using `Muonroi.Core` in the same host, `AddCoreServices()` registers it automatically. For isolated setups (tests, samples), register the default implementation manually:

```csharp
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
```

## API Reference

| Type | Purpose |
|------|---------|
| `IMLog` | Base structured logger: `Info`, `Warn`, `Error`, `Debug`, `InfoTrace`, `BeginProperty` |
| `IMLog<T>` | Category-typed variant of `IMLog`; extends `ILogger<T>` |
| `IMLogFactory` | Creates `IMLog<T>` or `IMLog` by category name |
| `IMLogContext` | Pushes properties into the ambient log scope via `PushProperty` / `PushProperties` |
| `IMLogContextScope` | Disposable scope returned by `PushProperty`; removes the property on `Dispose` |
| `ILogScopeFactory` | Creates scopes from a property dictionary via `BeginScope` |
| `LogPropertyConventions` | String constants for `TenantId`, `UserId`, `CorrelationId`, `TraceSessionId`, `RuleCode`, `RequestName` |
| `MLogServiceCollectionExtensions` | `ILoggingBuilder.AddMuonroiLogging()` extension method |

## Samples

- [Quickstart.Logging](../../samples/Quickstart.Logging/) — ASP.NET Core API demonstrating `IMLog<T>` emit, `BeginProperty` scoped logging, and `IMLogFactory` category-name creation

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — contracts only (`IMLog`, `IMLog<T>`, `IMLogFactory`, `IMLogContext`, `IMLogContextScope`, `ILogScopeFactory`, `LogPropertyConventions`); reference this instead of `Muonroi.Logging` in library projects that only consume the interfaces
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `ISystemExecutionContextAccessor` and `ISystemExecutionContext` used for ambient context enrichment

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
