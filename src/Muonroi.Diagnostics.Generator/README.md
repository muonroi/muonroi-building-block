# Muonroi.Diagnostics.Generator

> Roslyn source generator that weaves line-level trace instrumentation into `[MTraceable]`-annotated methods at compile time — zero reflection, zero runtime overhead for un-annotated code.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Diagnostics.Generator.svg)](https://www.nuget.org/packages/Muonroi.Diagnostics.Generator/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is a Roslyn `IIncrementalGenerator` (`[Generator]`). When added to a project it scans every method decorated with `[MTraceable]` (from `Muonroi.Core.Abstractions`) and emits a `partial class` companion that wraps the method body with trace-capture calls against the active `ITraceSession`. It also ships `TraceableSyntaxRewriter`, which rewrites local-variable assignments and declarations to call `RecordLineTrace` in-line (up to 50 captures per method). There is **no runtime API** in this package; all observable behavior is in the generated `*_Traces.g.cs` files and the `Muonroi.Diagnostics` runtime package.

## Installation

```bash
dotnet add package Muonroi.Diagnostics.Generator --prerelease
```

The generator is activated automatically by the SDK. No `Initialize` call or DI registration is required — the package sets `IncludeBuildOutput=false` so only the Roslyn analyzer assembly is deployed.

Pair it with the runtime package that hosts `ITraceSession`:

```bash
dotnet add package Muonroi.Diagnostics --prerelease
```

## Quick Start

1. Mark the containing class `partial` and annotate the method with `[MTraceable]`:

```csharp
using Muonroi.Core.Abstractions.Diagnostics;

public partial class OrderService
{
    [MTraceable]
    public void PlaceOrder(string orderId, decimal amount)
    {
        var validated = amount > 0;   // captured → RecordLineTrace(line, "validated", value)
        var result = ProcessCore(orderId, amount);
    }
}
```

2. At build time the generator emits `OrderService_Traces.g.cs` into your project's `obj/` folder:

```csharp
// OrderService_Traces.g.cs  (auto-generated — do not edit)
using System;
using Muonroi.Core.Abstractions.Diagnostics;

namespace YourNamespace;

partial class OrderService
{
    public void PlaceOrder_TraceWrapper()
    {
        using var scope = Muonroi.Core.Abstractions.Context.MTraceContextHolder
            .Current.Value?.BeginNode("PlaceOrder", MTraceNodeType.Custom);
        PlaceOrder();
    }
}
```

3. Call `PlaceOrder_TraceWrapper()` (or invoke `PlaceOrder` directly when tracing is not needed) — the wrapper delegates to the original method while opening a `MTraceNodeType.Custom` scope on the active `ITraceSession`.

## Features

- **Incremental Roslyn generator** — implements `IIncrementalGenerator` for fast, incremental builds; only re-runs when attributed methods change.
- **Wrapper method emission** — for each `[MTraceable]` method, generates a `<MethodName>_TraceWrapper()` overload that opens a `MTraceNodeType.Custom` node on `MTraceContextHolder.Current.Value`.
- **Line-level variable capture** — `TraceableSyntaxRewriter` rewrites local declarations and assignment expressions to call `RecordLineTrace(line, variableName, value)`, up to 50 captures per method (hard-capped via `MaxCaptures`).
- **Sensitive-data exclusion** — honors `[MTraceSensitive]` (from `Muonroi.Core.Abstractions.Diagnostics`) on properties, parameters, and classes; annotated symbols are not recorded.
- **Zero build-output footprint** — `IncludeBuildOutput=false`; the generator DLL ships only as a Roslyn component and is never copied to output directories.
- **`netstandard2.0` target** — compatible with all modern SDK versions and the Roslyn host version constraints for analyzer packages.

## API Reference

This package contains no runtime API. The types below are in the generated code and the companion `Muonroi.Core.Abstractions` / `Muonroi.Diagnostics` packages.

| Type | Package | Purpose |
|------|---------|---------|
| `TraceableGenerator` | this package | `IIncrementalGenerator` that emits `*_Traces.g.cs` for each `[MTraceable]` method |
| `TraceableSyntaxRewriter` | this package | `CSharpSyntaxRewriter` that rewrites variable assignments/declarations to capture values via `RecordLineTrace` |
| `MTraceableAttribute` | `Muonroi.Core.Abstractions` | Marks a method for trace wrapper generation; `Name` property overrides the node label |
| `MTraceSensitiveAttribute` | `Muonroi.Core.Abstractions` | Excludes a property, parameter, or class from trace capture |
| `MTraceNodeType` | `Muonroi.Core.Abstractions` | Enum used in generated `BeginNode` calls (`Custom`, `Rule`, `Handler`, …) |
| `ITraceSession` | `Muonroi.Core.Abstractions` | Session consumed by generated code; implemented by `Muonroi.Diagnostics` |

## Samples

No dedicated sample exists for this package. Usage is demonstrated through any project that references both `Muonroi.Diagnostics.Generator` and `Muonroi.Diagnostics` — see the runtime package for a complete end-to-end example.

## Compatibility

- Target framework: `netstandard2.0` (Roslyn component — runs inside the build host, not the application)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Diagnostics`](../Muonroi.Diagnostics/) — runtime implementation of `ITraceSession`; required for the generated trace calls to execute
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `MTraceableAttribute`, `MTraceSensitiveAttribute`, `ITraceSession`, and `MTraceContextHolder`

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
