# Muonroi.Data.EntityFrameworkCore.Sqlite

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.Sqlite.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.Sqlite/)

> SQLite provider configuration for Muonroi DbContexts.

## Overview
Provides `SqliteDbContextConfigurator` for lightweight, file-based SQLite database connections. Ideal for local development or edge caching scenarios in the Muonroi ecosystem.

## Features
- **SQLite Setup**: Simple configuration via `SqliteDbContextConfigurator`.
- **Development Ready**: Eases the transition between SQLite in development and other providers in production.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.Sqlite
```

## Quick Start

```csharp
builder.Services.AddMuonroiDbContext<MyDbContext>(options =>
{
    var configurator = new SqliteDbContextConfigurator();
    configurator.Configure(options, "Data Source=app.db");
});
```

## Ecosystem Combinations

### Muonroi.Data.EntityFrameworkCore.Sqlite + Muonroi.Experience.Runtime
Combine `SqliteDbContextConfigurator` with local experience stores for lightweight edge-device persistence.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
