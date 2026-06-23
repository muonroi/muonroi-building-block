# Muonroi.Http

> HTTP client utilities for Muonroi: resilient typed client base, bearer-token propagation, and correlation-id / API-key header forwarding.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Http.svg)](https://www.nuget.org/packages/Muonroi.Http/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Http` provides the three building blocks you need to write outbound HTTP clients in a Muonroi service: an abstract base class that runs every request through a Polly v8 `ResiliencePipeline`, and two `DelegatingHandler` implementations that automatically attach bearer tokens and forward correlation IDs / API keys. There is no bespoke `AddX()` extension — registration follows the standard ASP.NET `AddHttpClient(...).AddHttpMessageHandler<T>()` pattern.

## Installation

```bash
dotnet add package Muonroi.Http --prerelease
```

## Quick Start

```csharp
// Program.cs
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Http.Http;
using Muonroi.Logging;

// 1. IMLog<T> logging (required by BaseApiService and AuthenticateHeaderHandler)
builder.Services.AddLogging(lb => lb.AddMuonroiLogging());

// 2. Auth context — drives the correlation-id + bearer handlers
builder.Services.AddScoped<IAuthenticateInfoContext>(_ =>
    new MAuthenticateInfoContext(isAuthenticated: false));

// 3. Register the Muonroi DelegatingHandlers
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddTransient<AuthenticateHeaderHandler>();

// 4. Named client with both handlers in the pipeline
builder.Services.AddHttpClient("my-api", client =>
    {
        client.BaseAddress = new Uri("https://api.example.com/");
    })
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthenticateHeaderHandler>();

// 5. Your typed client derived from BaseApiService
builder.Services.AddScoped<MyApiClient>();
```

Define the typed client:

```csharp
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Http.Http;
using Muonroi.Logging.Abstractions;
using Polly;
using Polly.Retry;

public sealed class MyApiClient(
    IHttpClientFactory httpClientFactory,
    IAuthenticateInfoContext authContext,
    IMLog<BaseApiService> logger)
    : BaseApiService(httpClientFactory, authContext, logger)
{
    private static readonly ResiliencePipeline<HttpResponseMessage> Pipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200)
            })
            .Build();

    public Task<OrderDto> GetOrderAsync(int id, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{id}");
        return SendAsync<OrderDto>("my-api", request, Pipeline, ct);
    }
}
```

## Features

- **`BaseApiService`** — abstract base class that executes outbound HTTP requests through a caller-supplied Polly v8 `ResiliencePipeline<HttpResponseMessage>`, calls `EnsureSuccessStatusCode()`, and deserializes the JSON body to `TResponse`.
- **`CorrelationIdHandler`** — `DelegatingHandler` that reads `CorrelationId` and `ApiKey` from `IAuthenticateInfoContext` and forwards them as `X-Correlation-Id` / `X-Api-Key` headers on every outgoing request.
- **`AuthenticateHeaderHandler`** — `DelegatingHandler` that reads `IsAuthenticated` and `GetAccessToken()` from `IAuthenticateInfoContext` and attaches an `Authorization: Bearer <token>` header when the context is authenticated.
- **No bespoke DI extension** — wire the handlers via the standard `AddHttpClient(...).AddHttpMessageHandler<T>()` pipeline so they compose freely with other Polly policies registered at the `IHttpClientBuilder` level.

## Configuration

There is no dedicated options class. The handlers are stateless and draw all runtime data from the ambient `IAuthenticateInfoContext` (scoped, injected by the DI container per request).

Register in the DI container as shown in the Quick Start:

| Step | Call |
|------|------|
| Logging | `AddLogging(lb => lb.AddMuonroiLogging())` |
| Auth context | Register `IAuthenticateInfoContext` (scoped) |
| Handlers | `AddTransient<CorrelationIdHandler>()` and `AddTransient<AuthenticateHeaderHandler>()` |
| Named client | `AddHttpClient("name", ...).AddHttpMessageHandler<CorrelationIdHandler>().AddHttpMessageHandler<AuthenticateHeaderHandler>()` |
| Typed client | Inherit `BaseApiService`; inject `IHttpClientFactory`, `IAuthenticateInfoContext`, `IMLog<BaseApiService>` |

## API Reference

| Type | Purpose |
|------|---------|
| `BaseApiService` | Abstract base for typed HTTP clients. Exposes `SendAsync<TResponse>(clientName, request, pipeline, ct)` which runs through the given Polly pipeline and deserializes JSON. |
| `CorrelationIdHandler` | `DelegatingHandler`. Appends `X-Correlation-Id` and `X-Api-Key` headers from `IAuthenticateInfoContext`. |
| `AuthenticateHeaderHandler` | `DelegatingHandler`. Appends `Authorization: Bearer <token>` from `IAuthenticateInfoContext` when `IsAuthenticated` is `true`. |

## Samples

- [Quickstart.Http](../../samples/Quickstart.Http/) — End-to-end example: typed client over JSONPlaceholder, both handlers in the pipeline, Swagger UI included.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `IAuthenticateInfoContext` consumed by the handlers.
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — defines `IMLog<T>` used by `BaseApiService` and `AuthenticateHeaderHandler`.
- [`Muonroi.Resilience`](../Muonroi.Resilience/) — Polly pipeline helpers used alongside `BaseApiService.SendAsync`.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for full terms.
