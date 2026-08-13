# Muonroi.Data.EntityFrameworkCore.PostgreSQL

> EF Core provider package for PostgreSQL in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.PostgreSQL.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.PostgreSQL/)

This package provides the PostgreSQL implementation for `IDbContextConfigurator<T>`, enabling `Muonroi.Data.EntityFrameworkCore` to seamlessly connect to PostgreSQL databases without changing your `MDbContext` application logic.

## Features

- **PostgreSQL Provider** — Wraps the Npgsql Entity Framework Core provider.
- **Seamless Integration** — Works out-of-the-box with `AddDbContextConfigure<TDbContext, TPermission>`.
- **Automatic Configuration** — Reads `PostgreSqlConnectionString` from the configuration automatically.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.PostgreSQL --prerelease
```

## Quick Start

Ensure your `appsettings.json` specifies `PostgreSql` as the `DbType`:

```json
{
  "DatabaseConfigs": {
    "DbType": "PostgreSql",
    "ConnectionStrings": {
      "PostgreSqlConnectionString": "Host=localhost;Database=app;Username=postgres;Password=password"
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
