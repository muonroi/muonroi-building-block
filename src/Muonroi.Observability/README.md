# Muonroi.Observability

> OpenTelemetry tracing, metrics, and structured Serilog logging wired for tenant-aware Muonroi services in a single `AddObservability` call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Observability.svg)](https://www.nuget.org/packages/Muonroi.Observability/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package bootstraps OpenTelemetry (OTLP traces + metrics) and Serilog (console, file, OTLP sink) for any Muonroi service.
It enriches every trace span and log event with the current tenant ID, user ID, and correlation ID from `ISystemExecutionContextAccessor`.
Other Muonroi packages publish their `ActivitySource` and `Meter` names through the `ITelemetryDescriptor` contract; `AddObservability` discovers all registered descriptors at startup and wires them automatically — no per-package plumbing required.

## Installation

```bash
dotnet add package Muonroi.Observability --prerelease
```

## Quick Start

Register observability in `Program.cs` (or `Startup.cs`):

```csharp
using Muonroi.Observability;

var builder = WebApplication.CreateBuilder(args);

// Wire OTel tracing + metrics (reads "OpenTelemetry" config section)
builder.Services.AddObservability(builder.Configuration);

// Wire Serilog with tenant/correlation enrichment
builder.Host.UseSerilog((ctx, services, cfg) =>
    MSerilogAction.Configure(ctx, services, cfg, useConsole: true));
```

`appsettings.json`:

```json
{
  "OpenTelemetry": {
    "ServiceName": "my-service",
    "OtlpEndpoint": "http://localhost:4317"
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "OpenTelemetry": {
      "Endpoint": "http://localhost:4317",
      "Protocol": "Grpc"
    },
    "File": {
      "Path": "logs/service-.json"
    }
  }
}
```

## Features

- **Single-call setup** — `AddObservability` configures ASP.NET Core, HTTP client, gRPC, MassTransit, and runtime instrumentation in one call.
- **Tenant-aware spans** — `TenantActivityEnricher` stamps `tenant.id` on every `Activity` via `ISystemExecutionContextAccessor`.
- **Tenant-aware logs** — `TenantIdEnricher` appends `TenantId`, `UserId`, `CorrelationId`, and `SourceType` Serilog properties.
- **Auto-discovery of package descriptors** — any `ITelemetryDescriptor` implementation found in loaded assemblies has its `ActivitySourceNames` and `MeterNames` registered automatically.
- **Central ecosystem meter** — `MuonroiMetrics` exposes `muonroi.guard.violations`, `muonroi.exception.total`, and `muonroi.retry.attempts` counters on the `Muonroi.Ecosystem.Core` meter.
- **Compat telemetry helpers** — self-contained `ActivitySource` + metric helpers for gRPC (`GrpcRuntimeTelemetry`), message bus (`MessageBusRuntimeTelemetry`), and distributed cache (`DistributedCacheRuntimeTelemetry`) avoid cross-package circular references.
- **Serilog sinks** — console (opt-in), JSON file (rolling), and OTLP; all configured from `IConfiguration`.
- **Exception tagging** — `MuonroiTraceProcessor.TagException(activity, ex)` stamps `exception.category` and `exception.error_code` from `MException` onto the active span.

## Configuration

### `OpenTelemetryConfigs` (section `"OpenTelemetry"`)

| Property | Type | Description |
|----------|------|-------------|
| `ServiceName` | `string?` | Service name reported to the OTel backend. Defaults to `"MuonroiService"` when null. |
| `OtlpEndpoint` | `string?` | OTLP endpoint URI. Traces and metrics are exported only when this is non-empty. |

### Serilog sinks (via `MSerilogAction.Configure`)

`MSerilogAction.Configure` reads two optional sub-sections:

| Section | Key fields |
|---------|-----------|
| `Serilog:OpenTelemetry` | `Endpoint` (URI), `Protocol` (`Grpc` or `Http`/`HttpProtobuf`), `ResourceAttributes` (comma-separated `key=value` pairs) |
| `Serilog:File` | `Path` (file path; directory is created automatically) |

All standard `Serilog` configuration keys (minimum levels, filters, etc.) are also read from `IConfiguration` via `ReadFrom.Configuration`.

## API Reference

| Type | Purpose |
|------|---------|
| `OtelSetup.AddObservability(services, configuration)` | Extension method — registers OTel tracing, metrics, and `TenantIdEnricher`. |
| `MSerilogAction.Configure(ctx, services, cfg, useConsole)` | Configures Serilog sinks and enrichers for `UseSerilog`. |
| `OpenTelemetryConfigs` | POCO bound from the `"OpenTelemetry"` config section. |
| `TenantIdEnricher` | Serilog `ILogEventEnricher` — adds `TenantId`, `UserId`, `CorrelationId`, `SourceType`. |
| `MuonroiMetrics` | Static class exposing the `Muonroi.Ecosystem.Core` `Meter` and ecosystem-wide counters. |
| `MuonroiTraceProcessor` | OTel `BaseProcessor<Activity>` — call `TagException(activity, ex)` to annotate spans with `MException` details. |
| `GrpcRuntimeTelemetry` | Compat helper: `ActivitySource` + `TrackRequest(...)` for gRPC spans/metrics. |
| `MessageBusRuntimeTelemetry` | Compat helper: `ActivitySource` + `TrackOperation(...)` for message-bus spans/metrics. |
| `DistributedCacheRuntimeTelemetry` | Compat helper: `ActivitySource` + `TrackOperation(...)` for cache spans/metrics. |
| `MLogEntry` | Structured log entry model with tenant, correlation, elapsed time, and error code fields. |
| `ILogSanitizer` / `LogSanitizer` | Redacts sensitive fields from a log data dictionary before emission. |
| `BootstrapMethod` | Enum controlling bootstrap-phase logging behavior (`Silent`, `Failure`, `None`). |

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `ITelemetryDescriptor` and `ISystemExecutionContextAccessor` consumed here
- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — tenant context resolved for span/log enrichment
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — license feature gate (`FreeTierFeatures.Premium.AuditTrail`) checked during registration

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
