# Muonroi.Http
> Resilient HTTP client utilities and context propagation for the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Http.svg)](https://www.nuget.org/packages/Muonroi.Http/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Http` streamlines outbound HTTP communications within the Muonroi Building Block architecture. When microservices communicate with one another, or with external APIs, maintaining distributed context (like correlation IDs, API keys, and authorization tokens) is critical. 

This package provides delegating handlers to automatically attach this contextual information to outbound requests. Additionally, it offers a `BaseApiService` class that integrates `Polly` resilience pipelines and standardized JSON deserialization, making typed HTTP clients easier to build and inherently more reliable.

## Features

- **Context Propagation**: Automatically forwards `CorrelationId` and `ApiKey` headers via the `CorrelationIdHandler`.
- **Token Propagation**: Automatically attaches `Bearer` tokens to outbound requests when the current user is authenticated, using the `AuthenticateHeaderHandler`.
- **Resilient Base Class**: `BaseApiService` wraps `IHttpClientFactory` and `Polly.ResiliencePipeline` to provide standardized error handling and retry mechanisms across all downstream API calls.

## Installation

```bash
dotnet add package Muonroi.Http
```

## Quick Start

### 1. Registering Delegating Handlers

You can register the provided handlers to automatically forward context for your named or typed HTTP clients.

```csharp
using Muonroi.Http.Http;

var builder = WebApplication.CreateBuilder(args);

// Register the handlers
builder.Services.AddTransient<AuthenticateHeaderHandler>();
builder.Services.AddTransient<CorrelationIdHandler>();

// Attach them to a named client
builder.Services.AddHttpClient("DownstreamApi", client =>
{
    client.BaseAddress = new Uri("https://api.internal.com");
})
.AddHttpMessageHandler<AuthenticateHeaderHandler>()
.AddHttpMessageHandler<CorrelationIdHandler>();
```

### 2. Building a Typed API Service

Inherit from `BaseApiService` to quickly build a robust typed client.

```csharp
using Muonroi.Http.Http;
using Polly;

public class PaymentApiClient : BaseApiService
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public PaymentApiClient(
        IHttpClientFactory httpClientFactory, 
        IAuthenticateInfoContext authContext, 
        IMLog<BaseApiService> logger,
        ResiliencePipelineProvider<string> pipelineProvider) 
        : base(httpClientFactory, authContext, logger)
    {
        // Retrieve a pre-configured Polly pipeline (e.g., retries, circuit breakers)
        _pipeline = pipelineProvider.GetPipeline<HttpResponseMessage>("default-api-pipeline");
    }

    public async Task<PaymentResultDto> ProcessPaymentAsync(PaymentRequestDto request, CancellationToken ct)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/payments")
        {
            Content = JsonContent.Create(request)
        };

        // SendAsync automatically executes via the Polly pipeline and deserializes the JSON response
        return await SendAsync<PaymentResultDto>("DownstreamApi", httpRequest, _pipeline, ct);
    }
}
```

## API Reference

### `BaseApiService`
An abstract base class for creating typed HTTP clients. 
- `SendAsync<TResponse>(string clientName, HttpRequestMessage request, ResiliencePipeline<HttpResponseMessage> pipeline, CancellationToken ct)`: Executes the request through the given Polly pipeline, ensures a success status code, and deserializes the JSON response to `TResponse`.

### `AuthenticateHeaderHandler` (DelegatingHandler)
Intercepts outbound requests and injects the `Authorization: Bearer <token>` header if the `IAuthenticateInfoContext` indicates the current user is authenticated.

### `CorrelationIdHandler` (DelegatingHandler)
Intercepts outbound requests and injects `X-Correlation-ID` and `X-API-KEY` headers (using `CustomHeader.CorrelationId` and `CustomHeader.ApiKey` constants) based on the current context.

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Auth -> DelegatingHandler injects Bearer token on outbound calls automatically
Forward the active JWT token to downstream APIs seamlessly using AuthenticateHeaderHandler.

### + Tenancy -> TenantHeaderHandler propagates TenantId to downstream services
Ensure that cross-boundary HTTP requests carry the correct tenant context.

### + Resilience -> BaseApiService wrapped with Polly retry + circuit breaker
Protect your systems from cascading failures by automatically applying resilience pipelines to typed clients.

### + Observability -> Every outbound HTTP call traced with OTel HttpClient instrumentation
Export metrics and distributed tracing headers using W3C Trace Context automatically.

### + Bff -> Bff uses Http client internals for backend proxying with token exchange
Use the resilient HTTP tools to securely proxy frontend requests to backend microservices.

### Full Stack
`csharp
// combined registration
builder.Services.AddHttpClient<MyClient>().AddMuonroiDefaults(); // adds auth + tenant + resilience handlers
builder.Services.AddMuonroiAuth();
builder.Services.AddTenantContext();
builder.Services.AddMuonroiObservability();
`

## Samples
- samples/BffProxy/
- samples/ResilientMicroservices/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
