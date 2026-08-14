# Muonroi.Core.Abstractions

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.Core.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Core.Abstractions/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.Core.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Core.Abstractions/)

> Definitive interface and exception contracts for the Muonroi ecosystem.

## Overview
Declares foundational interfaces like `IContextResolver`, `ISystemExecutionContext`, standard exception types (`MException`, `MNotFoundException`), and guard classes like `MGuard`.

## Features
- **Execution Context**: Defines `ISystemExecutionContext` and `IContextResolver` for tracking user/tenant flow.
- **Exception Hierarchy**: Base custom exception standard with classes like `MException` and `MNotFoundException`.
- **Defensive Programming**: Includes the `MGuard` utility to quickly validate state and arguments.
- **Ecosystem Registration**: Exposes `MEcosystemRegistry` for system-wide capability mapping.

## Installation
```bash
dotnet add package Muonroi.Core.Abstractions
```

## Quick Start
```csharp
public class MyService 
{
    public void ProcessData(string data)
    {
        MGuard.NotNullOrEmpty(data, nameof(data));
        // Process
    }
}
```

## Ecosystem Combinations
- **With Muonroi.Core**: The concrete service extensions fulfill the ecosystem interfaces specified here.
- **Full Stack Example**:
```csharp
builder.Services.AddEcosystemRegistry(); // Defined in MEcosystemRegistry extensions
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
