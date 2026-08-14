# Muonroi.Core

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Core.svg)](https://www.nuget.org/packages/Muonroi.Core/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Core.svg)](https://www.nuget.org/packages/Muonroi.Core/)

> Central extensions and services for the Muonroi application ecosystem.

## Overview
Delivers essential implementation classes and extensions including `CoreServiceCollectionExtensions`, `MDateTimeService`, `MSequentialGuidGenerator`, and various formatters.

## Features
- **DI Registration**: Provides `CoreServiceCollectionExtensions` to wire up required primitives.
- **Time Management**: Implements `MDateTimeService`, `LocalClockProvider`, and `UtcClockProvider` for standardized time.
- **Extensions**: Rich extension methods like `JsonExtensions`, `MCryptographyExtension`, and `MStringExtension`.
- **Identifiers**: Includes `MSequentialGuidGenerator` for database-friendly sequential GUID generation.

## Installation
```bash
dotnet add package Muonroi.Core
```

## Quick Start
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCoreServices();

var app = builder.Build();
var timeService = app.Services.GetRequiredService<IMDateTimeService>();
var now = timeService.Now;
```

## Ecosystem Combinations
- **With Muonroi.Core.Abstractions**: Resolves core system interfaces like `IMDateTimeService` and `IGuidGenerator`.
- **Full Stack Example**:
```csharp
builder.Services.AddCoreServices()
                .AddEcosystemRegistry();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
