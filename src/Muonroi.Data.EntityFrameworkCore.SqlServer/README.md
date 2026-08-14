# Muonroi.Data.EntityFrameworkCore.SqlServer

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.SqlServer.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.SqlServer/)

> SQL Server provider configuration for Muonroi DbContexts.

## Overview
Provides `SqlServerDbContextConfigurator` to correctly configure Microsoft SQL Server database connections, incorporating standard retry policies and execution strategies for enterprise resilience.

## Features
- **SQL Server Integration**: Utilizes `SqlServerDbContextConfigurator` for robust setup.
- **Transient Error Handling**: Configured execution strategy for Azure SQL and on-premise environments.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.SqlServer
```

## Quick Start

```csharp
builder.Services.AddMuonroiDbContext<MyDbContext>(options =>
{
    var configurator = new SqlServerDbContextConfigurator();
    configurator.Configure(options, "Server=.;Database=mydb;Integrated Security=True;");
});
```

## Ecosystem Combinations

### Muonroi.Data.EntityFrameworkCore.SqlServer + Muonroi.Governance.Enterprise
Leverage `SqlServerDbContextConfigurator` to reliably persist `AuditTrailTenantPartition` data in highly available enterprise deployments.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
