# Muonroi.Grpc
> Robust, multi-tenant gRPC client and server utilities for the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Grpc.svg)](https://www.nuget.org/packages/Muonroi.Grpc/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Grpc` is a comprehensive package for building and consuming gRPC services within the Muonroi Building Block ecosystem. It bridges standard ASP.NET Core gRPC capabilities with Muonroi-specific concepts such as Tenant ID propagation, distributed tracing (OpenTelemetry), rate limiting, and centralized configuration-driven retry policies.

This package eliminates the boilerplate required to configure resilient gRPC clients and secures gRPC servers with automatic interceptors for logging, telemetry, and authentication.

## Features

- **Centralized Configuration**: Drive gRPC client settings (timeouts, retries, load balancing, message sizes) directly from `appsettings.json` via `GrpcServicesConfig`.
- **Context Propagation**: The `GrpcClientAuthInterceptor` automatically forwards JWT tokens and `X-Tenant-ID` headers to downstream services.
- **Resilient Clients**: Provides a `BaseGrpcService` base class and extension methods that automatically apply Polly retry and timeout policies.
- **Server Interceptors**: Includes `GrpcServerInterceptor` for automatic error handling, logging, and tenant resolution, plus built-in `GrpcRateLimiter` support.
- **Observability**: Built-in OpenTelemetry support via `GrpcRuntimeTelemetry` and `GrpcClientTelemetryInterceptor`.

## Installation

```bash
dotnet add package Muonroi.Grpc
```

## Quick Start

### 1. Server Setup

Register gRPC server components in your `Program.cs`. This automatically configures server interceptors for exception mapping, telemetry, and tenant resolution.

```csharp
using Muonroi.Grpc;

var builder = WebApplication.CreateBuilder(args);

// Adds gRPC services, interceptors, and binds GrpcServicesConfig options
builder.Services.AddGrpcServer(builder.Configuration);

var app = builder.Build();

// Enable gRPC routing and optional gRPC-Web support
app.UseGrpcTransport(builder.Configuration);

// Map your actual gRPC services
app.MapGrpcService<MyServiceImplementation>();

 await app.RunAsync();
```

### 2. Client Setup

Register a strongly-typed gRPC client. This automatically adds the authentication and telemetry interceptors and applies resiliency policies.

```csharp
using Muonroi.Grpc;

var builder = WebApplication.CreateBuilder(args);

// Adds the typed client, intercepting calls to forward the current TenantId and JWT
builder.Services.AddGrpcClient<MyService.MyServiceClient>("https://my-downstream-service:5001");
```

### 3. AppSettings Configuration

The package automatically reads configuration from the `GrpcServices` section.

```json
{
  "GrpcServices": {
    "Server": {
      "EnableDetailedErrors": false,
      "ResponseCompressionAlgorithm": "gzip",
      "RateLimit": {
        "Enabled": true,
        "RequestsPerMinutePerTenant": 1200
      }
    },
    "ClientDefaults": {
      "TimeoutSeconds": 10,
      "RetryCount": 3,
      "MaxReceiveMessageSizeBytes": 104857600
    },
    "Services": {
      "MyServiceClient": {
        "Uri": "https://my-downstream-service:5001",
        "ForwardAuthToken": true,
        "ForwardTenantId": true
      }
    }
  }
}
```

## API Reference

### `GrpcHandler`
Provides primary DI registration extensions:
- `AddGrpcServer(services, config)`
- `AddGrpcClient<TClient>(services, serviceUri)`
- `UseGrpcTransport(app, config)`

### `BaseGrpcService`
An abstract base class designed to wrap raw generated gRPC clients to provide a cleaner application-level API with built-in telemetry scopes and exception unwrapping.

### Interceptors
- **`GrpcClientAuthInterceptor`**: Outbound interceptor forwarding `Authorization` and `X-Tenant-ID`.
- **`GrpcClientTelemetryInterceptor`**: Outbound interceptor recording spans and metrics.
- **`GrpcServerInterceptor`**: Inbound interceptor catching exceptions and standardizing fault contracts.

### Telemetry
Register the provided `GrpcTelemetryDescriptor` in your observability composition root to automatically expose `Muonroi.BuildingBlock.Grpc` meters and traces.

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Tenancy -> Tenant ID propagated in gRPC metadata headers automatically
Seamlessly flow the current TenantId across service boundaries without manual header mapping.

### + Auth -> JWT bearer validation on gRPC calls
Verify downstream requests securely using standard authentication middleware on the gRPC server.

### + Resilience -> Polly retry wraps gRPC channel calls
Apply jittered backoffs and circuit breakers to gRPC clients automatically via configuration.

### + Observability -> gRPC call duration traced as OTel spans per method
Automatically capture request latency, status codes, and distributed traces.

### + Tenancy.SiteProfile.Grpc -> Site profile resolved via gRPC for cross-service calls
Carry over the active site profile across microservices.

### Full Stack
`csharp
// combined registration
builder.Services.AddGrpcServer(builder.Configuration);
builder.Services.AddMuonroiAuth();
builder.Services.AddTenantContext();
builder.Services.AddMuonroiObservability();
`

## Samples
- samples/GrpcMicroservices/
- samples/MultiTenantSaaS/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
