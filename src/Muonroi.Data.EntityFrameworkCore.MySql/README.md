# Muonroi.Data.EntityFrameworkCore.MySql

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.MySql.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.MySql/)

> MySQL provider configuration for Muonroi DbContexts.

## Overview
Provides `MySqlDbContextConfigurator` to correctly configure MySQL/MariaDB database connections with standard Muonroi resiliency and connection string conventions.

## Features
- **MySQL Integration**: Uses `MySqlDbContextConfigurator` for seamless setup.
- **Resiliency**: Configures execution strategies suitable for MySQL.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.MySql
```

## Quick Start

```csharp
builder.Services.AddMuonroiDbContext<MyDbContext>(options =>
{
    var configurator = new MySqlDbContextConfigurator();
    configurator.Configure(options, "Server=localhost;Database=mydb;Uid=root;Pwd=password;");
});
```

## Ecosystem Combinations

### Muonroi.Data.EntityFrameworkCore.MySql + Muonroi.EntityFrameworkCore.Configuration
Use `MySqlDbContextConfigurator` alongside `MEntityConfigurationBase` to apply standard column mappings to MySQL-specific data types.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
