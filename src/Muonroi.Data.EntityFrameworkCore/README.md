# Muonroi.Data.EntityFrameworkCore

> EF Core infrastructure for Muonroi services: a batteries-included `MDbContext` with automatic audit timestamping, soft-delete, multi-tenant global query filters, domain-event dispatch, and a generic repository base.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

Writing EF Core boilerplate — audit columns, soft-delete interceptors, tenant isolation filters, and transaction helpers — is repetitive across every service. This package provides `MDbContext` and `MRepository<T>` as drop-in bases that handle all of that automatically. Provider selection (SQL Server, PostgreSQL, MySQL, SQLite, MongoDB), permission sync, and authentication repositories are wired via a single `AddDbContextConfigure<TDbContext, TPermission>` extension.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore --prerelease
```

## Quick Start

### 1. Define your context

Subclass `MDbContext` and add your application `DbSet` properties:

```csharp
using Microsoft.EntityFrameworkCore;
using Muonroi.Data.EntityFrameworkCore.Entity;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : MDbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Wires audit/soft-delete/tenant filters for every entity, including Order.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(e => e.Property(x => x.Reference).HasMaxLength(64));
    }
}
```

### 2. Register via DI (production path)

`AddDbContextConfigure` reads `DatabaseConfigs` from `appsettings.json`, selects the correct provider, registers `ITenantContext`, `IPermissionSyncService`, auth repositories, and the license interceptor:

```csharp
// Program.cs
builder.Services.AddDbContextConfigure<AppDbContext, AppPermission>(
    builder.Configuration);
```

`appsettings.json`:

```json
{
  "DatabaseConfigs": {
    "DbType": "PostgreSql",
    "ConnectionStrings": {
      "PostgreSqlConnectionString": "Host=localhost;Database=app;..."
    }
  }
}
```

Supported `DbType` values: `SqlServer`, `MySql`, `PostgreSql`, `Sqlite`, `MongoDb`.

### 3. Use a repository

```csharp
public class OrderService(MDbContext db)
{
    // MDbContext.Set<T>() is safe for core operations.
    // For a full repository, extend MRepository<T>.
}

public class OrderRepository(
    MDbContext dbContext,
    IAuthenticateInfoContext authContext,
    ILicenseGuard licenseGuard,
    IMDateTimeService dateTimeService)
    : MRepository<Order>(dbContext, authContext, licenseGuard, dateTimeService)
{
    public Task<int> CreateAsync(Order order) => AddBatchAsync([order]);
}
```

## Features

- **Automatic audit stamping** — `SaveChangesAsync` sets `CreationTime`, `CreatorUserId`, `LastModificationTime`, `LastModificationUserId`, and Unix timestamp variants on every `MEntity` without any caller code.
- **Soft-delete** — `EntityState.Deleted` is intercepted and converted to an `IsDeleted = true` update; deleted rows are filtered out of all queries automatically.
- **Multi-tenant global query filters** — entities implementing `ITenantScoped` get an EF global filter keyed to `TenantContext.CurrentTenantId`; `TenantContext.AllowCrossTenantAccess` bypasses it for admin operations.
- **Domain-event dispatch** — `SaveEntitiesAsync` wraps save + `IMediator.Publish` in a resilient execution strategy; events are tracked via `TrackEntity(MEntity)`.
- **Transaction helpers** — `BeginTransactionAsync`, `CommitTransactionAsync`, `RollbackTransaction`, and `ExecuteTransactionAsync(Func<Task<MVoidMethodResult>>)` simplify two-phase operations.
- **Multi-provider support** — `AddDbContextConfigure` selects `IDbContextConfigurator` for SQL Server, PostgreSQL, MySQL, SQLite, or MongoDB based on configuration; no code changes needed to switch providers.
- **Permission sync** — registers `IPermissionSyncService` and scans assemblies for `IPermissionProvider` implementations.
- **Authentication repositories** — registers `IAuthenticateRepository`, `IRefreshTokenValidator`, and `MAuthenticateTokenHelper<TPermission>`.
- **Tenant quota management** — opt-in via `AddTenantQuotaManagement<TContext>()` to register `ITenantQuotaStore` and `ITenantQuotaTracker`.
- **WebAuthn credential store** — opt-in via `AddEfWebAuthnCredentialStore()`.
- **Bulk insert** — `MRepository<T>.BulkInsertAsync` delegates to `DbContext.BulkInsertAsync` for high-throughput writes.
- **Encrypted connection strings** — opt-in via `EnableEncryption: true` in configuration; decryption is performed inside `ILicenseGuard.DecryptSecurely`.

## Configuration

### appsettings.json structure

```json
{
  "DatabaseConfigs": {
    "DbType": "SqlServer",
    "ConnectionStrings": {
      "SqlServerConnectionString": "Server=...;Database=...;...",
      "PostgreSqlConnectionString": "",
      "MySqlConnectionString": "",
      "SqliteConnectionString": "",
      "MongoDbConnectionString": ""
    }
  },
  "MultiTenantOptions": {
    "Enabled": true
  },
  "EnableEncryption": false
}
```

### Optional extensions

```csharp
// Tenant quota tracking (ITenantQuotaStore + ITenantQuotaTracker)
services.AddTenantQuotaManagement<AppDbContext>();

