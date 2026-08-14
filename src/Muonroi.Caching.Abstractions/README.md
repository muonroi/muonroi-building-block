# Muonroi.Caching.Abstractions

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Caching.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Caching.Abstractions/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Caching.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Caching.Abstractions/)

> Caching interfaces and telemetry descriptors for the Muonroi ecosystem.

## Overview
Defines the core caching contracts like `IMCacheService` and configuration models such as `CacheEntryOptions`. It also includes diagnostics through `DistributedCacheRuntimeTelemetry` and `DistributedCacheTelemetryDescriptor`.

## Features
- **Core Abstractions**: Defines `IMCacheService` for ecosystem-wide caching strategies.
- **Telemetry**: Exposes `DistributedCacheRuntimeTelemetry` and `DistributedCacheTelemetryDescriptor` for monitoring cache hits and misses.
- **Cache Configuration**: Provides `CacheEntryOptions` for TTL and expiration policies.

## Installation
```bash
dotnet add package Muonroi.Caching.Abstractions
```

## Quick Start
```csharp
public class MyCacheConsumer
{
    private readonly IMCacheService _cache;
    
    public MyCacheConsumer(IMCacheService cache)
    {
        _cache = cache;
    }
}
```

## Ecosystem Combinations
- **With Muonroi.Caching.Memory**: Implements the abstractions using local memory.
- **Full Stack Example**:
```csharp
builder.Services.AddSingleton<IMCacheService, MyCustomCacheImplementation>();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
