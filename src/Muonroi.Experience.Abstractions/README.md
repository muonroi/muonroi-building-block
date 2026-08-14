# Muonroi.Experience.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Experience.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Experience.Abstractions/)

> Interfaces and contracts for the Muonroi Experience intelligence engine.

## Overview
Defines core contracts like `IExperienceBrain`, `IExperienceStore`, and foundational data types such as `NeuronExperience` to build adaptive system behaviors.

## Features
- **Core Contracts**: Implement `IExperienceBrain` for custom intelligence engines.
- **Storage Interfaces**: Use `IExperienceStore` to manage persistent experience data.
- **Experience Models**: Standardize learning structures with `NeuronExperience`.

## Installation

```bash
dotnet add package Muonroi.Experience.Abstractions
```

## Quick Start

```csharp
public class CustomBrain : IExperienceBrain
{
    public Task<ExperienceSearchResult> QueryAsync(string context)
    {
        // Custom implementation
        return Task.FromResult(new ExperienceSearchResult());
    }
}
```

## Ecosystem Combinations

### Muonroi.Experience.Abstractions + Muonroi.Experience.Runtime
Implement the `IExperienceExtractor` interface here, then register it to be processed by the `ExperienceStoreOrchestrator` in the runtime package.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
