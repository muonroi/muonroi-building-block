# Muonroi.Services

> Generic EF Core CRUD base class with virtual hook points for schema-divergent multi-tenancy — inherit once, override only what differs per site.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Services.svg)](https://www.nuget.org/packages/Muonroi.Services/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Services` provides `MServiceBase<TEntity, TDto>`, an abstract EF Core service base that ships fully-functional Create / Read / Update / Delete operations out of the box. Every operation calls a lifecycle hook sequence; sites override only the hooks that differ without touching the shared CRUD logic. Data access always uses `DbContext.Set<TEntity>()` so the base is agnostic to concrete `DbSet` property names across divergent schemas.

This is an implementation layer that couples to `DbContext` by design. It is not a vendor-neutral contract; there is no `.Abstractions` counterpart.

## Installation

```bash
dotnet add package Muonroi.Services --prerelease
```

## Quick Start

**1. Define your entity (implements `IEntityBase<TKey>`) and DTO:**

```csharp
using Muonroi.Data.Abstractions.Entities;

public sealed class Product : IEntityBase<long>
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class ProductDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

**2. Implement `IEntityMapper<TEntity, TDto>` (or extend `EntityMapperBase<TEntity, TDto>` from `Muonroi.Mapping.Abstractions`):**

```csharp
using Muonroi.Mapping.Abstractions;

public sealed class ProductMapper : EntityMapperBase<Product, ProductDto>
{
    protected override void MapCoreToDto(Product entity, ProductDto dto)
    {
        dto.Id = entity.Id;
        dto.Name = entity.Name;
        dto.Price = entity.Price;
    }

    protected override void MapCoreToEntity(ProductDto dto, Product entity)
    {
        entity.Name = dto.Name;
        entity.Price = dto.Price;
    }
}
```

**3. Inherit `MServiceBase` and override only the hooks you need:**

```csharp
using Muonroi.Mapping.Abstractions;
using Muonroi.Services;

public sealed class ProductService(AppDbContext context, IEntityMapper<Product, ProductDto> mapper)
    : MServiceBase<Product, ProductDto>(context, mapper)
{
    // Set a site-specific default before the entity is saved for the first time.
    protected override void ApplyDefaultValues(Product entity)
    {
        if (entity.Price <= 0)
            entity.Price = 0.99m;
    }
}
```

**4. Register in DI and use:**

```csharp
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseInMemoryDatabase("my-db"));

builder.Services.AddScoped<IEntityMapper<Product, ProductDto>, ProductMapper>();
builder.Services.AddScoped<ProductService>();
```

```csharp
// In a controller or minimal-API handler:
ProductDto created = await productService.CreateAsync(dto, ct);
ProductDto? found  = await productService.GetByIdAsync(created.Id, ct);
```

There is no `AddXxx` extension method — `MServiceBase` is a base class, not a framework component. Register each concrete service directly with `AddScoped` / `AddTransient`.

## Features

- **Full CRUD** — `CreateAsync`, `GetByIdAsync`, `GetByConditionAsync`, `UpdateAsync`, `DeleteAsync` are provided and `virtual` (override when needed).
- **Lifecycle hooks** — six override points fired in order: `ValidateAsync` → `ApplyDefaultValues` → `BeforeCreate` → save → `AfterCreate` (and `BeforeUpdate` / `AfterUpdate` for updates).
- **Schema-agnostic data access** — all queries go through `DbContext.Set<TEntity>()`, not named `DbSet` properties; works across tenant schemas without modification.
- **Guard-checked dependencies** — `Context` and `Mapper` are validated non-null at construction time via `MGuard.NotNull`.
- **Predicate queries** — `GetByConditionAsync(Expression<Func<TEntity, bool>>)` executes a server-side `WHERE` clause and maps all matching rows to DTOs.

## Configuration

No options class or `appsettings` section. Wire up:

| Registration | Reason |
|---|---|
| `AddDbContext<TContext>` | Provides the `DbContext` injected into the base constructor. |
| `AddScoped<IEntityMapper<TEntity, TDto>, TMapper>` | Required mapping contract. |
| `AddScoped<TService>` | Your concrete `MServiceBase` subclass. |

## API Reference

| Type | Purpose |
|------|---------|
| `MServiceBase<TEntity, TDto>` | Abstract base; inherit to get CRUD + hooks. Requires `TEntity : IEntityBase`, `TDto : class`. |
| `Task<TDto?> GetByIdAsync<TKey>(TKey id, CancellationToken)` | Finds by primary key; returns `null` when not found. |
| `Task<List<TDto>> GetByConditionAsync(Expression<Func<TEntity, bool>>, CancellationToken)` | Server-side filtered list query. |
| `Task<TDto> CreateAsync(TDto, CancellationToken)` | Runs `ValidateAsync → ApplyDefaultValues → BeforeCreate → SaveChanges → AfterCreate`. |
| `Task<TDto> UpdateAsync(TEntity, TDto, CancellationToken)` | Applies mapper update, runs `ValidateAsync → BeforeUpdate → SaveChanges → AfterUpdate`. |
| `Task<bool> DeleteAsync(TEntity, CancellationToken)` | Removes entity; returns `true` when `SaveChanges` affected at least one row. |
| `ValidateAsync(TEntity, CancellationToken)` | Override to throw or use a result pattern for business validation. Default: no-op. |
| `ApplyDefaultValues(TEntity)` | Override to set site-specific defaults on new entities. Default: no-op. |
| `BeforeCreate(TEntity, CancellationToken)` | Override for pre-save enrichment (code generation, reference resolution). Default: no-op. |
| `AfterCreate(TEntity, CancellationToken)` | Override for post-commit side effects (events, notifications). Default: no-op. |
| `BeforeUpdate(TEntity, CancellationToken)` | Override for pre-update enrichment or change detection. Default: no-op. |
| `AfterUpdate(TEntity, CancellationToken)` | Override for post-update side effects. Default: no-op. |

## Samples

- [Quickstart.Services](../../samples/Quickstart.Services/) — minimal ASP.NET Core API wiring `MServiceBase` over an in-memory EF Core context with an `ApplyDefaultValues` hook override and Swagger UI.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Data.Abstractions`](../Muonroi.Data.Abstractions/) — supplies `IEntityBase` / `IEntityBase<TKey>` that entity types must implement.
- [`Muonroi.Mapping.Abstractions`](../Muonroi.Mapping.Abstractions/) — supplies `IEntityMapper<TEntity, TDto>` and `EntityMapperBase<TEntity, TDto>` used by the base constructor.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
