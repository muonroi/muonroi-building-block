# Muonroi.Data.EntityFrameworkCore.SqlServer

> EF Core provider package for SQL Server in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.SqlServer.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.SqlServer/)

This package provides the SQL Server implementation for `IDbContextConfigurator<T>`, enabling `Muonroi.Data.EntityFrameworkCore` to seamlessly connect to SQL Server databases without changing your `MDbContext` application logic.

## Features

- **SQL Server Provider** — Wraps the official Microsoft Entity Framework Core SQL Server provider.
- **Seamless Integration** — Works out-of-the-box with `AddDbContextConfigure<TDbContext, TPermission>`.
- **Automatic Configuration** — Reads `SqlServerConnectionString` from the configuration automatically.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.SqlServer --prerelease
```

## Quick Start

Ensure your `appsettings.json` specifies `SqlServer` as the `DbType`:

```json
{
  "DatabaseConfigs": {
    "DbType": "SqlServer",
    "ConnectionStrings": {
      "SqlServerConnectionString": "Server=(localdb)\\mssqllocaldb;Database=app;Trusted_Connection=True;MultipleActiveResultSets=true"
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
