# Muonroi.Data.EntityFrameworkCore.MongoDb

> EF Core provider package for MongoDB in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.MongoDb.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.MongoDb/)

This package provides the MongoDB implementation for `IDbContextConfigurator<T>`, enabling `Muonroi.Data.EntityFrameworkCore` to seamlessly connect to MongoDB databases without changing your `MDbContext` application logic.

## Features

- **MongoDB Provider** — Wraps the official EF Core MongoDB provider.
- **Seamless Integration** — Works out-of-the-box with `AddDbContextConfigure<TDbContext, TPermission>`.
- **Automatic Configuration** — Reads `MongoDbConnectionString` from the configuration automatically.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.MongoDb --prerelease
```

## Quick Start

Ensure your `appsettings.json` specifies `MongoDb` as the `DbType`:

```json
{
  "DatabaseConfigs": {
    "DbType": "MongoDb",
    "ConnectionStrings": {
      "MongoDbConnectionString": "mongodb://localhost:27017/app"
    }
  }
}
```

The database configuration will automatically pick up this package when configuring your context:

```csharp
// Program.cs
builder.Services.AddDbContextConfigure<AppDbContext, AppPermission>(
    builder.Configuration);
```

## Ecosystem Combinations

### + Tenancy.Core → Per-Tenant MongoDB Collections
With `ITenantContext` active, the MongoDB provider routes documents to tenant-specific collections or databases automatically.

### + Observability → MongoDB Operation Tracing
All MongoDB operations are traced as OTel spans with collection name, operation type, and tenant metadata.

### + Caching.Memory → Read-Through Cache for MongoDB
```csharp
var doc = await _cache.GetOrSetAsync($"doc:{id}", () => _collection.FindAsync(id));
```

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).