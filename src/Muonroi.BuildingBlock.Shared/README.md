# Muonroi.BuildingBlock.Shared

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.BuildingBlock.Shared.svg)](https://www.nuget.org/packages/Muonroi.BuildingBlock.Shared/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.BuildingBlock.Shared.svg)](https://www.nuget.org/packages/Muonroi.BuildingBlock.Shared/)

> Core shared primitives and project settings for the Muonroi ecosystem.

## Overview
Serves as the foundational project for the Muonroi ecosystem, ensuring global consistency across all building block packages.

## Features
- **Global Settings**: Core foundational project setups.
- **Dependency Management**: Centralized dependency markers for ecosystem building blocks.

## Installation
```bash
dotnet add package Muonroi.BuildingBlock.Shared
```

## Quick Start
```xml
<!-- Reference in your csproj -->
<PackageReference Include="Muonroi.BuildingBlock.Shared" Version="1.0.0" />
```

## Ecosystem Combinations
- **With Muonroi.Core**: Provides baseline references for core system initialization.
- **Full Stack Example**:
```csharp
// Standard ecosystem dependency resolution
builder.Services.AddCoreServices();
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
