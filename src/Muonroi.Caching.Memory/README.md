# Muonroi.Caching.Memory

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Caching.Memory.svg)](https://www.nuget.org/packages/Muonroi.Caching.Memory/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Caching.Memory.svg)](https://www.nuget.org/packages/Muonroi.Caching.Memory/)

> Multi-level caching memory implementations for the Muonroi ecosystem.

## Overview
Provides `IMultiLevelCacheService` (L1 memory + L2 Redis), `DistributedCacheKeyBuilder` for tenant-aware key generation, and `AddMultiLevelCaching()` registration.

## Features
- **Multi-Level Caching**: Exposes `IMultiLevelCacheService` for layered L1/L2 data access.
- **Tenant-Aware Keys**: Uses `DistributedCacheKeyBuilder` to prevent data collision across tenants.
- **Easy Registration**: Exposes the `AddMultiLevelCaching()` extension method for easy DI setup.

## Installation
```bash
dotnet add package Muonroi.Caching.Memory
```

## Quick Start
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMultiLevelCaching();

var app = builder.Build();
var cache = app.Services.GetRequiredService<IMultiLevelCacheService>();
```

## Ecosystem Combinations
- **With Muonroi.Caching.Redis**: Connects the `IMultiLevelCacheService` L1 cache with a distributed L2 Redis cache.
- **Full Stack Example**:
```csharp
builder.Services.AddMultiLevelCaching()
                .AddRedisCache(options => ...);
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
