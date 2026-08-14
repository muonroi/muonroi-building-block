<Muonroi.Data.Dapper>
> Dapper integration for Muonroi: lightweight read-side repository, multi-tenant query filtering, and connection factory.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.Dapper.svg)](https://www.nuget.org/packages/Muonroi.Data.Dapper/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Data.Dapper` package provides robust integration with the popular Dapper ORM. It enables high-performance read-side repositories in CQRS architectures and ensures multi-tenant data isolation by enforcing row-level security (RLS) policies.

Dapper is widely recognized as one of the fastest micro-ORMs in the .NET ecosystem, making it an excellent choice for read-heavy operations, analytics, and complex query scenarios where Entity Framework Core might introduce unnecessary overhead. This package wraps Dapper to align it with Muonroi's architectural building blocks, adding native support for logging, dependency injection, multi-tenancy, and various database providers (SQL Server, PostgreSQL, MySQL).

Use this package when you need to perform high-speed database queries, require raw SQL execution, or when building the read-side of a system, without compromising the overall architectural guarantees and tenant isolation requirements of your application.

## Features

- **Read-Side Repositories**: Includes `MDapperRepositoryBase` and interfaces designed to abstract read-heavy operations, promoting the CQRS pattern out-of-the-box.
- **Row-Level Security (RLS)**: Enforces tenant data isolation by applying query filtering at the connection and transaction level, ensuring that data leaking between tenants is structurally impossible.
- **Cross-Database Support**: Supports Microsoft SQL Server, PostgreSQL (`Npgsql`), and MySQL through configurable tenant session context setters.
- **Dapper Custom Handlers**: Provides out-of-the-box type handlers for commonly problematic types, such as `MProtobufTimestampHandler` for Google Protobuf Timestamps and `MTrimStringHandler` for string sanitization.
- **Connection Management**: Simplifies connection string resolution and connection instantiation via `MConnectionStringProvider`, seamlessly handling scoped lifecycles.
- **Entity Framework Core Synergy**: Includes integration types (like `MStringConverter`) that help bridge the gap when using Dapper alongside EF Core.

## Installation

```bash
dotnet add package Muonroi.Data.Dapper
```

## Quick Start

### Basic Configuration

Register the Dapper infrastructure and configure Row-Level Security in your `Program.cs` or `Startup.cs`:

```csharp
using Muonroi.Data.Dapper.Rls;
using Microsoft.Extensions.DependencyInjection;

// In your Startup/Program
builder.Services.AddDapperRls(options => 
{
    options.GuaranteeLevel = RlsGuaranteeLevel.Strict;
    options.UsePostgreSqlContextSetter();
});
```

### Implementing a Read-Side Repository

Create a read repository inheriting from `MDapperRepositoryBase`:

```csharp
using Dapper;
using Muonroi.Data.Dapper;
using System.Collections.Generic;
using System.Threading.Tasks;

public class OrderReadRepository : MDapperRepositoryBase
{
    public OrderReadRepository(IConnectionStringProvider connectionStringProvider) 
        : base(connectionStringProvider)
    {
    }

    public async Task<IEnumerable<OrderDto>> GetActiveOrdersAsync(string customerId)
    {
        var sql = @"
            SELECT Id, OrderDate, TotalAmount, Status 
            FROM Orders 
            WHERE CustomerId = @CustomerId AND Status = 'Active'";

        using var connection = GetConnection();
        return await connection.QueryAsync<OrderDto>(sql, new { CustomerId = customerId });
    }
}
```

### Working with Multi-Tenancy (RLS)

When Row-Level Security is enabled, queries executed through the repository will automatically run within a tenant-scoped session context:

```csharp
public async Task<IEnumerable<ProductDto>> GetTenantProductsAsync()
{
    // The underlying connection has been pre-configured with the current tenant's ID context.
    // Assuming the database is configured to apply RLS policies based on SESSION_CONTEXT.
    
    var sql = "SELECT * FROM Products";
    using var connection = GetConnection();
    return await connection.QueryAsync<ProductDto>(sql);
}
```

If you need to bypass RLS for systemic operations:

```csharp
using Muonroi.Data.Dapper.Rls.Bypass;

public class SystemMetricsJob
{
    private readonly IBypassScope _bypassScope;
    private readonly MDapperRepositoryBase _repository;

    public async Task CalculateGlobalMetricsAsync()
    {
        // Bypass RLS to query across all tenants
        using (_bypassScope.BeginBypass())
        {
            var sql = "SELECT COUNT(*) FROM Orders";
            using var connection = _repository.GetConnection();
            var totalOrders = await connection.ExecuteScalarAsync<int>(sql);
        }
    }
}
```

## Configuration

### Dapper RLS Options

The `DapperRlsOptions` object allows you to fine-tune how Row-Level Security is applied:

```csharp
services.AddDapperRls(options =>
{
    // Level of strictness for RLS enforcement (None, Warn, Strict)
    options.GuaranteeLevel = RlsGuaranteeLevel.Strict;
    
    // Choose the database provider setter (SQL Server, Postgres, MySQL)
    options.UseMsSqlContextSetter();
    
    // Custom actions during setup
    options.OnMissingTenantContext = () => throw new MissingTenantContextException();
});
```

## API Reference

### Core Abstractions

- `MDapperRepositoryBase`: The foundational abstract class for all Dapper-based repositories. Provides robust connection instantiation handling lifecycle requirements.
- `IConnectionStringProvider`: Strategy for resolving database connection strings across potentially varied configurations (e.g., per-tenant connection strings).
- `MDapperExtensions`: Helpful extension methods for standard Dapper operations, often injecting cross-cutting concerns like distributed tracing.

### Row-Level Security

- `IRlsGuaranteeProvider`: Asserts that Row-Level security guarantees are maintained prior to executing queries.
- `ITenantSessionContextSetter`: Abstraction for setting the database-level context for RLS (e.g., `sp_set_session_context` in SQL Server).
- `TenantRlsDapper`: Wraps command execution to ensure the tenant context is established prior to query dispatch.
- `BypassScope`: Permits trusted execution paths to momentarily bypass tenant restrictions (requires elevated privileges/injection).

### Handlers

- `MProtobufTimestampHandler`: Maps `Google.Protobuf.WellKnownTypes.Timestamp` directly to database `DATETIME` or `TIMESTAMP` columns.
- `MTrimStringHandler`: Ensures string parameters and results are safely trimmed.

## Integration

`Muonroi.Data.Dapper` is designed to be used in conjunction with:
- **Muonroi.Tenancy.Abstractions**: Sources the current tenant context for the `ITenantSessionContextSetter`.
- **Muonroi.Core.Abstractions**: Resolves environment and configuration constants.
- **Muonroi.Logging.Abstractions**: Wires Dapper command execution timings into the overarching telemetry systems.

## Ecosystem Combinations

> Great standalone. Becomes **significantly more powerful** when combined.

### + Data.Abstractions → IQueryRepository
Dapper implements the IQueryRepository read-side contract.

### + Tenancy.Core → Automatic RLS Filtering
All queries automatically filtered by TenantId via RLS.

### + Caching.Memory → Read-Side Caching
Cache Dapper read results:
```csharp
GetOrSetAsync("users", () => dapper.QueryAsync(...))
```

### + Diagnostics & Observability → Query Tracing
Each Dapper call creates a trace node showing SQL + params, with query duration histograms per-tenant.

### Full CQRS Stack
```csharp
builder.Services
    .AddDapperRls(config)
    .AddEntityFrameworkCore(config);
```

## Samples

See the working example in [Quickstart.Data.Dapper](../../samples/Quickstart.Data.Dapper).

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
