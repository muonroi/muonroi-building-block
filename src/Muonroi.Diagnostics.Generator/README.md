# Muonroi.Diagnostics.Generator

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Diagnostics.Generator.svg)](https://www.nuget.org/packages/Muonroi.Diagnostics.Generator/)

> Roslyn source generator for automatic trace instrumentation.

## Overview
Contains `TraceableGenerator` and `TraceableSyntaxRewriter` to automatically emit diagnostic tracing code for methods marked for instrumentation during compilation.

## Features
- **Source Generation**: `TraceableGenerator` automatically instruments marked classes.
- **Syntax Rewriting**: Uses `TraceableSyntaxRewriter` to weave `MTraceSessionScope` calls without boilerplate.

## Installation

```bash
dotnet add package Muonroi.Diagnostics.Generator
```

## Quick Start

```csharp
// Just add the package. The source generator automatically instruments
// classes decorated with appropriate tracing attributes.

[Traceable]
public class WorkerClass
{
    public void Process() { } // MTraceSessionScope injected at compile time
}
```

## Ecosystem Combinations

### Muonroi.Diagnostics.Generator + Muonroi.Diagnostics
The generator emits code that relies on `MTraceSession` from the `Muonroi.Diagnostics` package, removing the need to write boilerplate trace scopes.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
