# Muonroi.Diagnostics

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Diagnostics.svg)](https://www.nuget.org/packages/Muonroi.Diagnostics/)

> Distributed tracing and execution sessions for the Muonroi ecosystem.

## Overview
Provides tracing capabilities through `MTraceSession` and `MTraceContext` to track execution flow across distributed systems, with in-memory support via `InMemoryTraceSessionStore`.

## Features
- **Execution Tracing**: Use `MTraceSessionScope` to track scoped operations.
- **Context Propagation**: Pass metadata across boundaries using `MTraceContext`.
- **Dependency Injection**: Easy registration via extensions in `MDiagnosticsServiceCollectionExtensions`.

## Installation

```bash
dotnet add package Muonroi.Diagnostics
```

## Quick Start

```csharp
// Register diagnostics
builder.Services.AddMDiagnostics();

// Use tracing
public class MyService(MTraceSession traceSession)
{
    public void DoWork()
    {
        using var scope = new MTraceSessionScope(traceSession, "MyOperation");
        // Work here is traced
    }
}
```

## Ecosystem Combinations

### Muonroi.Diagnostics + Muonroi.Experience.Runtime
Track the extraction of neuron experiences through `ExperienceExtractionPipeline` using `MTraceSessionScope` to monitor pipeline latency and bottlenecks.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
