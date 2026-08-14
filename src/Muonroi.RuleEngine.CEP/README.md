# Muonroi.RuleEngine.CEP

> Complex Event Processing integration for Muonroi Rule Engine. Pattern-based event correlation and temporal windowing.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.CEP.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.CEP/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.RuleEngine.CEP` package provides Complex Event Processing capabilities to the Muonroi Rule Engine ecosystem. It allows you to process streams of real-time events, aggregate them over time-based windows, and detect complex patterns such as repeated failures, threshold breaches, or specific event sequences. 

This is particularly useful for use cases like fraud detection (e.g., multiple failed logins within a specific time frame), rate limiting, and real-time monitoring, where individual events need to be evaluated in the context of other recent events.

## Features

- **Windowed Aggregation**: Supports both `WindowType.Sliding` and `WindowType.Tumbling` time windows for event grouping.
- **Out-of-Order Handling**: Robust tracking of events in `CepEngine` based on their true temporal timestamp, properly handling out-of-order delivery.
- **TTL Support**: Automatic eviction of old events based on configurable Time-To-Live via `CepConfigBuilder`, preventing memory leaks on long-running windows.
- **Fluent Configuration**: Use `CepWindowBuilder` to easily define windows and correlation keys for your data stream.
- **Built-in Observability**: Comprehensive OpenTelemetry metrics (`cep.window.evaluate`, `cep.window.event_count`) and activity tracing out of the box.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.CEP
```

## Quick Start

Create a window using `CepWindowBuilder` specifying the payload type, window size, and window type, then add events to it.

```csharp
using Muonroi.RuleEngine.CEP;
using Muonroi.RuleEngine.CEP.Builder;

// 1. Define the window configuration
var config = CepWindowBuilder.Named("FailedLogins")
    .Sliding(TimeSpan.FromMinutes(1))
    .KeepEventsFor(TimeSpan.FromMinutes(5))
    .Build();

// 2. Create the runtime window for your payload
var window = CepWindowBuilder.For<LoginAttempt>(config)
    .CorrelateBy(attempt => attempt.UserId)
    .Build();

// 3. Add an event and get back all events currently in that key's active window
var recentEvents = window.Add(
    new LoginAttempt { UserId = "user-123", Status = "Failed" },
    DateTime.UtcNow
);

if (recentEvents.Count >= 3)
{
    Console.WriteLine("Pattern detected: 3 or more failed logins in the last minute!");
}
```

## Ecosystem Combinations

### + Muonroi.Messaging.MassTransit â†’ Event Stream Ingestion
MassTransit consumers feed real-time events into CEP windows. High-volume event streams from the broker populate `CepEngine` windows without polling, ensuring low-latency pattern detection for integration events.

### + Muonroi.Tenancy.Core â†’ Per-Tenant Isolated Windows
By passing `tenantId` into `CepEngine.AddEvent`, each tenant has its own isolated CEP window state. Events from tenant A never mix with tenant B â€” critical for SaaS fraud detection.

### + Muonroi.RuleEngine.Core â†’ Pattern-Triggered Rule Execution
When a CEP window pattern matches (e.g., 3 failed logins in 60s), the events can be passed to a `RuleOrchestrator` execution to evaluate a comprehensive fraud response ruleset.

### Full CEP Fraud Detection Stack
```csharp
builder.Services
    .AddCepWeb(config)                              // API endpoints for config
    .AddTenantContext(config)                       // isolated per-tenant
    .AddRuleEngine<FraudContext>()                  // action on pattern match
    .AddMassTransit(x => x.AddConsumers(...));      // event source
```

## Samples
- [`Quickstart.CEP`](../../samples/Quickstart.CEP)
- [`FraudDetection`](../../samples/FraudDetection)

## License
Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
