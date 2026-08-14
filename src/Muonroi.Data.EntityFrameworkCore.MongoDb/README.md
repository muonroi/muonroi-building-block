# Muonroi.Data.EntityFrameworkCore.MongoDb

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Data.EntityFrameworkCore.MongoDb.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.MongoDb/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Data.EntityFrameworkCore.MongoDb.svg)](https://www.nuget.org/packages/Muonroi.Data.EntityFrameworkCore.MongoDb/)

> MongoDB implementations using the EF Core provider for Muonroi.

## Overview
Connects Muonroi data models to MongoDB via EF Core, utilizing `MMongoDbEntity` and `MongoDbContextConfigurator`.

## Features
- **Mongo Models**: Implements `MMongoDbEntity` for standard NoSQL entity mapping.
- **Context Config**: Employs `MongoDbContextConfigurator` to correctly structure the context for Document storage.

## Installation
```bash
dotnet add package Muonroi.Data.EntityFrameworkCore.MongoDb
```

## Quick Start
```csharp
public class MyMongoEntity : MMongoDbEntity
{
    public string Data { get; set; }
}
```

## Ecosystem Combinations
- **With Muonroi.Data.Abstractions**: Translates generic `IMRepositoryBase` calls to the MongoDB EF Core driver.
- **Full Stack Example**:
```csharp
// Setup Mongo context configurator
var configurator = new MongoDbContextConfigurator();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
