# Muonroi.EntityFrameworkCore.Configuration

[![NuGet](https://img.shields.io/nuget/v/Muonroi.EntityFrameworkCore.Configuration.svg)](https://www.nuget.org/packages/Muonroi.EntityFrameworkCore.Configuration/)

> Base entity configurations and attributes for Muonroi data models.

## Overview
Provides foundational abstractions like `MEntityConfigurationBase` and attributes such as `SiteColumnAttribute` to standardize Entity Framework Core model creation and schema definitions.

## Features
- **Standard Configurations**: Inherit from `MEntityConfigurationBase` for consistent entity maps.
- **Schema Attributes**: Decorate properties with `SiteColumnAttribute` for domain-specific schema definitions.

## Installation

```bash
dotnet add package Muonroi.EntityFrameworkCore.Configuration
```

## Quick Start

```csharp
public class UserConfiguration : MEntityConfigurationBase<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder); // Applies standard conventions
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}

public class User
{
    [SiteColumn("TenantId")]
    public string SiteId { get; set; }
}
```

## Ecosystem Combinations

### Muonroi.EntityFrameworkCore.Configuration + Muonroi.Governance
Use `SiteColumnAttribute` to map tenant identifiers that are evaluated by the `MPolicyDecisionService` during multi-tenant data access.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
