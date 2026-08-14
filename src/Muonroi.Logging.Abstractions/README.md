# Muonroi.Logging.Abstractions

> Contracts-only package that defines the `IMLog<T>`, `IMLogContext`, and log-property conventions used across the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Logging.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Logging.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships the logging interfaces and property-key constants that consumer libraries depend on at compile time — no runtime behavior is included. Services that want structured logging take `IMLog<T>` by constructor injection; middleware and request handlers push ambient properties through `IMLogContext`. The concrete implementation lives in [`Muonroi.Logging`](../Muonroi.Logging/).

## Installation

```bash
dotnet add package Muonroi.Logging.Abstractions --prerelease
```

## Quick Start

This package defines contracts only. Implement or inject `IMLog<T>` in your service; for tests or AOT scenarios you can supply a no-op implementation without taking a dependency on any logging provider:

```csharp
using Muonroi.Logging.Abstractions;
using Microsoft.Extensions.Logging;

// Minimal no-op implementation — useful in tests or AOT hosts.
internal sealed class NoOpLog<T> : IMLog<T>
{
    private sealed class NullScope : IMLogContextScope
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }

    public IMLogContextScope BeginProperty(string key, object? value) => NullScope.Instance;
    public void Info(string messageTemplate, params object?[] args) { }
    public void Warn(string messageTemplate, params object?[] args) { }
    public void Error(Exception? ex, string messageTemplate, params object?[] args) { }
    public void Debug(string messageTemplate, params object?[] args) { }
    public void InfoTrace(string messageTemplate, params object?[] args) { }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) { }
}

// Register with open-generic DI so every IMLog<T> request is satisfied:
services.AddSingleton(typeof(IMLog<>), typeof(NoOpLog<>));
```

For full structured logging in an ASP.NET Core application, use `AddMuonroiLogging()` from [`Muonroi.Logging`](../Muonroi.Logging/):

```csharp
builder.Logging.AddMuonroiLogging();
```

## Features

- `IMLog` / `IMLog<T>` — structured logging interface extending `ILogger` / `ILogger<T>` with shorthand methods (`Info`, `Warn`, `Error`, `Debug`, `InfoTrace`) and `BeginProperty` scoping.
- `IMLogFactory` — factory interface for creating `IMLog` instances by type or category name.
- `IMLogContext` — ambient property bag; push key-value pairs into the logging context with `PushProperty` / `PushProperties`.
- `IMLogContextScope` — disposable scope returned by `BeginProperty` / `PushProperty`; disposing it removes the property from the context.
- `IInterceptedLogWriter` — interceptor abstraction for custom logging sinks; the signature takes `CategoryName` for contextual routing.
- `LogEvent` — performance-oriented model in `Muonroi.Logging.Abstractions.Models` implementing `Reset()` for aggressive `ObjectPool` reuse.
- `LogPropertyConventions` — static constants for standard structured-log property keys: `TenantId`, `UserId`, `CorrelationId`, `TraceSessionId`, `RuleCode`, `RequestName`.

## API Reference

| Type | Purpose |
|------|---------|
| `IMLog` | Base structured-logging interface; extends `ILogger`. Adds `Info`, `Warn`, `Error`, `Debug`, `InfoTrace`, and `BeginProperty`. |
| `IMLog<T>` | Generic variant; extends both `IMLog` and `ILogger<T>`. Inject this in application services. |
| `IMLogFactory` | Creates `IMLog` and `IMLog<T>` instances by type parameter or category name string. |
| `IMLogContext` | Ambient context bag. `PushProperty(key, value)` and `PushProperties(dict)` attach properties to all log events within the returned scope. |
| `IMLogContextScope` | Disposable returned by `BeginProperty` / `PushProperty`. Dispose to remove the pushed property from the ambient context. |
| `IInterceptedLogWriter` | Interceptor abstraction for custom logging sinks, taking `CategoryName` in its signature for contextual routing. |
| `LogEvent` | Performance-oriented model (in `Models`) implementing `Reset()` for aggressive `ObjectPool` reuse to minimize allocation overhead. |
| `LogPropertyConventions` | String constants: `TenantId`, `UserId`, `CorrelationId`, `TraceSessionId`, `RuleCode`, `RequestName`. |

## Samples

- [Quickstart.Logging](../../samples/Quickstart.Logging/) — ASP.NET Core API that demonstrates `AddMuonroiLogging()`, `IMLog<T>` injection, and `IMLogContext.PushProperty` scopes.
- [Muonroi.Pdf.AotSample](../../samples/Muonroi.Pdf.AotSample/) — shows a no-op `IMLog<T>` open-generic registration for AOT / trimmed hosts that cannot use the full Serilog provider.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Logging`](../Muonroi.Logging/) — concrete implementation; registers `MLog<T>`, `MLogContext`, `MLogFactory`, and `ILogScopeFactory` via `builder.Logging.AddMuonroiLogging()`.

## Ecosystem Combinations

### + Logging → Implementation Behind IMLog
`Muonroi.Logging` implements the `IMLog<T>` and `IMLogFactory` contracts defined here. Swap to a different implementation without changing any call sites.

### + Tenancy → IMLog Auto-Enriched with TenantId
When `ITenantContext` is active, all `IMLog<T>` implementations automatically scope log entries with the current tenant ID.

### + All Packages → Universal Logging Contract
Every Muonroi package accepts `IMLog<T>` instead of `ILogger<T>`, enabling consistent structured logging across the entire ecosystem.

## Samples
- [`Quickstart.Logging.Abstractions`](../../samples/Quickstart.Logging.Abstractions)

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
