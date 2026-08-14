# Muonroi.Mapping.Abstractions
> Entity-DTO mapping contracts with template method pattern for schema-divergent multi-tenancy.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Mapping.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Mapping.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Mapping.Abstractions` package provides foundational interfaces and abstract base classes for mapping between domain entities and Data Transfer Objects (DTOs). Its primary focus is addressing the complexity of mapping in multi-tenant environments where core entity schemas remain static, but specific tenants (sites) require extended, schema-divergent fields.

Rather than relying purely on reflection or complex configuration profiles, this package encourages an explicit, code-first mapping strategy using the **Template Method Pattern**. By separating core mappings from site-specific mappings, developers can construct extensible mappers that adapt to varying data shapes across deployments without polluting the core mapping logic.

Use this package when you are building a product that allows tenant-specific data model extensions, and you need a standardized, interface-driven way to handle DTO transformations across the system.

## Features

- **Standardized Interfaces**: `IEntityMapper<TEntity, TDto>` establishes a uniform contract for mapping entities to DTOs, DTOs to entities, and applying updates to existing entities.
- **Template Method Implementation**: `EntityMapperBase<TEntity, TDto>` orchestrates the mapping process, defining a strict lifecycle that guarantees both core and extended fields are processed.
- **Extensibility Hooks**: Virtual methods allow derived classes to map site-specific, custom fields only when necessary, leaving the core mapping logic untouched.
- **Schema-Divergent Support**: Perfect for environments where a baseline "Core" system is deployed, but individual clients have distinct columns or JSON extension bags added to their tables.

## Installation

```bash
dotnet add package Muonroi.Mapping.Abstractions
```

## Quick Start

### Implementing a Basic Mapper

Inherit from `EntityMapperBase` and implement the abstract core mapping methods.

```csharp
using Muonroi.Mapping.Abstractions;

public class UserMapper : EntityMapperBase<UserEntity, UserDto>
{
    protected override void MapCoreToDto(UserEntity entity, UserDto dto)
    {
        dto.Id = entity.Id;
        dto.Username = entity.Username;
        dto.Email = entity.Email;
    }

    protected override void MapCoreToEntity(UserDto dto, UserEntity entity)
    {
        entity.Username = dto.Username;
        entity.Email = dto.Email;
        // Notice we typically don't map IDs back to entities on updates
    }
}
```

### Implementing a Schema-Divergent Mapper

If a specific tenant deployment requires mapping custom fields (e.g., a custom loyalty tier), inherit from your base mapper or override the virtual methods.

```csharp
public class TenantAUserMapper : UserMapper
{
    // Core mappings are handled by the base class.
    
    protected override void MapSiteSpecificToDto(UserEntity entity, UserDto dto)
    {
        // Assuming Tenant A added a 'LoyaltyTier' column to their schema
        if (entity.CustomFields.TryGetValue("LoyaltyTier", out var tier))
        {
            dto.ExtendedProperties["LoyaltyTier"] = tier;
        }
    }

    protected override void MapSiteSpecificToEntity(UserDto dto, UserEntity entity)
    {
        if (dto.ExtendedProperties.TryGetValue("LoyaltyTier", out var tier))
        {
            entity.CustomFields["LoyaltyTier"] = tier;
        }
    }
}
```

### Usage

```csharp
IEntityMapper<UserEntity, UserDto> mapper = new TenantAUserMapper();

// Creating a DTO (Runs Core -> SiteSpecific)
UserDto dto = mapper.ToDto(myUserEntity);

// Applying updates to an existing entity (Runs Core -> SiteSpecific)
mapper.ApplyUpdate(myUserEntity, updateDto);
```

## API Reference

### `IEntityMapper<TEntity, TDto>`
The core interface that must be resolved from the DI container.
- `TDto ToDto(TEntity entity)`: Creates and populates a new DTO from the entity.
- `TEntity ToEntity(TDto dto)`: Creates and populates a new Entity from the DTO.
- `void ApplyUpdate(TEntity entity, TDto dto)`: Updates an existing entity instance with values from the DTO.

### `EntityMapperBase<TEntity, TDto>`
The abstract base class implementing the template method pattern.
- `abstract void MapCoreToDto(...)`: Maps the baseline schema from Entity to DTO.
- `abstract void MapCoreToEntity(...)`: Maps the baseline schema from DTO to Entity.
- `virtual void MapSiteSpecificToDto(...)`: Maps tenant-specific fields from Entity to DTO (default: no-op).
- `virtual void MapSiteSpecificToEntity(...)`: Maps tenant-specific fields from DTO to Entity (default: no-op).

## Integration

`Muonroi.Mapping.Abstractions` connects directly to:
- **Muonroi.Data.Abstractions**: Can be used alongside repository abstractions to automatically map results before returning them from read-side repositories.

## Ecosystem Combinations

> Great standalone. Becomes **significantly more powerful** when combined.

### + Mapper → Core Mapping Engine
EntityMapperBase template uses Mapper for the actual mapping work.

### + Tenancy.SiteProfile → Per-Site Overrides
Per-site overrides for specific tenants:
```csharp
class TenantAMapper : EntityMapperBase<Order, OrderDto>
```

### Full Mapping Stack
```csharp
builder.Services
    .AddMappingAbstractions(config)
    .AddSiteProfiles(config);
```

## Samples

See the working example in [Quickstart.Mapping.Abstractions](../../samples/Quickstart.Mapping.Abstractions).

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
