# Muonroi.Data.EntityFrameworkCore.PostgreSQL

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.PostgreSQL.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.PostgreSQL/)

> PostgreSQL provider configuration for Muonroi DbContexts.

## Overview
Provides `PostgreSqlDbContextConfigurator` to correctly configure PostgreSQL database connections with standard Muonroi resiliency and execution strategies.

## Features
- **PostgreSQL Integration**: Uses `PostgreSqlDbContextConfigurator` for standard setup.
- **Npgsql Features**: Enables Postgres-specific capabilities within the Muonroi data architecture.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.PostgreSQL
```

## Quick Start

```csharp
builder.Services.AddMuonroiDbContext<MyDbContext>(options =>
{
    var configurator = new PostgreSqlDbContextConfigurator();
    configurator.Configure(options, "Host=localhost;Database=mydb;Username=postgres;Password=password");
});
```

## Ecosystem Combinations

### Muonroi.Data.EntityFrameworkCore.PostgreSQL + Muonroi.EntityFrameworkCore.Configuration
Use `PostgreSqlDbContextConfigurator` alongside `MEntityConfigurationBase` to handle NodaTime or JSONB configurations efficiently.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
