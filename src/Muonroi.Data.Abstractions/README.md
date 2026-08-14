# Muonroi.Data.Abstractions

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Data.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Data.Abstractions/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Data.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Data.Abstractions/)

> Data-layer contracts and repository patterns for the Muonroi ecosystem.

## Overview
Houses common data access patterns, including `IMRepositoryBase`, `IUnitOfWork`, and entity markers like `IAuditable` and `IEntityBase`.

## Features
- **Repository Pattern**: Exposes `IMRepositoryBase` for generalized data fetching.
- **UoW Management**: Defines `IUnitOfWork` and `MultiDbUnitOfWork` for transaction control.
- **Entity Interfaces**: Includes `IAuditable`, `IEntityBase`, and `ISiteScoped` to standardize domain entities.

## Installation
```bash
dotnet add package Muonroi.Data.Abstractions
```

## Quick Start
```csharp
public class MyEntity : IEntityBase, IAuditable
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Ecosystem Combinations
- **With Muonroi.Data.EntityFrameworkCore**: EF Core implementations map directly to `IMRepositoryBase`.
- **Full Stack Example**:
```csharp
public class MyRepository : IMRepositoryBase<MyEntity> 
{
    // Implementation
}
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
