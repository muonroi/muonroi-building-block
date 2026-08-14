# Muonroi.Caching.Redis

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Caching.Redis.svg)](https://www.nuget.org/packages/Muonroi.Caching.Redis/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Caching.Redis.svg)](https://www.nuget.org/packages/Muonroi.Caching.Redis/)

> Distributed Redis caching and routing rule sets for Muonroi.

## Overview
Implements distributed caching via `RedisCacheService`, routing tables via `RedisRoutingTableStore`, and multi-tenant cache separation with `RedisTenantCache`.

## Features
- **Distributed Caching**: Provides `RedisCacheService` as the core distributed store.
- **Routing & Rules**: Utilizes `RedisRoutingTableStore` and `RedisRuleSetChangeNotifier` for dynamic routing scenarios.
- **Tenant Isolation**: Employs `RedisTenantCache` for safe multi-tenant data management.
- **Trace & Debugging**: Includes `RedisTraceSessionStore` and `RuleDebuggerModeService` for advanced debugging.

## Installation
```bash
dotnet add package Muonroi.Caching.Redis
```

## Quick Start
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRedisCaching(options => {
    // Config
});
```

## Ecosystem Combinations
- **With Muonroi.Core.Abstractions**: Uses trace session stores like `RedisTraceSessionStore` for monitoring system-wide execution context.
- **Full Stack Example**:
```csharp
builder.Services.AddEcosystemRegistry()
                .AddRedisCaching();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
