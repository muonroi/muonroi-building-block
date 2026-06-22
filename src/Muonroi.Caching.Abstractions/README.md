# Muonroi.Caching.Abstractions

> Caching contracts — `IMCacheService`, `CacheEntryOptions`, and cache key conventions — for Muonroi applications.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Caching.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Caching.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is the **contracts-only** layer of the Muonroi caching stack. It defines the `IMCacheService` interface, the `CacheEntryOptions` record, the `DistributedCacheKeyBuilder` static helper, and the telemetry surface. There is **no runtime behavior** in this package — all implementations live in [`Muonroi.Caching.Memory`](../Muonroi.Caching.Memory/) (in-process + multi-level) and [`Muonroi.Caching.Redis`](../Muonroi.Caching.Redis/) (distributed Redis backend).

Reference this package directly when authoring a library that depends on the caching abstractions without coupling to a specific backend.

## Installation

```bash
dotnet add package Muonroi.Caching.Abstractions --prerelease
```

## Quick Start

This package ships contracts only. To get a working cache you depend on an implementation package and consume `IMCacheService` or `IMultiLevelCacheService` via DI.

### Implement `IMCacheService` in a custom backend

```csharp
using Muonroi.Caching.Abstractions.Distributed;

public sealed class MyCustomCacheService : IMCacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken token = default)
        => /* your storage read */ Task.FromResult(default(T?));

    public Task SetAsync<T>(string key, T value, CacheEntryOptions? options = null,
        CancellationToken token = default)
        => /* your storage write */ Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken token = default)
        => /* your storage delete */ Task.CompletedTask;

    public Task RefreshAsync(string key, CancellationToken token = default)
        => /* slide TTL */ Task.CompletedTask;

    public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory,
        CacheEntryOptions? options = null, CancellationToken token = default) where T : class
        => /* check then factory */ factory();
}
```

### Consume `IMCacheService` with `CacheEntryOptions`

```csharp
using Muonroi.Caching.Abstractions.Distributed;

// Injected by the implementation package's DI registration
public class OrderRepository(IMCacheService cache)
{
    public async Task<Order?> GetAsync(int orderId, CancellationToken token)
    {
        string key = DistributedCacheKeyBuilder.Build(
            key: $"order:{orderId}",
            keyNamespace: "orders");

        return await cache.GetOrSetAsync<Order>(
            key,
            factory: async () => await LoadFromDatabaseAsync(orderId, token),
            options: new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                KeyNamespace = "orders",
                TenantScoped = true   // key includes the current tenant id
            },
            token);
    }
}
```

See the [Quickstart.Caching](../../samples/Quickstart.Caching/) sample for a full working example using `Muonroi.Caching.Memory`.

## Features

- `IMCacheService` — unified distributed cache interface: `GetAsync`, `SetAsync`, `RemoveAsync`, `RefreshAsync`, `GetOrSetAsync` (cache-aside with factory)
- `CacheEntryOptions` — immutable record with `AbsoluteExpirationRelativeToNow` (default 24 h), `SlidingExpiration`, `KeyNamespace`, and `TenantScoped` (default `true`)
- `DistributedCacheKeyBuilder` — composes structured cache keys in `{namespace}:{tenantId}:{baseKey}` form; `NormalizeTenantId` trims and null-coalesces blank input
- `DistributedCacheTelemetryDescriptor` — `ITelemetryDescriptor` that exposes the Activity source and Meter names for wiring into OpenTelemetry
- `DistributedCacheRuntimeTelemetry` — static helpers (`ActivitySource`, `TrackOperation`) for recording operation counts, error counts, and latency histograms

## Configuration

This package carries no DI registration. Configuration and DI wiring are provided by the implementation packages:

- **`Muonroi.Caching.Memory`** — call `builder.Services.AddMultiLevelCaching(builder.Configuration)` to register `IMultiLevelCacheService` (in-process + optional distributed layer)
- **`Muonroi.Caching.Redis`** — registers `IMCacheService` backed by Redis; see that package's README for the `appsettings.json` `Cache` section

## API Reference

| Type | Purpose |
|------|---------|
| `IMCacheService` | Core distributed cache contract: get, set, remove, refresh, get-or-set |
| `CacheEntryOptions` | Immutable options record controlling TTL, sliding expiry, key namespace, and tenant scoping |
| `DistributedCacheKeyBuilder` | Static helper that builds `{namespace}:{tenantId}:{key}` composite keys |
| `DistributedCacheTelemetryDescriptor` | `ITelemetryDescriptor` exposing the Activity source and Meter names |
| `DistributedCacheRuntimeTelemetry` | Static metrics surface: `TrackOperation` records counters and latency histograms |

## Samples

- [Quickstart.Caching](../../samples/Quickstart.Caching/) — end-to-end demo of all cache operations (get-or-set, set, direct get, eviction, warming, key builder) using `Muonroi.Caching.Memory`

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Caching.Memory`](../Muonroi.Caching.Memory/) — implements `IMultiLevelCacheService` with in-process L1 + optional distributed L2
- [`Muonroi.Caching.Redis`](../Muonroi.Caching.Redis/) — implements `IMCacheService` with a Redis distributed backend
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — foundational guards and interfaces referenced by this package

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
