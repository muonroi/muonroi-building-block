# Muonroi.Caching.Memory

> Multi-level in-memory + distributed cache with tenant-aware key isolation and stampede protection — drop in one call, get two cache layers.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Caching.Memory.svg)](https://www.nuget.org/packages/Muonroi.Caching.Memory/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Caching.Memory` wires together `IMemoryCache` and `IDistributedCache` into a single `IMultiLevelCacheService`. On every read it checks memory first, then the distributed layer, and only calls the factory on a full miss — keeping expensive database or API calls to a minimum. Per-tenant key namespacing and optional stampede protection (SemaphoreSlim per key) are built in and require no additional configuration.

## Installation

```bash
dotnet add package Muonroi.Caching.Memory --prerelease
```

## Quick Start

```csharp
// Program.cs
using Muonroi.Caching.Memory.MultiLevel;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Registers IMemoryCache + IDistributedMemoryCache + IMultiLevelCacheService.
builder.Services.AddMultiLevelCaching(builder.Configuration);

builder.Services.AddControllers();
WebApplication app = builder.Build();
app.MapControllers();
app.Run();
```

Inject and use `IMultiLevelCacheService` in your services or controllers:

```csharp
public class ProductsController(IMultiLevelCacheService cache) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProduct(int id, CancellationToken token)
    {
        // Cache-aside: checks memory → distributed → calls factory on miss.
        ProductDto? product = await cache.GetOrSetAsync<ProductDto>(
            key: $"product:{id}",
            factory: async () => await _repository.FindAsync(id, token),
            absoluteExpirationInMinutes: 5,
            token: token);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:int}/cache")]
    public async Task<IActionResult> Evict(int id, CancellationToken token)
    {
        // Removes entry from both memory and the distributed layer.
        await cache.RemoveAsync($"product:{id}", token);
        return Ok();
    }
}
```

## Features

- **Two-layer read path** — memory cache checked first; distributed cache consulted only on a memory miss; factory called only on a full miss.
- **Stampede protection** — a per-key `SemaphoreSlim` prevents concurrent requests from all flooding through to the factory simultaneously (enabled by default via `CacheConfigs.EnableStampedeProtection`).
- **Tenant-aware key isolation** — resolves tenant ID from `ISystemExecutionContextAccessor` or `TenantContext.CurrentTenantId` and embeds it in every cache key via `DistributedCacheKeyBuilder`.
- **TTL jitter** — configurable percentage variance on expiration times smooths thundering-herd cache expiry (`CacheConfigs.TtlJitterPercent`).
- **License-gated distributed layer** — in-memory fallback stays available in free mode; external distributed cache requires `FreeTierFeatures.Premium.DistributedCache` activation checked via `ILicenseGuard`.
- **OpenTelemetry tracing** — every operation starts an `Activity` (source `DistributedCacheRuntimeTelemetry`) tagged with operation name, a SHA-256 key hash, and tenant ID.
- **Configurable defaults** — `CacheConfigs` binds from `appsettings.json` under the `CacheConfigs` section; no code changes needed to adjust TTL, namespace, or stampede settings.

## Configuration

### DI Registration

```csharp
builder.Services.AddMultiLevelCaching(builder.Configuration);
```

`AddMultiLevelCaching` registers:
- `IMemoryCache` (via `AddMemoryCache`)
- `IDistributedCache` (via `AddDistributedMemoryCache` by default)
- `IMultiLevelCacheService` as a singleton (`MultiLevelCacheService`)

### Options — `CacheConfigs`

```json
{
  "CacheConfigs": {
    "CacheType": "Memory",
    "KeyNamespace": "myapp",
    "EnableStampedeProtection": true,
    "DefaultAbsoluteExpirationInMinutes": 1440,
    "TtlJitterPercent": 5
  }
}
```

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `CacheType` | `MultiLevelCacheType` | `Memory` | `Memory`, `Redis`, or `MultiLevel` |
| `KeyNamespace` | `string` | `""` | Prefix prepended to every cache key |
| `EnableStampedeProtection` | `bool` | `true` | Per-key semaphore prevents concurrent factory calls |
| `DefaultAbsoluteExpirationInMinutes` | `int` | `1440` | Default TTL when caller passes `null` |
| `TtlJitterPercent` | `int` | `0` | Randomizes TTL by ±N% (clamped 0–50) |

## API Reference

| Type | Purpose |
|------|---------|
| `IMultiLevelCacheService` | Primary abstraction — `GetOrSetAsync`, `SetAsync`, `GetAsync`, `RemoveAsync` |
| `MultiLevelCacheService` | Concrete implementation; registered as singleton by `AddMultiLevelCaching` |
| `CacheConfigs` | Options record; binds from `"CacheConfigs"` section (`DefaultSectionName`) |
| `MultiLevelCacheType` | Enum: `Memory` / `Redis` / `MultiLevel` — selects cache layer strategy |
| `AddMultiLevelCaching` | Extension on `IServiceCollection`; single registration entry point |

`DistributedCacheKeyBuilder` (from `Muonroi.Caching.Abstractions`) is used internally to compose `{namespace}:{tenantId}:{baseKey}` strings and is also available for direct use when you need to log or inspect resolved keys.

## Samples

- [Quickstart.Caching](../../samples/Quickstart.Caching/) — ASP.NET Core Web API demonstrating cache-aside, explicit set, direct read, eviction, cache warming, and key composition with `DistributedCacheKeyBuilder`

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Caching.Abstractions`](../Muonroi.Caching.Abstractions/) — contracts (`DistributedCacheKeyBuilder`, `CacheEntryOptions`, `IMCacheService`) consumed by this package
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — provides `TenantContext` used for automatic tenant-scoped key isolation
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — `ILicenseGuard` and `LicenseState` that gate the external distributed cache feature

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
