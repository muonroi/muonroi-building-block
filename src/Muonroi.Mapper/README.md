# Muonroi.Mapper
> Lightweight, reflection-based object mapper optimized for structural parity.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Mapper.svg)](https://www.nuget.org/packages/Muonroi.Mapper/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Mapper` package is an ultra-lightweight, expression-tree-based object-to-object mapping tool. While comprehensive mappers like AutoMapper are feature-rich, they often introduce complex configuration and startup overhead that is unnecessary for many basic mapping scenarios (such as moving data between a DTO and an equivalent Entity).

This mapper avoids the need for heavy pre-registration of profiles by using expression trees compiled dynamically the first time two types are mapped. It automatically pairs properties based on matching names and types. The compiled expressions are then cached in a thread-safe dictionary, resulting in near-native mapping speeds for subsequent calls.

Use this package when you need simple, reliable mapping between structurally similar objects without the bloat of external dependencies or the complexity of explicit configuration profiles.

## Features

- **Expression Tree Compilation**: Generates native CLR instructions for mapping rather than relying on slow reflection at runtime.
- **Convention-Based Mapping**: Maps properties purely by name and type assignability—no configuration needed.
- **Thread-Safe Action Caching**: Employs a `ConcurrentDictionary` to cache mapping actions natively, guaranteeing performance under high concurrency.
- **Zero-Friction Startup**: As mappings are generated lazily and dynamically on first-use, there is zero impact on the application's startup phase.
- **Dependency Injection Ready**: Exposes simple extensions for `IServiceCollection` to wire up the `IMapper` singleton immediately.

## Installation

```bash
dotnet add package Muonroi.Mapper
```

## Quick Start

### Basic Configuration

Register the simple mapper into the DI container during startup:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Mapper.Mapper;

var builder = WebApplication.CreateBuilder(args);

// Register the simple mapper and its internal configuration caching
builder.Services.AddSimpleMapper();
```

### Mapping Objects

Inject the `IMapper` interface into your services, controllers, or handlers to map instances:

```csharp
using Muonroi.Mapper.Interfaces; // Or Muonroi.Mapper.Mapper based on usage
using System.Threading.Tasks;

public class UserService
{
    private readonly IMapper _mapper;
    private readonly IUserRepository _repository;

    public UserService(IMapper mapper, IUserRepository repository)
    {
        _mapper = mapper;
        _repository = repository;
    }

    public async Task<UserDto> GetUserAsync(Guid id)
    {
        UserEntity entity = await _repository.GetByIdAsync(id);
        
        // 1. Map to a new instance (infers generic type)
        return _mapper.Map<UserDto>(entity);
    }
    
    public async Task UpdateUserAsync(Guid id, UserUpdateDto updateDto)
    {
        UserEntity entity = await _repository.GetByIdAsync(id);
        
        // 2. Map onto an existing instance
        _mapper.Map(updateDto, entity);
        
        await _repository.UpdateAsync(entity);
    }
}
```

## API Reference

### Core Types

- `IMapper`: The core interface declaring overloads for creating mapped instances or mapping onto existing ones.
- `SimpleMapper`: The concrete implementation of `IMapper` responsible for delegating mapping to cached delegates.
- `MappingConfiguration`: The internal expression tree compiler. It generates `Action<object, object>` lambdas dynamically by evaluating property matchings, checking `CanRead`, `CanWrite`, and `IsAssignableFrom`.
- `MapperServiceCollectionExtensions`: Contains the `AddSimpleMapper` method to register the mapping components in the `IServiceCollection` as singletons.

## Integration

`Muonroi.Mapper` integrates with:
- **Muonroi.Core.Abstractions**: Uses `MGuard` for standard null safety guarding across mappings.

## Ecosystem Combinations

> Great standalone. Becomes **significantly more powerful** when combined.

### + Data.EntityFrameworkCore → Entity Isolation
Map entities to DTOs without exposing EF entities to API layer.

### + Mediator → Pipeline Mapping
Query handlers return mapped DTOs automatically.

### + Tenancy → Tenant-Specific Mapping
Mappers can be per-site via SiteProfile: different tenants get different mapping strategies.

### Full Mapping Stack
```csharp
builder.Services
    .AddSimpleMapper(config)
    .AddSiteProfiles(config);
```

## Samples

See the working example in [Quickstart.Mapper](../../samples/Quickstart.Mapper).

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
