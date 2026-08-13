# Muonroi.Data.EntityFrameworkCore.Sqlite

> EF Core provider package for SQLite in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.Sqlite.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.Sqlite/)

This package provides the SQLite implementation for `IDbContextConfigurator<T>`, enabling `Muonroi.Data.EntityFrameworkCore` to seamlessly connect to SQLite databases without changing your `MDbContext` application logic.

## Features

- **SQLite Provider** — Wraps the official Microsoft Entity Framework Core SQLite provider.
- **Seamless Integration** — Works out-of-the-box with `AddDbContextConfigure<TDbContext, TPermission>`.
- **Automatic Configuration** — Reads `SqliteConnectionString` from the configuration automatically.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.Sqlite --prerelease
```

## Quick Start

Ensure your `appsettings.json` specifies `Sqlite` as the `DbType`:

```json
{
  "DatabaseConfigs": {
    "DbType": "Sqlite",
    "ConnectionStrings": {
      "SqliteConnectionString": "Data Source=app.db"
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
