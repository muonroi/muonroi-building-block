# Muonroi.Caching.Redis

> Redis-backed distributed cache and routing-table store for the Muonroi ecosystem, with built-in tenancy isolation, OpenTelemetry tracing, and license enforcement.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Caching.Redis.svg)](https://www.nuget.org/packages/Muonroi.Caching.Redis/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-red.svg)](LICENSE-COMMERCIAL)

This package wires StackExchange.Redis into the Muonroi service collection and registers `RedisCacheService` as the `IMCacheService` implementation. Every cache operation is tenant-scoped, emits OpenTelemetry `Activity` spans, and is gated by the `distributed-cache` license feature. A second registration (`AddRedisRoutingTable`) adds a Redis-backed routing table store used by the message-routing subsystem.

## Installation

```bash
dotnet add package Muonroi.Caching.Redis --prerelease
```

> **License requirement.** This is a commercial package. A valid Muonroi license with the `distributed-cache` feature is required at runtime. Registration will throw `MConfigurationException` at startup if `RedisConfigs.Enable` is `true` but `Host` or `Port` is absent.

## Quick Start

**appsettings.json**

```json
{
  "RedisConfigs": {
    "Enable": true,
    "Host": "localhost",
    "Port": "6379",
    "Password": "",
    "KeyPrefix": "myapp",
    "AllowAdmin": false,
    "AbortOnConnectFail": false
  }
}
```

**Program.cs**

```csharp
using Muonroi.Core.Abstractions.Configuration;
using Muonroi.Caching.Redis.Redis;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

RedisConfigs redisConfigs = builder.Configuration
    .GetSection(RedisConfigs.DefaultSectionName)
    .Get<RedisConfigs>() ?? new RedisConfigs();

builder.Services.AddRedis(builder.Configuration, redisConfigs);

WebApplication app = builder.Build();
app.Run();
```

**Cache-aside pattern using `IMCacheService`**

```csharp
public class ProductService(IMCacheService cache)
{
    public async Task<ProductDto?> GetProductAsync(int id, CancellationToken ct)
    {
        string key = $"product:{id}";

        return await cache.GetOrSetAsync<ProductDto>(
            key,
            factory: async () => await _repository.FindAsync(id, ct),
            options: new CacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                TenantScoped = true   // key includes current tenant id
            },
            token: ct);
    }
}
```

**`IDistributedCache` extension methods (alternative low-level API)**

```csharp
// Available as extensions on IDistributedCache:
await distributedCache.GetOrSetAsync<MyDto>("key", () => FetchAsync(), absoluteExpirationInMinutes: 60);
await distributedCache.SetCacheAsync("key", myDto, absoluteExpirationInMinutes: 10);
string? raw = await distributedCache.GetCacheAsync("key");
await distributedCache.RemoveAsync("key");
await distributedCache.RefreshAsync("key");
```

## Features

- Registers `IConnectionMultiplexer`, `IDistributedCache` (StackExchange.Redis), and `IMCacheService` (`RedisCacheService`) in a single call to `AddRedis`.
- Tenant-isolated cache keys via `DistributedCacheKeyBuilder` — keys include the current `ITenantContext.TenantId` by default; opt out per entry with `TenantScoped = false`.
- OpenTelemetry `Activity` spans for every cache operation (`get`, `set`, `remove`, `refresh`, `get_or_set`) tagged with `cache.operation`, `cache.key_hash` (SHA-256 prefix), and `tenant.id`.
- License guard: each operation calls `licenseGuard.EnsureFeature("distributed-cache")` and throws `MInternalException` when the feature is absent.
- `AddRedisRoutingTable` registers `IRedisRoutingTableStore` (`RedisRoutingTableStore`) with a configurable local in-memory cache TTL and pub/sub invalidation channel.
- `IDistributedCache` extension methods (`GetCacheAsync`, `GetCacheAsync<T>`, `SetCacheAsync<T>`, `RemoveAsync`, `RefreshAsync`, `GetOrSetAsync<T>`) for call sites that already depend on `IDistributedCache`.
- Connection settings can be supplied via `RedisConfigs` object, overridden by configuration keys under `RedisConfigs:Host`, `RedisConfigs:Port`, `RedisConfigs:Password`, `RedisConfigs:KeyPrefix`.

## Configuration

### `RedisConfigs` (section `"RedisConfigs"`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enable` | `bool` | `false` | When `false`, `AddRedis` returns immediately without registering any Redis services. |
| `Host` | `string` | `""` | Redis host address. Required when `Enable` is `true`. |
| `Port` | `string` | `""` | Redis port. Required when `Enable` is `true`. |
| `Password` | `string` | `""` | Redis password. Optional — omit for unauthenticated instances. |
| `KeyPrefix` | `string` | `""` | Instance name prepended to every Redis key via `InstanceName`. |
| `AllowAdmin` | `bool` | `false` | Enables administrative commands on the connection. |
| `AbortOnConnectFail` | `bool` | `false` | When `false`, the client retries connection in the background. |

### `RedisRoutingTableOptions`

Passed to `AddRedisRoutingTable(configure: opts => { ... })`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LocalCacheTtl` | `TimeSpan` | `60 s` | In-process cache TTL before re-reading from Redis. |
| `KeyPrefix` | `string` | `"mrt"` | Prefix for Redis hash keys. |
| `ChannelName` | `string` | `"routing-table-changed"` | Pub/sub channel for cache invalidation. |

### `CacheEntryOptions` (used with `IMCacheService`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AbsoluteExpirationRelativeToNow` | `TimeSpan?` | `null` | Hard expiry from the moment of write. |
| `SlidingExpiration` | `TimeSpan?` | `null` | TTL reset on each access. |
| `KeyNamespace` | `string?` | `null` | Namespace prefix inserted before the base key. |
| `TenantScoped` | `bool` | `true` | When `true`, the current tenant id is embedded in the key. |

## API Reference

| Type | Purpose |
|------|---------|
| `RedisExtensions.AddRedis` | Registers `IConnectionMultiplexer`, `IDistributedCache`, and `IMCacheService` (singleton). |
| `RedisExtensions.AddRedisRoutingTable` | Registers `IRedisRoutingTableStore` with optional `RedisRoutingTableOptions`. |
| `RedisCacheService` | `IMCacheService` implementation backed by StackExchange.Redis. |
| `IRedisRoutingTableStore` | Gets, upserts, and removes per-tenant routing table entries from Redis. |
| `RedisRoutingTableStore` | Default implementation of `IRedisRoutingTableStore` with local cache and pub/sub invalidation. |
| `RoutingTableEntry` | Immutable record representing a single routing rule (message type, tenant, address, FEEL predicate, order). |
| `RedisConfigs` | Connection + feature-flag options for `AddRedis` (section `"RedisConfigs"`). |
| `RedisRoutingTableOptions` | Configures the routing table store (TTL, key prefix, pub/sub channel). |
| `GetCacheAsync` / `GetCacheAsync<T>` | `IDistributedCache` extensions — read a raw or deserialized value. |
| `SetCacheAsync<T>` | `IDistributedCache` extension — write with optional absolute expiry. |
| `GetOrSetAsync<T>` | `IDistributedCache` extension — cache-aside with factory. |
| `RemoveAsync` / `RefreshAsync` | `IDistributedCache` extensions — evict or reset TTL. |

## Samples

- [Quickstart.Caching](../../samples/Quickstart.Caching/) — ASP.NET Core Web API demonstrating `IMCacheService` get/set/remove/get-or-set patterns and `DistributedCacheKeyBuilder` key composition. Defaults to in-memory caching; switch to Redis by enabling the `RedisConfigs` section in appsettings.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires a Muonroi license activation with the `distributed-cache` feature.

## Related Packages

- [`Muonroi.Caching.Abstractions`](../Muonroi.Caching.Abstractions/) — defines `IMCacheService`, `CacheEntryOptions`, and `DistributedCacheKeyBuilder`; no runtime Redis dependency.
- [`Muonroi.Caching.Memory`](../Muonroi.Caching.Memory/) — in-process and multi-level cache (`IMultiLevelCacheService`); use when Redis is not available or as the L1 layer.
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `RedisConfigs`, guards (`MGuard`), and exceptions (`MConfigurationException`).
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — `ILicenseGuard` and `FreeTierFeatures` used to enforce the `distributed-cache` feature gate.

## License

This package is distributed under the **Muonroi Commercial License**. A valid license is required to use it. Contact [leanhphi1706@gmail.com](mailto:leanhphi1706@gmail.com) to obtain a license.
