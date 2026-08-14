# Muonroi.Observability

> OpenTelemetry and Serilog integration for Muonroi: unified metrics, distributed tracing, and structured logging.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Observability.svg)](https://www.nuget.org/packages/Muonroi.Observability/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Observability` package provides a turnkey solution for instrumenting applications within the Muonroi ecosystem. It opinionates the setup of OpenTelemetry (OTel) for traces and metrics, and Serilog for structured logging, ensuring that telemetry signals are correctly correlated across distributed microservices.

A key challenge in multi-tenant systems is ensuring that telemetry data is context-aware without explicitly passing IDs through every method call. This package integrates deeply with `Muonroi.Tenancy.Abstractions` to automatically enrich logs, spans, and metrics with Tenant IDs, ensuring that dashboards and alerts can be segmented by tenant out of the box.

Use this package in the entry point of any API, worker, or web application to establish a robust observability baseline.

## Features

- **OpenTelemetry Bootstrapping**: `AddMuonroiObservability()` configures OTel Tracing and Metrics to export via OTLP.
- **Tenant Context Enrichment**: `TenantIdEnricher` automatically appends `TenantId` to Serilog logs, while OTel processors append it to tracing spans.
- **Log Sanitization**: Includes `ILogSanitizer` and `LogSanitizer` to automatically mask or redact sensitive information (e.g., PII, passwords) before logs are flushed.
- **Custom Metrics**: Exposes `MuonroiMetrics` for recording domain-specific measurements (e.g., active tenants, rule engine evaluations, quota usage).

## Installation

```bash
dotnet add package Muonroi.Observability
```

## Quick Start

### Configuring Observability in `Program.cs`

```csharp
using Muonroi.Observability;
using Muonroi.Observability.Logging;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
builder.Host.UseMuonroiSerilog((context, loggerConfig) =>
{
    loggerConfig.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}");
});

// 2. Configure OpenTelemetry
builder.Services.AddMuonroiObservability(builder.Configuration);
```

### Recording Custom Metrics

Inject `MuonroiMetrics` to record domain-specific counters or histograms.

```csharp
using Muonroi.Observability.OpenTelemetry;

public class OrderService
{
    private readonly MuonroiMetrics _metrics;

    public OrderService(MuonroiMetrics metrics)
    {
        _metrics = metrics;
    }

    public void ProcessOrder(Order order)
    {
        // Record the metric. The tenant ID will be captured automatically by the OTel context.
        _metrics.OrderProcessedCounter.Add(1, new KeyValuePair<string, object?>("order.type", order.Type));
    }
}
```

## Ecosystem Combinations

### + Muonroi.Tenancy.Core â†’ Tenant-Aware Tracing
`TenantIdEnricher` securely tags all spans/logs/metrics with the resolved `tenant.id` from `ISystemExecutionContextAccessor`, enabling per-tenant telemetry isolation without passing context manually.

### + Muonroi.Diagnostics â†’ Embedded Diagnostic Sessions
Log entries emitted within a diagnostic session are attached to the current trace node, creating a combined log+trace view, directly exportable via OTLP.

### + Muonroi.Messaging.MassTransit â†’ Distributed Message Correlation
Correlates log entries to OTel trace spans via `TraceId`/`SpanId` across message bus boundaries.

### Full Observability Stack
```csharp
builder.Services
    .AddMuonroiObservability(config)
    .AddTenantContext(config);
```

## Samples

- [`Quickstart.Observability`](../../samples/Quickstart.Observability)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
