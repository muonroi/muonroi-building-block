# Muonroi.Experience.Runtime

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Experience.Runtime.svg)](https://www.nuget.org/packages/Muonroi.Experience.Runtime/)

> Runtime implementations for the Muonroi Experience intelligence engine.

## Overview
Provides concrete implementations like `ClaudeExperienceBrain`, `QdrantExperienceStore`, and the `ExperienceStoreOrchestrator` to execute AI-driven adaptive logic and vector search operations.

## Features
- **Vector Storage**: Use `QdrantExperienceStore` for high-performance semantic search.
- **AI Brains**: Leverage `ClaudeExperienceBrain` or `OllamaExperienceBrain` for intelligence.
- **Orchestration**: Manage experience pipelines with `ExperienceStoreOrchestrator` and `EvolutionBackgroundService`.

## Installation

```bash
dotnet add package Muonroi.Experience.Runtime
```

## Quick Start

```csharp
builder.Services.AddExperienceRuntime(options =>
{
    options.UseQdrantStore(new QdrantClientWrapper(config));
    options.UseClaudeBrain(new ExperienceBrainOptions { ApiKey = "..." });
});

// Usage
public class MyAgent(ExperienceStoreOrchestrator orchestrator)
{
    public async Task Process() => await orchestrator.EvolveAsync();
}
```

## Ecosystem Combinations

### Muonroi.Experience.Runtime + Muonroi.Diagnostics
Monitor the `ClusteringEngine` and `MistakeDetector` execution latency by injecting `MTraceSession` from the diagnostics ecosystem.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
