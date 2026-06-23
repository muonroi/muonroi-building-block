# Muonroi.Data.Abstractions

> Repository, unit-of-work, and entity contracts that decouple your domain from any specific ORM or database driver.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Data.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships **contracts only** — interfaces, marker types, and the `MultiDbUnitOfWork` coordinator class. There is no runtime ORM behavior here. Your application domain layer takes a dependency on these abstractions; the implementation is provided by `Muonroi.Data.EntityFrameworkCore` (EF Core) or `Muonroi.Data.Dapper` (raw SQL / RLS), both of which reference this package.

## Installation

```bash
dotnet add package Muonroi.Data.Abstractions --prerelease
```

## Quick Start

Because this is a contracts package, the typical usage is **implementing** the interfaces in an infrastructure project and **consuming** them in the domain/application layer.

### 1. Define your entity

```csharp
using Muonroi.Data.Abstractions.Entities;

// Implement IEntityBase<TKey> for a typed primary key.
// Add IAuditable<Guid> to track created/updated timestamps and user.
public class Order : IEntityBase<long>, IAuditable<Guid>
{
    public long Id { get; set; }
    public string Reference { get; set; } = string.Empty;

    // IAuditable<Guid>
    public DateTime? CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
```

### 2. Declare a repository interface using IMRepositoryBase

```csharp
using Muonroi.Data.Abstractions.Repositories;

// IMRepositoryBase<T> accepts any class implementing IEntityBase —
// no MEntity inheritance required.
public interface IOrderRepository : IMRepositoryBase<Order>
{
    Task<Order?> FindByReferenceAsync(string reference, CancellationToken ct = default);
}
```

### 3. Use the unit of work in an application service

```csharp
using Muonroi.Data.Abstractions.UnitOfWork;

public class PlaceOrderHandler(IOrderRepository orders)
{
    public async Task HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = new Order { Reference = cmd.Reference };
        orders.Add(order);
        await orders.UnitOfWork.SaveEntitiesAsync(ct);
    }
}
```

The concrete `IOrderRepository` implementation (EF Core or Dapper) is registered by the infrastructure package — see [Muonroi.Data.EntityFrameworkCore](../Muonroi.Data.EntityFrameworkCore/) or [Muonroi.Data.Dapper](../Muonroi.Data.Dapper/).

### 4. Coordinate multiple DbContexts with MultiDbUnitOfWork

```csharp
using Muonroi.Data.Abstractions.UnitOfWork;

// Pass any number of IMDataContext implementors.
var uow = new MultiDbUnitOfWork(primaryContext, auditContext);
int written = await uow.SaveChangesAsync(cancellationToken);
```

## Features

- **`IMRepository<T>`** — full CRUD contract for entities inheriting `MEntity`: `Add`, `UpdateAsync`, `DeleteAsync`, `AddBatchAsync`, `AddOrUpdateBatchAsync`, `UpdateBatchAsync`, `DeleteBatchAsync`, `BulkInsertAsync`, `SoftRestoreAsync`, transactional `ExecuteTransactionAsync` / `RollbackTransactionAsync`.
- **`IMRepositoryBase<T>`** — relaxed variant that accepts any `class, IEntityBase` (no `MEntity` inheritance); adds `ExecuteStoredProcedureAsync` and `ExecuteStoredProcedureScalarAsync`.
- **`IMQueries<T>`** — read-side contract: `GetByIdAsync`, `GetByGuidAsync`, `GetAllAsync` (plain and paged), `GetByConditionAsync`, `GetPagedAsync<TDto>`, `FirstOrDefaultAsync`, `AnyAsync`, `ExistsAsync`, `CountAsync`.
- **`IMUnitOfWork`** — `SaveChangesAsync` (returns row count) and `SaveEntitiesAsync` (returns correlation `Guid`).
- **`IMDataContext`** — thin `SaveChangesAsync` contract for a single context.
- **`MultiDbUnitOfWork`** — concrete coordinator that fans out `SaveChangesAsync` across multiple `IMDataContext` instances in sequence.
- **`IEntityBase` / `IEntityBase<TKey>`** — marker and typed-key entity contracts.
- **`IAuditable` / `IAuditable<TUserKey>`** — `CreatedDate`, `UpdatedDate`, and optional typed `CreatedBy` / `UpdatedBy`.
- **`ISiteScoped`** — `SiteCode` property for schema-divergent multi-tenancy scenarios.

## API Reference

| Type | Kind | Purpose |
|------|------|---------|
| `IMRepository<T>` | Interface | Full write contract for `MEntity`-derived entities |
| `IMRepositoryBase<T>` | Interface | Relaxed write contract for any `IEntityBase` entity; adds stored-procedure helpers |
| `IMQueries<T>` | Interface | Read-side queries: by id, by guid, paged, filtered, projected |
| `IMUnitOfWork` | Interface | Saves changes; exposes `SaveChangesAsync` (int) and `SaveEntitiesAsync` (Guid) |
| `IMDataContext` | Interface | Single-context save contract; implemented by EF DbContext wrappers |
| `MultiDbUnitOfWork` | Class | Fans out `SaveChangesAsync` across multiple `IMDataContext` instances |
| `IEntityBase` | Interface | Marker for all Muonroi entities |
| `IEntityBase<TKey>` | Interface | Adds typed `Id` property |
| `IAuditable` | Interface | `CreatedDate` / `UpdatedDate` timestamps |
| `IAuditable<TUserKey>` | Interface | Extends `IAuditable` with typed `CreatedBy` / `UpdatedBy` |
| `ISiteScoped` | Interface | `SiteCode` for site-scoped multi-tenancy |

## Samples

No standalone sample is provided for this contracts package. See the implementation packages for runnable examples:

- [Muonroi.Data.EntityFrameworkCore](../Muonroi.Data.EntityFrameworkCore/) — EF Core implementation
- [Muonroi.Data.Dapper](../Muonroi.Data.Dapper/) — Dapper + MSSQL RLS implementation

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — EF Core implementation of `IMRepository<T>`, `IMUnitOfWork`, and `IMDataContext`
- [`Muonroi.Data.EntityFrameworkCore.Events`](../Muonroi.Data.EntityFrameworkCore.Events/) — outbox / saga DbContext extensions built on top of EF Core
- [`Muonroi.Data.Dapper`](../Muonroi.Data.Dapper/) — Dapper implementation with RLS support and stored-procedure helpers
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — core models (`MEntity`, `MPagedResult`, `MVoidMethodResult`) referenced by this package

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
