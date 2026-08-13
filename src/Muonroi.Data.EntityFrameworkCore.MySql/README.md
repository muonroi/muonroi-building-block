# Muonroi.Data.EntityFrameworkCore.MySql

> EF Core provider package for MySQL in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.MySql.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.MySql/)

This package provides the MySQL implementation for `IDbContextConfigurator<T>`, enabling `Muonroi.Data.EntityFrameworkCore` to seamlessly connect to MySQL (and MariaDB) databases without changing your `MDbContext` application logic.

## Features

- **MySQL Provider** — Wraps the Pomelo Entity Framework Core provider for MySQL.
- **Seamless Integration** — Works out-of-the-box with `AddDbContextConfigure<TDbContext, TPermission>`.
- **Automatic Configuration** — Reads `MySqlConnectionString` from the configuration automatically.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.MySql --prerelease
```

## Quick Start

Ensure your `appsettings.json` specifies `MySql` as the `DbType`:

```json
{
  "DatabaseConfigs": {
    "DbType": "MySql",
    "ConnectionStrings": {
      "MySqlConnectionString": "Server=localhost;Database=app;Uid=root;Pwd=password;"
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
