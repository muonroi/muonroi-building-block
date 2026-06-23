# Muonroi.Grpc

> Production-ready gRPC server and client infrastructure for ASP.NET Core — tenancy, telemetry, rate limiting, and Polly resilience out of the box.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Grpc.svg)](https://www.nuget.org/packages/Muonroi.Grpc/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

`Muonroi.Grpc` wires the full Muonroi execution-context pipeline into gRPC: server-side interceptors propagate correlation IDs, tenant IDs, and auth tokens from incoming metadata; client-side interceptors forward the same context outbound. It adds in-memory rate limiting, optional gRPC-Web and JSON transcoding, optional mutual TLS enforcement, and OpenTelemetry traces and metrics — all configured from a single `appsettings.json` section. This is a **Commercial** package and requires a license that enables the `Premium.Grpc` feature.

## Installation

```bash
dotnet add package Muonroi.Grpc --prerelease
```

## Quick Start

### Server

Call `AddGrpcServer` in `Program.cs` and `UseGrpcTransport` on the application pipeline:

```csharp
using Muonroi.Grpc.Grpc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Registers GrpcServerInterceptor, GrpcRateLimiter, IContextResolver,
// ITenantContextPolicy, MTokenInfo, and a "grpc-runtime" health check.
// Binds server options from the "GrpcServicesConfig" appsettings section.
builder.Services.AddGrpcServer(builder.Configuration);

WebApplication app = builder.Build();

// Enables gRPC-Web middleware when GrpcServicesConfig.Server.EnableGrpcWeb = true.
app.UseGrpcTransport(builder.Configuration);

app.MapGrpcService<GreeterService>();
app.Run();
```

### Typed client service

Derive from `BaseGrpcService` to get retry, timeout, circuit-breaker, and automatic metadata propagation:

```csharp
using Grpc.Core;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Grpc.Grpc;

public sealed class GreeterClientService(ISystemExecutionContextAccessor contextAccessor)
    : BaseGrpcService(contextAccessor)
{
    public Task<string> SayHelloAsync(string name)
    {
        return CallGrpcServiceAsync(
            methodName: "Greeter/SayHello",
            grpcCall: (Metadata metadata) =>
            {
                // Pass 'metadata' (correlation id, tenant id, api key) to your
                // generated stub: greeterClient.SayHelloAsync(request, metadata)
                return Task.FromResult($"Hello, {name}!");
            },
            policy: null);
    }
}
```

Register the client service:

```csharp
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
builder.Services.AddScoped<GreeterClientService>();
```

### Multi-client registration from configuration

```csharp
// Reads each named entry from GrpcServicesConfig.Services and creates a
// typed Grpc.Net.ClientFactory client with channel options and interceptors.
builder.Services.AddGrpcClients(
    builder.Configuration,
    new Dictionary<string, Type>
    {
        ["InventoryService"] = typeof(Inventory.InventoryClient),
        ["PaymentService"]   = typeof(Payment.PaymentClient),
    });
```

## Features

- **`AddGrpcServer`** — registers the gRPC server with `GrpcServerInterceptor` (auth, tenancy, rate limiting, telemetry), optional JSON transcoding, and a health check
- **`UseGrpcTransport`** — enables gRPC-Web middleware from configuration
- **`AddGrpcClient<TClient>`** — registers a single typed gRPC client with channel options and interceptors
- **`AddGrpcClients`** — registers multiple named typed clients from `GrpcServicesConfig.Services`
- **`BaseGrpcService`** — abstract base class for client services: Polly retry + timeout + circuit-breaker, automatic metadata propagation via `CreateMetadata()` and `CallGrpcServiceAsync()`
- **`GrpcServerInterceptor`** — server interceptor covering unary, client-streaming, server-streaming, and duplex-streaming handlers; extracts correlation ID, tenant ID, user identity, and API key from incoming metadata
- **`GrpcClientAuthInterceptor`** — outbound interceptor that forwards correlation ID, tenant ID, and Bearer token from the current execution context
- **`GrpcClientTelemetryInterceptor`** — outbound interceptor that records OTel traces and metrics for every client call
- **`GrpcRateLimiter`** — in-memory sliding-window rate limiter per API key and per tenant
- **Mutual TLS** — server can require client certificates and restrict to an allowlist of thumbprints
- **Compression** — server response compression algorithm and level are configurable (default: gzip/Optimal)

## Configuration

Bind the `GrpcServicesConfig` section in `appsettings.json`:

```json
{
  "GrpcServicesConfig": {
    "Server": {
      "EnableDetailedErrors": true,
      "EnableGrpcWeb": false,
      "EnableJsonTranscoding": false,
      "ResponseCompressionAlgorithm": "gzip",
      "ResponseCompressionLevel": "Optimal",
      "RequireMutualTls": false,
      "AllowedClientCertificateThumbprints": [],
      "RateLimit": {
        "Enabled": false,
        "RequestsPerMinutePerApiKey": 600,
        "RequestsPerMinutePerTenant": 1200
      }
    },
    "ClientDefaults": {
      "TimeoutSeconds": 10,
      "RetryCount": 3,
      "InitialBackoffSeconds": 1,
      "MaxBackoffSeconds": 8,
      "LoadBalancingPolicy": "pick_first",
      "MaxReceiveMessageSizeBytes": 104857600,
      "MaxSendMessageSizeBytes": 104857600,
      "ForwardAuthToken": false,
      "ForwardTenantId": true
    },
    "Services": {
      "InventoryService": {
        "Uri": "https://inventory-svc:5001",
        "TimeoutSeconds": 5,
        "RetryCount": 2,
        "ForwardAuthToken": true
      }
    }
  }
}
```

`GrpcServiceConfig.Methods` accepts per-method policy overrides (timeout, retry, backoff) keyed by method name.

## API Reference

| Type | Purpose |
|------|---------|
| `GrpcHandler` | Static extension class — `AddGrpcServer`, `AddGrpcClient<T>`, `AddGrpcClients`, `UseGrpcTransport` |
| `BaseGrpcService` | Abstract client base — `CreateMetadata()`, `CallGrpcServiceAsync()` with Polly policies |
| `GrpcServerInterceptor` | Server-side interceptor: tenancy, auth, rate limiting, telemetry for all call types |
| `GrpcClientAuthInterceptor` | Outbound interceptor: correlation ID, tenant ID, Bearer token forwarding |
| `GrpcClientTelemetryInterceptor` | Outbound interceptor: OTel traces and metrics per client call |
| `GrpcRateLimiter` | In-memory per-minute rate limiter keyed by API key and tenant ID |
| `GrpcServicesConfig` | Root configuration: `Server`, `ClientDefaults`, `Services` dictionary |
| `GrpcServerConfig` | Server runtime options (message sizes, compression, gRPC-Web, JSON transcoding, mTLS, rate limit) |
| `GrpcClientDefaultsConfig` | Default client options (timeout, retry, backoff, load balancing, message sizes, forwarding flags) |
| `GrpcServiceConfig` | Per-service client options — overrides `GrpcClientDefaultsConfig` for a named service |
| `GrpcMethodPolicyConfig` | Per-method policy overrides (timeout, retry, backoff) |
| `GrpcRateLimitConfig` | Rate limit settings: enabled flag, RPM limits per API key and per tenant |
| `MetadataExtensions` | `GetValue(this Metadata, string key)` — case-insensitive metadata lookup |

## Samples

- [Quickstart.Grpc](../../samples/Quickstart.Grpc/) — `AddGrpcServer`/`UseGrpcTransport` + `BaseGrpcService` client calls (license-gated)

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires a license with the `Premium.Grpc` feature enabled

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — `ISystemExecutionContextAccessor`, `SystemExecutionContext`, custom header constants
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — `ILicenseGuard`, license feature contracts
- [`Muonroi.Observability`](../Muonroi.Observability/) — OTel pipeline consumed by the server and client interceptors
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — `ITenantContextPolicy`, multi-tenant header validation

## License

This package is distributed under a **Commercial license**. A valid Muonroi license with the `Premium.Grpc` feature is required at runtime. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL) for terms.
