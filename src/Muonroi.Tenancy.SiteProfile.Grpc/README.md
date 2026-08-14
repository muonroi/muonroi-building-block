# Muonroi.Tenancy.SiteProfile.Grpc

> gRPC site resolution interceptor for multi-site services — extracts `SiteCode` from gRPC metadata and routes each request to the correct site-specific handler without any `if`/`switch` in your code.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Tenancy.SiteProfile.Grpc.svg)](https://www.nuget.org/packages/Muonroi.Tenancy.SiteProfile.Grpc/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package adds the gRPC layer of the Muonroi multi-site tenancy stack. It provides a server-side `SiteCodeGrpcInterceptor` that reads a configurable metadata key (or an HTTP header fallback) on every incoming call and populates the scoped `ISiteCodeHolder`. On the client side, `ISiteGrpcClientFactory` resolves the right per-site gRPC client or facade without any per-call branching logic. Two service-side dispatch patterns are supported: shared-proto dispatch (via `SiteGrpcDispatchHelper<T>`) and per-site-proto auto-discovery (via `[SiteGrpcService]` + `MapSiteGrpcServices`).

## Installation

```bash
dotnet add package Muonroi.Tenancy.SiteProfile.Grpc --prerelease
```

## Quick Start

The snippet below mirrors the pattern used in `samples/TestProject.Service` and `samples/TestProject.Aggregate`.

**Server — register and intercept**

```csharp
// Program.cs

// 1. Register the site resolver and interceptor
services.AddSiteGrpcServices(o =>
{
    o.MetadataKey = "x-site-code";          // gRPC metadata key
    o.HttpHeaderFallbackKey = "x-site-code"; // HTTP header fallback (gRPC-Web)
    o.Required = false;                      // allow missing SiteCode (set true to throw)
});
services.AddGrpc(o => o.Interceptors.Add<SiteCodeGrpcInterceptor>());

// 2a. Shared proto: map a single gRPC service for all sites
app.MapGrpcService<MySharedGrpcService>();

// 2b. Per-site proto: auto-discover [SiteGrpcService]-annotated types from an assembly
app.MapSiteGrpcServices(typeof(BravoGrpcService).Assembly);
```

**Server — shared proto dispatcher**

```csharp
// Register site-specific handlers
services.AddSiteGrpcHandler<MyServiceBase, AlphaMyService>("ALPHA");
services.AddSiteGrpcHandler<MyServiceBase, DefaultMyService>("default");
services.AddSiteGrpcDispatcher<MyServiceBase>();

// Dispatcher proxy — thin delegating class
public class MyDispatcher : MyServiceBase
{
    private readonly SiteGrpcDispatchHelper<MyServiceBase> _helper;
    public MyDispatcher(SiteGrpcDispatchHelper<MyServiceBase> helper) => _helper = helper;

    public override Task<MyReply> MyRpc(MyRequest req, ServerCallContext ctx)
        => _helper.DispatchAsync(ctx, (h, c) => h.MyRpc(req, c));
}
```

**Client — site-routed factory**

```csharp
// Register per-site clients
services.AddGrpcClient<AlphaFcdClient>(o => o.Address = new Uri("https://alpha-grpc:5001"));
services.AddGrpcClient<DefaultFcdClient>(o => o.Address = new Uri("https://default-grpc:5001"));

services.AddSiteGrpcClientFactory();
services.AddSiteGrpcClient<AlphaFcdClient>("ALPHA", "fcd");
services.AddSiteGrpcClient<DefaultFcdClient>("default", "fcd");

WebApplication app = builder.Build();
app.InitializeSiteGrpcClients(); // required before app.Run()
 await app.RunAsync();

// Consuming service — inject ISiteGrpcClientFactory, no if/switch needed
public class FcdAggregator(ISiteGrpcClientFactory factory)
{
    public async Task ProcessAsync()
    {
        var client = factory.CreateForCurrentSite<FcdServiceBase>("fcd");
        await client.CreateAsync(request);
    }
}
```

## Features

- **`SiteCodeGrpcInterceptor`** — server interceptor that extracts `SiteCode` from a configurable gRPC metadata key with an optional HTTP header fallback; throws `RpcException(InvalidArgument)` when `Required = true` and the key is absent.
- **`ISiteCodeHolder`** — scoped holder populated by the interceptor; consumed by `ISiteProfileResolver`'s `siteCodeAccessor`.
- **`ISiteGrpcClientFactory`** — resolves the registered per-site gRPC client (`CreateForCurrentSite<TBase>`) or unified facade client (`CreateFacadeForCurrentSite<TFacade>`) for the current request, falling back to `"default"` when no site-specific entry matches.
- **`SiteGrpcDispatchHelper<TServiceBase>`** — server-side helper for shared-proto services; resolves the keyed `TServiceBase` handler for the current site and delegates RPC calls to it.
- **`[SiteGrpcService(siteId)]`** — attribute for per-site-proto gRPC service implementations; `MapSiteGrpcServices(assembly)` auto-discovers and maps them as separate endpoints.
- **`[GenerateSiteGrpcFacadeAttribute]`** — applied to a partial facade interface; a source generator emits the completed interface and a concrete facade class that merges shared + per-site RPC methods.
- **`AddSiteGrpcFacadeClient<TFacade, TImpl>`** — registers a keyed scoped facade client; resolves per-site constructor arguments from the root `GrpcClientFactory`.
- **`InitializeSiteGrpcClients`** — `WebApplication` extension that captures the root `GrpcClientFactory` into `GrpcClientFactoryAccessor` for Autofac compatibility; must be called between `builder.Build()` and `app.Run()`.

## Configuration

### `SiteGrpcOptions`

Configured via `AddSiteGrpcServices(o => { ... })`.

| Property | Type | Default | Description |
|---|---|---|---|
| `MetadataKey` | `string` | `"SiteCode"` | gRPC metadata key that carries the `SiteCode`. |
| `HttpHeaderFallbackKey` | `string?` | `null` | HTTP header checked when the metadata key is absent. Set to `null` to disable. |
| `Required` | `bool` | `true` | When `true`, missing `SiteCode` throws `RpcException(InvalidArgument)`. When `false`, continues without setting `ISiteCodeHolder`. |

### Equivalent `appsettings.json` (not read automatically)

Options are configured in code via the lambda — there is no automatic `appsettings.json` binding for `SiteGrpcOptions`.

## API Reference

| Type | Purpose |
|------|---------|
| `SiteGrpcExtensions` | Static class with all `IServiceCollection` and `WebApplication` extension methods. |
| `SiteCodeGrpcInterceptor` | gRPC server interceptor; extracts `SiteCode` and sets `ISiteCodeHolder`. |
| `ISiteCodeHolder` | Scoped holder for the current request's resolved `SiteCode`. |
| `SiteGrpcOptions` | Options for metadata key, HTTP fallback key, and required behavior. |
| `ISiteGrpcClientFactory` | Resolves the per-site gRPC client or facade for the current request. |
| `SiteGrpcDispatchHelper<TServiceBase>` | Server-side helper that routes shared-proto RPC calls to the correct keyed site handler. |
| `SiteGrpcClientRegistry` | Internal registry built from `AddSiteGrpcClient` descriptors; consumed by `ISiteGrpcClientFactory`. |
| `GrpcClientFactoryAccessor` | Captures the root `GrpcClientFactory` for Autofac-compatible scoped client resolution. |
| `SiteGrpcServiceAttribute` | Marks a gRPC service class as per-site-proto for auto-discovery via `MapSiteGrpcServices`. |
| `GenerateSiteGrpcFacadeAttribute` | Triggers source-generator emission of a unified facade interface + implementation. |

## Samples

- [TestProject.Service](../../samples/TestProject.Service/) — gRPC service host with `AddSiteGrpcServices`, `SiteCodeGrpcInterceptor`, shared-proto mapping, and per-site handler registration.
- [TestProject.Aggregate](../../samples/TestProject.Aggregate/) — aggregate host with `ISiteGrpcClientFactory`, per-site proto auto-discovery via `[SiteGrpcService]` + `MapSiteGrpcServices`, and `InitializeSiteGrpcClients`.

## Compatibility

- Target framework: `net8.0`
- Requires: `Microsoft.AspNetCore.App` framework reference, `Grpc.AspNetCore.Server`, `Grpc.Net.ClientFactory`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Tenancy.SiteProfile`](../Muonroi.Tenancy.SiteProfile/) — core multi-site tenancy abstractions and `ISiteProfileResolver`; required by this package.
- [`Muonroi.Tenancy.SiteProfile.Web`](../Muonroi.Tenancy.SiteProfile.Web/) — HTTP/REST equivalent of site resolution (middleware + `IHttpContextAccessor`-based `SiteCode` extraction).
- [`Muonroi.Tenancy.SiteProfile.SourceGenerators`](../Muonroi.Tenancy.SiteProfile.SourceGenerators/) — source generator that processes `[GenerateSiteGrpcFacade]` and emits the facade implementation.


## Ecosystem Combinations

### + Tenancy.SiteProfile → gRPC Transport for Profile Resolution
When site profile metadata lives in a central service, `SiteProfileGrpcClient` fetches it over gRPC instead of resolving locally — enabling centralized site management.

### + Grpc → Base Service Patterns
`SiteProfileGrpcService` uses `BaseGrpcService` patterns for interceptors, tenant propagation, and OTel tracing.

### + Resilience → gRPC Failover
Polly retry wraps gRPC calls to the site profile service. If the central service is temporarily unavailable, the last known profile is used from local cache.

### + Caching.Memory → Profile Cache
Resolved site profiles are cached in memory to avoid gRPC calls on every request:
```csharp
builder.Services
    .AddSiteProfileGrpc(config)        // gRPC client for remote profiles
    .AddMultiLevelCaching(config);    // cache resolved profiles locally
```

## Samples
- [`Quickstart.Tenancy.SiteProfile`](../../samples/Quickstart.Tenancy.SiteProfile)


## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