// WebAuthn credential store
services.AddEfWebAuthnCredentialStore();

// Permission provider assembly scan (called automatically by AddDbContextConfigure)
services.AddPermissionProviders(typeof(AppDbContext).Assembly);
```

## API Reference

| Type | Purpose |
|------|---------|
| `MDbContext` | Base `DbContext` — audit, soft-delete, tenant filters, domain-event dispatch, transaction helpers; ships built-in `DbSet`s for Identity entities |
| `MDbContextBase<TContext>` | Lighter abstract base for schema-divergent multi-tenancy; no built-in Identity DbSets; override `ConfigureSiteSpecific(ModelBuilder)` |
| `MRepository<T>` | Generic repository base for any `MEntity`; provides `Add`, `AddBatchAsync`, `AddOrUpdateBatchAsync`, `UpdateAsync`, `UpdateBatchAsync`, `DeleteAsync`, `DeleteBatchAsync`, `BulkInsertAsync`, `SoftRestoreAsync`, `ExecuteTransactionAsync` |
| `MDbContextConfiguration` (static) | Hosts `AddDbContextConfigure<TDbContext, TPermission>` — the single DI entry point |
| `TenantQuotaEfServiceCollectionExtensions` (static) | `AddTenantQuotaManagement<TContext>` |
| `PermissionProviderExtensions` (static) | `AddPermissionProviders(params Assembly[])` |
| `IDbContextConfigurator<T>` | Provider-specific configuration strategy (SQL Server / PostgreSQL / MySQL / SQLite / MongoDB implementations included) |
| `IPermissionSyncService` | Syncs declared permissions to the database |
| `EfTenantQuotaStore<TContext>` | EF implementation of `ITenantQuotaStore` |

## Samples

- [Quickstart.Data.EntityFrameworkCore](../../samples/Quickstart.Data.EntityFrameworkCore/) — minimal ASP.NET Core API demonstrating `MDbContext` subclassing, audit timestamping, and soft-delete with the EF Core in-memory provider

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Data.Abstractions`](../Muonroi.Data.Abstractions/) — contracts (`IMRepository<T>`, `IMUnitOfWork`, `IMDataContext`) implemented by this package
- [`Muonroi.Core`](../Muonroi.Core/) — `MEntity` base class and core domain primitives
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — `ITenantContext`, `TenantContext`, `TenantSchemaSelector` used by the global query filters
- [`Muonroi.EntityFrameworkCore.Configuration`](../Muonroi.EntityFrameworkCore.Configuration/) — `DatabaseConfigs` options type and `DbTypes` enum
- [`Muonroi.Data.Dapper`](../Muonroi.Data.Dapper/) — alternative Dapper-based data layer for raw SQL / Dapper workloads

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
