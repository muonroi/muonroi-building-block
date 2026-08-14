# Muonroi.Services
> Generic EF Core service base with virtual lifecycle hooks for schema-divergent multi-tenancy architectures.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Services.svg)](https://www.nuget.org/packages/Muonroi.Services/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.Services` provides a standardized application service layer implementation designed specifically for schema-divergent multi-tenancy. At its core is the `MServiceBase<TEntity, TDto>` abstract class, which handles standard Entity Framework Core CRUD operations while exposing a rich set of lifecycle hooks.

Unlike traditional repository patterns, this package couples intentionally to EF Core `DbContext`. It provides the "Core" domain logic but delegates specific validation, enrichment, and side-effects to tenant-specific or site-specific overrides. This makes it an ideal fit for SaaS applications where different tenants require slightly different validation rules or default values for the same underlying domain entity.

## Features

- **Standardized CRUD**: Built-in `GetByIdAsync`, `GetByConditionAsync`, `CreateAsync`, `UpdateAsync`, and `DeleteAsync` reducing boilerplate.
- **DTO Mapping Integration**: Seamlessly relies on `IEntityMapper<TEntity, TDto>` to transform inputs and outputs automatically.
- **Extensive Lifecycle Hooks**: Provides `virtual` methods for complete interception of the mutation pipeline:
  - `ValidateAsync(TEntity)`
  - `ApplyDefaultValues(TEntity)`
  - `BeforeCreate(TEntity)` / `AfterCreate(TEntity)`
  - `BeforeUpdate(TEntity)` / `AfterUpdate(TEntity)`
- **Multi-tenant Ready**: By overriding these hooks in tenant-specific subclasses (Site Profiles), you can implement divergent business rules without polluting the core service class with conditional logic.

## Installation

```bash
dotnet add package Muonroi.Services
```

## Quick Start

### 1. Create a Base Service

Inherit from `MServiceBase` to implement your generic domain service. 

```csharp
using Muonroi.Services;
using Microsoft.EntityFrameworkCore;

public class ProductService : MServiceBase<Product, ProductDto>
{
    public ProductService(AppDbContext context, IEntityMapper<Product, ProductDto> mapper) 
        : base(context, mapper)
    {
    }

    // Core shared logic can be added here
}
```

### 2. Override for Specific Tenant/Site Logic

When running in a multi-tenant environment, you might have a specific tenant that needs special validation.

```csharp
public class EnterpriseTenantProductService : ProductService
{
    public EnterpriseTenantProductService(AppDbContext context, IEntityMapper<Product, ProductDto> mapper) 
        : base(context, mapper)
    {
    }

    protected override Task ValidateAsync(Product entity, CancellationToken ct)
    {
        if (entity.Price < 100)
        {
            throw new ValidationException("Enterprise products must have a minimum price of 100.");
        }
        return Task.CompletedTask;
    }

    protected override void ApplyDefaultValues(Product entity)
    {
        // Enforce specific defaults for this tenant
        entity.IsPremium = true;
    }

    protected override async Task AfterCreate(Product entity, CancellationToken ct)
    {
        // E.g., Publish a domain event specifically needed by this tenant
        await _eventBus.PublishAsync(new EnterpriseProductCreatedEvent(entity.Id), ct);
    }
}
```

## API Reference

### `MServiceBase<TEntity, TDto>`

**Properties:**
- `Context`: The underlying `DbContext`
- `Mapper`: The `IEntityMapper<TEntity, TDto>` provided via DI.

**Core Methods:**
- `GetByIdAsync<TKey>(TKey id, ct)`: Fetches and maps a single entity.
- `GetByConditionAsync(Expression<Func<TEntity, bool>> predicate, ct)`: Fetches multiple entities matching a predicate.
- `CreateAsync(TDto dto, ct)`: Maps to entity, triggers create hooks, saves, and returns the DTO.
- `UpdateAsync(TEntity entity, TDto dto, ct)`: Applies DTO to entity, triggers update hooks, saves, and returns the DTO.
- `DeleteAsync(TEntity entity, ct)`: Removes the entity from the context and saves.

**Hooks (Protected Virtual):**
- `ValidateAsync`: Intercept before creation or update. Throw to abort.
- `ApplyDefaultValues`: Sync method to inject default properties before `BeforeCreate`.
- `BeforeCreate` / `AfterCreate`: Pre and post insert hooks.
- `BeforeUpdate` / `AfterUpdate`: Pre and post update hooks.

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Data.EntityFrameworkCore -> MServiceBase<T> wraps MDbContext with CRUD lifecycle hooks
Provides an integrated generic repository and service layer pattern directly over EF Core.

### + Tenancy.SiteProfile -> MSiteRepository resolves the correct DbContext per site automatically
Dynamically resolves which database schema or connection string to use based on the current site/tenant profile.

### + Mediator -> Service methods become IRequestHandler implementations: clean CQRS separation
Wrap MServiceBase method calls inside MediatR handlers to separate command/query execution logic.

### + Observability -> Each service method call traced as OTel span
Trace CRUD operations deeply to measure database performance and execution times.

### + RuleEngine -> Services can run rule orchestration before/after data mutations
Execute IMRuleOrchestrator inside the BeforeCreate or BeforeUpdate hooks to enforce complex business rules automatically.

### Full Stack
`csharp
// combined registration
builder.Services.AddSiteProfileDbContext<AppDbContext>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddMuonroiObservability();
builder.Services.AddRuleEngine();
`

## Samples
- samples/MultiTenantSaaS/
- samples/CQRSArchitecture/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
