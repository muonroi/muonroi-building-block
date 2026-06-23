# Muonroi.Diagnostics

> Deep diagnostics and runtime tracing for Muonroi — hierarchical trace trees, rule engine instrumentation, and Roslyn-based line tracing.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Diagnostics.svg)](https://www.nuget.org/packages/Muonroi.Diagnostics/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Diagnostics` provides structured, async-flow-safe trace sessions that capture hierarchical node trees, per-node events, rule engine fact snapshots, and optional line-level variable captures. Sessions are stored either in-memory (development) or in Redis (production). The package sits on top of `Muonroi.Core.Abstractions`, implementing `IMTraceContext` and `ITraceSessionStore`.

## Installation

```bash
dotnet add package Muonroi.Diagnostics --prerelease
```

## Quick Start

```csharp
// Program.cs
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Diagnostics.Extensions;

builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
builder.Services.AddMuonroiDiagnostics(); // in-memory store
```

Inject `IMTraceContext` and open a session:

```csharp
using Muonroi.Core.Abstractions.Diagnostics;

public sealed class OrderController(IMTraceContext traceContext) : ControllerBase
{
    [HttpPost("process")]
    public IActionResult Process(string sessionId, string tenantId, string userId)
    {
        using IDisposable scope = traceContext.Begin(sessionId, tenantId, userId);

        ITraceSession session = traceContext.Current!;

        using (session.BeginNode("LoadOrder", MTraceNodeType.Handler))
        {
            session.Record("Loaded order from store", new { orderId = 42 });

            using (session.BeginNode("ValidateOrder", MTraceNodeType.Rule))
            {
                session.Record("Validation passed");
            }
        }

        MTraceSessionRecord result = session.Export();
        return Ok(result);
    }
}
```

For Redis-backed storage in production:

```csharp
// Requires IConnectionMultiplexer registered (e.g. via StackExchange.Redis)
builder.Services.AddMuonroiDiagnosticsRedis();
```

## Features

- **Hierarchical trace trees** — `BeginNode` creates parent/child spans; each node measures its own `DurationMs`
- **Structured event recording** — `Record(message, payload)` serializes arbitrary payloads as JSON onto the current node
- **Rule engine instrumentation** — `RecordFactSnapshot("before"|"after", facts)` captures input/output fact dictionaries on `Rule` nodes
- **Line-level tracing** — `RecordLineTrace` and `RecordBranchTrace` (gated by `lineTraceEnabled = true`) capture variable values and branch decisions
- **Failure marking** — `MarkFailed(reason, ex)` flags the current node and records the exception
- **Dual store backends** — `InMemoryTraceSessionStore` (zero-dependency, development) and `RedisTraceSessionStore` (24-hour TTL, sorted-set index by tenant + date)
- **Async-flow-safe ambient scope** — `MTraceSessionScope` uses `AsyncLocal` to carry the session through async continuations without thread affinity
- **Full export** — `Export()` returns a `MTraceSessionRecord` containing all nodes, events, durations, and error flags

## Configuration

### In-memory (development)

```csharp
builder.Services.AddMuonroiDiagnostics();
// Registers: IMTraceContext → MTraceContext, ITraceSessionStore → InMemoryTraceSessionStore
```

### Redis-backed (production)

```csharp
// Prerequisite: IConnectionMultiplexer must be registered
builder.Services.AddMuonroiDiagnosticsRedis();
// Registers: IMTraceContext → MTraceContext, ITraceSessionStore → RedisTraceSessionStore
```

Redis key scheme used by `RedisTraceSessionStore`:
- Session data: `trace:session:{tenantId}:{sessionId}` (default TTL: 24 h)
- Date index: `trace:sessions:{tenantId}:{yyyyMMdd}` (sorted set, score = `StartedAt.Ticks`)

### `lineTraceEnabled`

Pass `true` to `IMTraceContext.Begin` to activate line-level captures. This is off by default to avoid overhead in production.

```csharp
traceContext.Begin(sessionId, tenantId, userId, lineTraceEnabled: true);
```

## API Reference

| Type | Purpose |
|------|---------|
| `IMTraceContext` | Facade — opens sessions (`Begin`) and exposes the ambient `Current` session |
| `ITraceSession` | Active session — `BeginNode`, `Record`, `RecordFactSnapshot`, `RecordLineTrace`, `RecordBranchTrace`, `MarkFailed`, `Export` |
| `MTraceNodeType` | Enum for node classification: `MediatorRequest`, `PipelineBehavior`, `RuleSet`, `Rule`, `Handler`, `Custom` |
| `MTraceContext` | Default `IMTraceContext` implementation |
| `MTraceSessionScope` | `IDisposable` ambient scope; `MTraceSessionScope.Current` returns the active session |
| `InMemoryTraceSessionStore` | `ITraceSessionStore` backed by `ConcurrentDictionary` |
| `RedisTraceSessionStore` | `ITraceSessionStore` backed by StackExchange.Redis with TTL and sorted-set index |
| `MDiagnosticsServiceCollectionExtensions` | `AddMuonroiDiagnostics()` / `AddMuonroiDiagnosticsRedis()` |

## Samples

- [Quickstart.Diagnostics](../../samples/Quickstart.Diagnostics/) — ASP.NET Core API demonstrating `IMTraceContext.Begin`, hierarchical `BeginNode` calls, `Record`, and `Export` via a REST endpoint

## Compatibility

- Target framework: net8.0
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `IMTraceContext`, `ITraceSession`, `ITraceSessionStore`, `MTraceNodeType`, and all record types consumed by this package
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — logging contracts used alongside tracing

## License

Licensed under the [Apache License, Version 2.0](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE).
