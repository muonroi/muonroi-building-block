# Muonroi.Mapping.Abstractions

> Entity-DTO mapping contracts with a template method base class for schema-divergent multi-tenancy.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Mapping.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Mapping.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This is a **contracts-only** package — it ships `IEntityMapper<TEntity, TDto>` and `EntityMapperBase<TEntity, TDto>` with no runtime behavior of its own. The concrete mapping logic lives in your application or in a package that depends on this one (e.g. `Muonroi.Services`, which requires `IEntityMapper<TEntity, TDto>` to be registered in DI).

The template method pattern in `EntityMapperBase` separates **core fields** (shared across all tenants, always mapped) from **site-specific fields** (schema-divergent, overridden per deployment), making it straightforward to support multi-tenant schemas without branching in service code.

## Installation

```bash
dotnet add package Muonroi.Mapping.Abstractions --prerelease
```

## Quick Start

### 1. Implement the mapper

Derive from `EntityMapperBase<TEntity, TDto>` and implement the two abstract methods. Override the virtual site-specific hooks only when a tenant deployment has extra schema columns.

```csharp
using Muonroi.Mapping.Abstractions;

public sealed class ProductMapper : EntityMapperBase<Product, ProductDto>
{
    // Required: map shared fields from entity → DTO.
    protected override void MapCoreToDto(Product entity, ProductDto dto)
    {
        dto.Id    = entity.Id;
        dto.Name  = entity.Name;
        dto.Price = entity.Price;
    }

    // Required: map mutable fields from DTO → entity.
    // Id is managed by the store; omit it here.
    protected override void MapCoreToEntity(ProductDto dto, Product entity)
    {
        entity.Name  = dto.Name;
        entity.Price = dto.Price;
    }

    // Optional: map site-specific (schema-divergent) fields.
    // Default implementation is a no-op — override only when needed.
    // protected override void MapSiteSpecificToDto(Product entity, ProductDto dto) { ... }
    // protected override void MapSiteSpecificToEntity(ProductDto dto, Product entity) { ... }
}
```

### 2. Register in DI

```csharp
builder.Services.AddScoped<IEntityMapper<Product, ProductDto>, ProductMapper>();
```

### 3. Consume in a service

```csharp
public sealed class ProductService(AppDbContext context, IEntityMapper<Product, ProductDto> mapper)
    : MServiceBase<Product, ProductDto>(context, mapper)
{
    // MServiceBase calls mapper.ToDto / ToEntity / ApplyUpdate automatically.
}
```

## Features

- `IEntityMapper<TEntity, TDto>` — three-method mapping contract: `ToDto`, `ToEntity`, and `ApplyUpdate` (patch an existing entity in place for update scenarios).
- `EntityMapperBase<TEntity, TDto>` — template method implementation that calls `MapCoreToDto` / `MapCoreToEntity` for shared fields and `MapSiteSpecificToDto` / `MapSiteSpecificToEntity` (virtual no-ops) for tenant-divergent fields.
- No reflection, no code generation, no configuration — pure hand-written mappings that remain fast and debuggable.
- Designed to satisfy the `IEntityMapper` dependency required by `Muonroi.Services.MServiceBase`.

## Configuration

No DI extension method is provided — this package is contracts only. Register your concrete mapper directly:

```csharp
// Scoped lifetime matches the typical EF Core DbContext lifetime.
services.AddScoped<IEntityMapper<MyEntity, MyDto>, MyMapper>();
```

## API Reference

| Type | Purpose |
|------|---------|
| `IEntityMapper<TEntity, TDto>` | Mapping contract: `ToDto`, `ToEntity`, `ApplyUpdate` |
| `EntityMapperBase<TEntity, TDto>` | Abstract base implementing the template method pattern; requires `TEntity : class, new()` and `TDto : class, new()` |

## Samples

- [Quickstart.Services](../../samples/Quickstart.Services/) — Registers `ProductMapper : EntityMapperBase<Product, ProductDto>` and wires it into `MServiceBase<Product, ProductDto>` for full CRUD over an in-memory EF Core store.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Services`](../Muonroi.Services/) — `MServiceBase<TEntity, TDto>` depends on `IEntityMapper<TEntity, TDto>`; pair these two packages for EF Core CRUD services.
- [`Muonroi.Data.Abstractions`](../Muonroi.Data.Abstractions/) — entity base contracts (`IEntityBase`, `IAuditable`, `ISiteScoped`) that your `TEntity` types typically implement.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
