# Muonroi.EntityFrameworkCore.Configuration

> Composable EF Core entity configuration with a template method pattern for schema-divergent multi-tenancy.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.EntityFrameworkCore.Configuration.svg)](https://www.nuget.org/packages/Muonroi.EntityFrameworkCore.Configuration/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

In multi-tenant deployments each site shares 70–80% of the same schema but diverges on a handful of column names, lengths, and constraints. This package provides two mechanisms to express that divergence cleanly: a base class (`MEntityConfigurationBase<TEntity>`) that separates core schema from site-specific overrides via ordered template methods, and a `[SiteColumn]` attribute (applied by `SiteColumnExtensions.ApplySiteColumnOverrides`) for lightweight declarative per-site remapping. It depends only on `Muonroi.Core.Abstractions` and `Microsoft.EntityFrameworkCore.Relational`.

## Installation

```bash
dotnet add package Muonroi.EntityFrameworkCore.Configuration --prerelease
```

## Quick Start

### Option A — virtual method overrides (`MEntityConfigurationBase<TEntity>`)

Derive one configuration class per entity in your core library and override site-specific hooks in each site project:

```csharp
// Core library — shared schema for all sites
public class OrderConfiguration : MEntityConfigurationBase<Order>
{
    protected override void ConfigureTable(EntityTypeBuilder<Order> builder)
        => builder.ToTable("ORDERS").HasKey(e => e.Id);

    protected override void ConfigureCoreColumns(EntityTypeBuilder<Order> builder)
    {
        builder.Property(e => e.OrderNo).HasColumnName("ORDER_NO").HasMaxLength(50);
        builder.Property(e => e.Status).HasColumnName("STATUS").IsRequired();
    }
}

// Site "BRAVO" — override only what differs
public class BravoOrderConfiguration : OrderConfiguration
{
    protected override void ConfigureSiteColumns(EntityTypeBuilder<Order> builder)
        => builder.Property(e => e.OrderNo).HasColumnName("BOOKING_NUMBER").HasMaxLength(25);
}
```

Register via EF Core's assembly scan inside `DbContext.OnModelCreating`:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(BravoOrderConfiguration).Assembly);
```

### Option B — `[SiteColumn]` attribute + `ApplySiteColumnOverrides`

Annotate a site-specific entity class and call the extension once during model building:

```csharp
public class BravoOrder
{
    public long Id { get; set; }

    [SiteColumn(Name = "BOOKING_NUMBER", MaxLength = 25)]
    public string? BookingNo { get; set; }

    [SiteColumn(IsRequired = true, DefaultValue = "N")]
    public string? Status { get; set; }

    // No attribute → convention: CONTAINER_NO
    public string? ContainerNo { get; set; }
}
```

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplySiteColumnOverrides<BravoOrder>("BRAVO");
}
```

Properties without `[SiteColumn]` are automatically mapped to `UPPER_SNAKE_CASE` by convention (e.g. `ContainerNo` → `CONTAINER_NO`).

## Features

- **Template method base class** — `MEntityConfigurationBase<TEntity>` calls five ordered hooks: `ConfigureTable` → `ConfigureCoreColumns` → `ConfigureCoreIndexes` → `ConfigureSiteColumns` → `ConfigureSiteIndexes`. Core methods are abstract; site methods are virtual no-ops, making them optional to override.
- **EF-native discovery** — implements `IEntityTypeConfiguration<TEntity>`, so `ApplyConfigurationsFromAssembly` picks up all configurations without manual registration.
- **Declarative site column overrides** — `[SiteColumn]` attribute supports column name, max length, required constraint, default value, column type, and ignore (exclude from mapping).
- **UPPER_SNAKE_CASE convention** — `SiteColumnExtensions.ApplySiteColumnOverrides` applies `UPPER_SNAKE_CASE` to all unmapped properties automatically, keeping convention consistent with the Dapper/`ISiteColumnMap` layer.
- **Shared naming convention** — `ToUpperSnakeCase` delegates to `ColumnNamingConvention.ToUpperSnakeCase` from `Muonroi.Core.Abstractions`, the single source of truth for column naming across EF Core and Dapper.

## Configuration

No DI registration is required. `MEntityConfigurationBase<TEntity>` is consumed by EF Core's model-building pipeline directly. `ApplySiteColumnOverrides` is called inside `DbContext.OnModelCreating`.

## API Reference

| Type | Purpose |
|------|---------|
| `MEntityConfigurationBase<TEntity>` | Abstract base implementing `IEntityTypeConfiguration<TEntity>`. Calls five template methods in a fixed order. |
| `SiteColumnAttribute` | Attribute (`[AttributeUsage(Property)]`) for declarative per-site column overrides: `Name`, `MaxLength`, `IsRequired`, `DefaultValue`, `HasColumnType`, `Ignore`. |
| `SiteColumnExtensions` | Static class. `ApplySiteColumnOverrides<TEntity>(ModelBuilder, string siteId)` reads `[SiteColumn]` attributes and applies overrides; falls back to `UPPER_SNAKE_CASE` convention for unannotated properties. |

## Samples

- [TestProject.Service](../../samples/TestProject.Service/) — demonstrates `[SiteColumn]` attribute on `BravoOrder` and `ApplySiteColumnOverrides` wired in a minimal `DbContext`.
- [TestProject.Service.IntegrationTests](../../samples/TestProject.Service.IntegrationTests/) — xUnit tests verifying `[SiteColumn]` attribute inspection and `ApplySiteColumnOverrides` column mapping via the EF Core in-memory provider.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `ColumnNamingConvention.ToUpperSnakeCase`, the shared naming source used by this package.
- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — the higher-level EF Core package (`MDbContext`, audit timestamping, soft-delete). Use `MEntityConfigurationBase<TEntity>` inside a `MDbContext` subclass for the full stack.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) at the repository root.
