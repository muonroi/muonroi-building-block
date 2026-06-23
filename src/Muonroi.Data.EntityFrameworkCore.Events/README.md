# Muonroi.Data.EntityFrameworkCore.Events

> Adds event dispatch, transactional outbox persistence, and saga support on top of `Muonroi.Data.EntityFrameworkCore` — the mediator and messaging layer you reach for when your EF Core context needs to publish domain events and coordinate long-running processes.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.Events.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.Events/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Data.EntityFrameworkCore.Events` extends the persistence-only core with two complementary patterns: a **transactional outbox** (`MEventOutboxDbContext`) that stores domain events atomically with business state, and **saga persistence** (`MSagaDbContext`) that tracks long-running process state keyed by `CorrelationId` with automatic tenant stamping. Both are wired through `Muonroi.Mediator` so domain events raised during `SaveChangesAsync` are dispatched via the same pipeline as the rest of the application.

## Installation

```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.Events --prerelease
```

## Quick Start

Register the Muonroi mediator first (required by `MSagaDbContext`), then call `AddMuonroiSagaDbContext<TContext>` with your concrete context:

```csharp
using Microsoft.EntityFrameworkCore;
using Muonroi.Data.EntityFrameworkCore.Extensions;
using Muonroi.Mediator.Mediator;

// 1. Register the mediator (required — MSagaDbContext dispatches domain events through it)
builder.Services.AddMMediator();

// 2. Register your saga context
builder.Services.AddMuonroiSagaDbContext<OrderSagaDbContext>(options =>
    options.UseSqlServer(connectionString));
```

Define your saga state by implementing `IMuonroiSaga` and derive your context from `MSagaDbContext`:

```csharp
using Muonroi.Messaging.Abstractions.Contracts;

public class OrderSaga : IMuonroiSaga
{
    public Guid CorrelationId { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }

    // your domain fields
    public string State { get; set; } = "Pending";
    public decimal Amount { get; set; }
}

public class OrderSagaDbContext(
    DbContextOptions options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<MDbContext>? logger = null,
    IMDateTimeService? dateTimeService = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null)
    : MSagaDbContext(options, mediator, licenseGuard, logger, dateTimeService, executionContextAccessor)
{
    public DbSet<OrderSaga> OrderSagas => Set<OrderSaga>();
}
```

`MSagaDbContext.OnModelCreating` discovers every `IMuonroiSaga` entity automatically, sets `CorrelationId` as the primary key, and adds an index on `TenantId`. `SaveChangesAsync` stamps `CreationTime` / `LastModificationTime` and injects the ambient `TenantId` from `ISystemExecutionContextAccessor` on new entries.

## Features

- **Saga persistence** — `MSagaDbContext` auto-configures any `IMuonroiSaga` entity: `CorrelationId` PK, `TenantId` index, creation/modification timestamps stamped on every save
- **Transactional outbox** — `MEventOutboxDbContext` persists `EventOutbox` entries and a `MessageInbox` deduplication table inside the same EF Core transaction as business state
- **Mediator integration** — `MSagaDbContext` requires `IMediator`; domain events raised during persistence flow through the Muonroi mediator pipeline
- **Design-time support** — `SharedDbContextFactory<TContext>` resolves `DatabaseConfigs` from `appsettings.json` at migration time; supports SQL Server, MySQL, PostgreSQL, and SQLite
- **Multi-provider** — design-time factory selects the provider branch from `DatabaseConfigs:DbType`; connection strings are decrypted via `MStringExtension.DecryptConfigurationValue`
- **Tenant-aware** — saga entities auto-inherit `TenantId` from the ambient execution context when not explicitly set

## Configuration

Register via `AddMuonroiSagaDbContext<TContext>` from `Muonroi.Data.EntityFrameworkCore.Extensions`:

```csharp
builder.Services.AddMMediator();                                    // required
builder.Services.AddMuonroiSagaDbContext<OrderSagaDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

For the design-time factory (`dotnet ef migrations add`), add the following section to `appsettings.json`:

```json
{
  "DatabaseConfigs": {
    "DbType": "SqlServer",
    "ConnectionStrings": {
      "SqlServerConnectionString": "<your-connection-string>"
    }
  }
}
```

Supported `DbType` values: `SqlServer`, `MySql`, `PostgreSql`, `Sqlite`.

## API Reference

| Type | Purpose |
|------|---------|
| `MSagaDbContext` | Abstract base context for saga state; auto-configures `IMuonroiSaga` entities (PK, index, timestamps) and dispatches domain events via `IMediator` |
| `MEventOutboxDbContext` | Concrete outbox context exposing `DbSet<EventOutbox>` and `DbSet<MessageInbox>`; implements `IEventOutboxStore` |
| `IMuonroiSaga` | Contract (from `Muonroi.Messaging.Abstractions`) all saga state classes must implement; defines `CorrelationId`, `TenantId`, and timestamp fields |
| `IEventOutboxStore` | Contract (from `Muonroi.Messaging.Abstractions`) for querying and appending `EventOutbox` entries |
| `MessageInbox` | Entity for at-least-once deduplication; keyed by `MessageId` + `ConsumerName` |
| `SharedDbContextFactory<TContext>` | `IDesignTimeDbContextFactory<TContext>` implementation; reads `DatabaseConfigs` from `appsettings.json` at migration time |
| `MuonroiSagaServiceCollectionExtensions` | Provides `AddMuonroiSagaDbContext<TContext>(IServiceCollection, Action<DbContextOptionsBuilder>)` |

## Samples

- [Quickstart.Data.Events](../../samples/Quickstart.Data.Events/) — minimal ASP.NET Core API demonstrating `AddMuonroiSagaDbContext`, `MSagaDbContext`, and `IMuonroiSaga` using an in-memory provider

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — persistence-only core (`MDbContext`); this package builds on top of it
- [`Muonroi.Messaging.Abstractions`](../Muonroi.Messaging.Abstractions/) — defines `IMuonroiSaga`, `IEventOutboxStore`, and `EventOutbox`
- [`Muonroi.Mediator`](../Muonroi.Mediator/) — provides `IMediator` and `AddMMediator`; required for domain-event dispatch

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
