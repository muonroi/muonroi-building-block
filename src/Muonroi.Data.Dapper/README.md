# Muonroi.Data.Dapper

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Data.Dapper.svg)](https://www.nuget.org/packages/Muonroi.Data.Dapper/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Data.Dapper.svg)](https://www.nuget.org/packages/Muonroi.Data.Dapper/)

> High-performance Dapper implementations with row-level security.

## Overview
Features Dapper repository patterns (`MDapperRepositoryBase`) and advanced Row-Level Security (RLS) via `TenantRlsDapper` and `DapperRlsServiceCollectionExtensions`.

## Features
- **Dapper Repository**: Employs `MDapperRepositoryBase` for fast micro-ORM operations.
- **Row-Level Security**: Uses `TenantRlsDapper` to enforce tenant isolation constraints.
- **Provider Settings**: Supports different DB providers via `MsSqlTenantSessionContextSetter` and `MySqlTenantSessionContextSetter`.

## Installation
```bash
dotnet add package Muonroi.Data.Dapper
```

## Quick Start
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDapperRlsServices();
```

## Ecosystem Combinations
- **With Muonroi.Data.Abstractions**: Resolves high-speed read queries while complementing generic repositories.
- **Full Stack Example**:
```csharp
builder.Services.AddDapperRlsServices()
                .AddScoped<ITenantSessionContextSetter, MsSqlTenantSessionContextSetter>();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
