# Muonroi.Resilience

> Polly-based resilience patterns optimized for Muonroi with built-in telemetry tracking.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Resilience.svg)](https://www.nuget.org/packages/Muonroi.Resilience/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Resilience` package provides a standardized approach to fault tolerance in distributed systems using Polly. In a microservices architecture, transient failures (e.g., brief network outages, temporary database unavailability) are common. This package abstracts the setup of robust Retry, Circuit Breaker, Timeout, and Bulkhead policies.

Crucially, it extends standard Polly functionality by integrating deeply with Muonroi's telemetry ecosystem. State transitions (e.g., a Circuit Breaker opening) and execution retries are automatically logged and emitted as OpenTelemetry metrics, ensuring full visibility into the stability of downstream dependencies.

Use this package when your application needs to reliably communicate with external APIs, databases, or message brokers over unreliable networks.

## Features

- **Pre-configured Resilience Pipelines**: Offers `AddMuonroiResilience()` which registers standard policy wrappers tailored for common scenarios.
- **Retry Policies**: Automatically retries failed operations using configurable exponential backoff strategies to avoid overwhelming struggling services, explicitly handling `MTransientException`.
- **Circuit Breaker**: Detects when a downstream service is fundamentally unhealthy and fails fast, preventing cascading failures across the system.
- **Timeouts**: Ensures that slow dependencies don't consume all available threads in the calling application via `AddTimeout`.
- **Telemetry Integration**: Policy executions and state changes automatically emit OTel events and structured logs via `MuonroiMetrics.RetryAttemptCount` and `IMLog`.

## Installation

```bash
dotnet add package Muonroi.Resilience
```

## Quick Start

### Basic Configuration

Register the standard Muonroi resilience policies in your dependency injection container. This will make the `ResiliencePipelineProvider` available to your services.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Register standard resilience pipeline ("muonroi-standard")
builder.Services.AddMuonroiResilience();
```

### Manual Policy Execution

If you need to wrap arbitrary code (e.g., a database connection attempt or a file system operation) in a resilience policy, you can resolve the `ResiliencePipelineProvider`.

```csharp
using Polly.Registry;
using System.Threading.Tasks;

public class DatabaseInitializer
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public DatabaseInitializer(ResiliencePipelineProvider<string> pipelineProvider)
    {
        _pipelineProvider = pipelineProvider;
    }

    public async Task InitializeAsync()
    {
        // Get the predefined 'muonroi-standard' pipeline
        var pipeline = _pipelineProvider.GetPipeline("muonroi-standard");

        // Execute the unstable operation within the pipeline
        await pipeline.ExecuteAsync(async token => 
        {
            await RunMigrationLogicAsync();
        });
    }
}
```

## API Reference

### Configuration Extensions

- `MuonroiResilienceExtensions`: Contains `AddMuonroiResilience()` which populates the DI container with `ResiliencePipelineBuilder` configurations standard to the ecosystem.

### Pipeline Handlers

- `PolicyHandler`: Internal handlers that wire Polly's events to Muonroi's `IMLog` and OpenTelemetry metrics abstractions.

## Ecosystem Combinations

### + Muonroi.Observability â†’ Resiliency Metrics
By combining these, Polly events such as retries are automatically intercepted and exported via `MuonroiMetrics.RetryAttemptCount`, allowing operators to set up Grafana alerts when internal services begin to destabilize before complete failure.

### + Muonroi.Logging.Abstractions â†’ Structured Resilience Logs
The resilience pipeline extracts `IMLog` from the `IServiceProvider` at runtime to emit structured logs containing the precise exception types and retry attempts when a transient error is caught and retried.

### Full Resilience Stack
```csharp
builder.Services
    .AddMuonroiResilience()
    .AddMuonroiObservability(config);
```

## Samples

- [`Quickstart.Resilience`](../../samples/Quickstart.Resilience)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
