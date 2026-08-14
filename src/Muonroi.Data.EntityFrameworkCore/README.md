# Muonroi.Data.EntityFrameworkCore

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Data.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore/)

> Entity Framework Core implementations and identity mechanisms for Muonroi.

## Overview
Provides foundational `MDbContext` and `MRepository` classes, alongside identity-centric configuration stores like `AuthenticateRepository` and `PermissionSyncService`.

## Features
- **Context Management**: Employs `MDbContext` and `MDbContextBase` for structured EF operations.
- **Authentication Store**: Integrates authentication data using `AuthenticateRepository`.
- **Permission Sync**: Exposes `PermissionSyncService` to synchronize roles and rights across systems.
- **Interceptors**: Includes `LicenseSaveChangesInterceptor` for enforcing save constraints.

## Installation
```bash
dotnet add package Muonroi.Data.EntityFrameworkCore
```

## Quick Start
```csharp
public class ApplicationDbContext : MDbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options) { }
}
```

## Ecosystem Combinations
- **With Muonroi.Data.EntityFrameworkCore.Events**: Allows event outbox processing to integrate seamlessly with the main `MDbContext`.
- **Full Stack Example**:
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options => 
    options.UseSqlServer(connectionString)
);
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
