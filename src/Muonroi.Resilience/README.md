# Muonroi.Resilience

> Polly-based resilience patterns for Muonroi services: retry, circuit breaker, and timeout policies with built-in OpenTelemetry integration.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Resilience.svg)](https://www.nuget.org/packages/Muonroi.Resilience/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package wires up a production-ready Polly v8 resilience pipeline under the well-known name `"muonroi-standard"` and emits retry telemetry via `MuonroiMetrics.RetryAttemptCount` (`muonroi.retry.attempts`). It also exposes `PolicyHandler` for building typed `ResiliencePipeline<T>` instances directly when DI injection is not suitable. The package depends on `Muonroi.Core.Abstractions`, `Muonroi.Logging.Abstractions`, and `Muonroi.Observability`.

## Installation

```bash
dotnet add package Muonroi.Resilience --prerelease
```

## Quick Start

Register the standard pipeline in `Program.cs`:

```csharp
using Muonroi.Resilience;

builder.Services.AddMuonroiResilience();
```

Resolve and execute via `ResiliencePipelineProvider<string>`:

```csharp
using Polly.Registry;

public class WeatherApiClient(
    ResiliencePipelineProvider<string> pipelines,
    IHttpClientFactory factory)
{
    public async Task<string> GetForecastAsync(CancellationToken ct)
    {
        ResiliencePipeline pipeline = pipelines.GetPipeline("muonroi-standard");

        return await pipeline.ExecuteAsync(async token =>
        {
            HttpClient client = factory.CreateClient("weather-api");
            HttpResponseMessage response = await client.GetAsync("forecast?latitude=21&longitude=105", token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token);
        }, ct);
    }
}
```

You can also register additional named pipelines alongside `"muonroi-standard"`:

```csharp
using Polly;
using Polly.Retry;
using Muonroi.Core.Abstractions.Exceptions;

builder.Services.AddResiliencePipeline("payment-gateway", (pipelineBuilder, context) =>
{
    pipelineBuilder
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<MTransientException>()
                .Handle<HttpRequestException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(2)
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});
```

## Features

- **`AddMuonroiResilience()`** — registers the `"muonroi-standard"` named pipeline with retry, circuit breaker, and timeout chained in that order.
- **Retry** — up to 3 attempts, exponential back-off with jitter, 1 s base delay; handles `MTransientException` and `HttpRequestException`; each attempt increments the `muonroi.retry.attempts` OTel counter.
- **Circuit breaker** — opens when the failure ratio reaches 50% over a 30 s / 5-call minimum window; stays open for 30 s; handles any `Exception`.
- **Timeout** — 10 s hard timeout per execution attempt.
- **`PolicyHandler`** — builds typed `ResiliencePipeline<T>` instances with the same defaults when you need a pipeline outside of the `ResiliencePipelineProvider` abstraction.
- **OTel metrics** — retry attempts reported via `MuonroiMetrics.RetryAttemptCount` (meter `Muonroi.Ecosystem.Core`, instrument `muonroi.retry.attempts`), tagged with `exception.type`.

## Configuration

There are no `appsettings.json` keys for the standard pipeline — all defaults are hardcoded in `MuonroiResilienceExtensions.AddMuonroiResilience()`. To customise thresholds, register additional named pipelines with `AddResiliencePipeline(name, ...)` from Polly.Extensions directly.

## API Reference

| Type | Purpose |
|------|---------|
| `MuonroiResilienceExtensions` | Static class; provides `AddMuonroiResilience(this IServiceCollection)` |
| `PolicyHandler` | Builds typed `ResiliencePipeline<T>` via `CreateDefaultPipeline<T>(serviceName)` with retry + circuit breaker + timeout |

## Samples

- [Quickstart.Resilience](../../samples/Quickstart.Resilience/) — full ASP.NET Core API demonstrating `AddMuonroiResilience()`, custom named pipelines, `PolicyHandler`, `MTransientException` retry triggers, circuit-breaker fast-fail, and OTel metrics

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `MTransientException` handled by the retry predicate
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — provides `IMLog<T>` used for retry and circuit-breaker event logging
- [`Muonroi.Observability`](../Muonroi.Observability/) — provides `MuonroiMetrics.RetryAttemptCount` OTel counter

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
