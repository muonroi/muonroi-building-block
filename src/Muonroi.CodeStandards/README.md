# Muonroi.CodeStandards

> Roslyn analyzers that enforce the three mandatory coding rules across every `Muonroi.*` namespace — forbidden raw throws, forbidden null-forgiving operators, and logging-only-via-IMLog — surfaced as build errors before code ships.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.CodeStandards.svg)](https://www.nuget.org/packages/Muonroi.CodeStandards/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is a **Roslyn analyzer** (not a runtime library). It ships no application code of its own — only diagnostic analyzers that the C# compiler runs at build time. Adding it to a project causes three rules (`MSTD0001`, `MSTD0002`, `MSTD0003`) to become hard **errors** whenever the code under analysis lives in a `Muonroi.*` namespace. Rules are scoped to non-test assemblies (any assembly whose name contains `.Tests` is exempt).

## Installation

```bash
dotnet add package Muonroi.CodeStandards --prerelease
```

Because this is an analyzer-only package, `<IncludeBuildOutput>false</IncludeBuildOutput>` is set in the csproj. The package does not add any runtime assemblies to your output directory.

## Quick Start

Add the package reference to any Muonroi library or service project:

```xml
<ItemGroup>
  <PackageReference Include="Muonroi.CodeStandards" Version="1.0.0-alpha.15" />
</ItemGroup>
```

The analyzers activate automatically. Code inside `Muonroi.*` namespaces will produce build errors when any of the three rules is violated.

**MSTD0001 — wrong throw, immediately caught at compile time:**

```csharp
// ERROR MSTD0001: Throw via MGuard or an MException-derived type;
// raw 'ArgumentNullException' is forbidden in namespace 'Muonroi.Orders'
namespace Muonroi.Orders;
public class OrderService
{
    public void Process(Order? order)
    {
        throw new ArgumentNullException(nameof(order)); // <-- MSTD0001
    }
}
```

Fix — use `MGuard` or throw an `MException`-derived type:

```csharp
MGuard.NotNull(order, nameof(order)); // guard, or throw new OrderNotFoundException(...)
```

**MSTD0002 — null-forgiving operator replaced automatically by IDE:**

```csharp
// ERROR MSTD0002: Null-forgiving operator '!' is forbidden in namespace 'Muonroi.Catalog'
var id = product!.Id; // <-- MSTD0002
```

The IDE code-fix rewrites `product!.Id` to `MGuard.NotNull(product).Id` and inserts `using Muonroi.Core.Abstractions.Guards;` when missing.

**MSTD0003 — raw logging sinks are banned:**

```csharp
// ERROR MSTD0003: Logging must go through IMLog; 'Console.WriteLine' is forbidden
// in namespace 'Muonroi.Payments'
Console.WriteLine("payment processed"); // <-- MSTD0003

// Also forbidden: Debug.*, Trace.*, Serilog.Log.*, ILogger.Log* on a non-IMLog receiver
```

Fix — inject `IMLog<T>` from `Muonroi.Logging.Abstractions`:

```csharp
public class PaymentService(IMLog<PaymentService> log)
{
    public void Process() => log.LogInformation("payment processed");
}
```

## Features

- **MSTD0001** (`DiagnosticSeverity.Error`, enabled by default): forbids `throw new X(...)` where `X` does not derive from `MException`; `MGuard.*` guard calls and `MException`-derived throws are allowed.
- **MSTD0002** (`DiagnosticSeverity.Error`, enabled by default): forbids the null-forgiving postfix operator `!` on real expressions. Placeholder forms `null!`, `default!`, and `default(T)!` are explicitly exempt.
- **MSTD0003** (`DiagnosticSeverity.Error`, enabled by default): forbids `Console.Write`/`Console.WriteLine`, `Console.Error`/`Console.Out` writes, `System.Diagnostics.Debug.*`, `System.Diagnostics.Trace.*`, static `Serilog.Log.*`, and raw `ILogger`/`ILogger<T>` `Log*` calls on a non-`IMLog` receiver.
- **Test-assembly exemption**: any compilation whose assembly name contains `.Tests` is skipped by all three analyzers.
- **Logging-infrastructure exemption**: namespaces starting with `Muonroi.Logging` are exempt from MSTD0003 (the logging wrapper itself must use `ILogger`).
- **MSTD0002 IDE code-fix**: the bundled `CodeFixProvider` rewrites `expr!` to `MGuard.NotNull(expr)` with batch-fix support.
- **Scope**: rules fire only inside `Muonroi.*` or `Muonroi` namespaces; third-party or application code in other namespaces is unaffected.

## Suppressing a diagnostic

Suppress only when genuinely required (e.g., pre-DI bootstrap code where `IMLog` is unavailable):

```csharp
#pragma warning disable MSTD0003 // IMLog not available during bootstrap
Console.WriteLine("host starting");
#pragma warning restore MSTD0003
```

Or use `[SuppressMessage]`:

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Muonroi.CodeStandards", "MSTD0002",
    Justification = "External override signature requires null!")]
public override void Hook(object param = null!) { }
```

## API Reference

| Type | Purpose |
|------|---------|
| `Mstd0001_ForbiddenThrowAnalyzer` | `DiagnosticAnalyzer` — fires MSTD0001 on `ThrowStatement`/`ThrowExpression` nodes where the thrown type does not derive from `MException` |
| `Mstd0002_NullForgivingAnalyzer` | `DiagnosticAnalyzer` — fires MSTD0002 on `SuppressNullableWarningExpression` nodes (excludes `null!`, `default!`, `default(T)!`) |
| `Mstd0003_LoggingViaMLogAnalyzer` | `DiagnosticAnalyzer` — fires MSTD0003 on `InvocationExpression` nodes that call forbidden logging sinks |
| `Mstd0002_NullForgivingCodeFix` | `CodeFixProvider` — replaces `expr!` with `MGuard.NotNull(expr)`; adds `using Muonroi.Core.Abstractions.Guards;` when missing; supports `WellKnownFixAllProviders.BatchFixer` |
| `MstdDiagnosticDescriptors` | Internal static holder for the three `DiagnosticDescriptor` instances (`MSTD0001`, `MSTD0002`, `MSTD0003`) |

## Samples

No dedicated sample project exists for this package. The analyzers take effect in any project that references `Muonroi.CodeStandards` — see any of the quickstart samples in the repo for projects that carry the reference transitively.

## Compatibility

- Target framework: `netstandard2.0`
- Roslyn component: yes (`<IsRoslynComponent>true</IsRoslynComponent>`)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `MGuard` and `MException`, the types MSTD0001 and MSTD0002 steer you toward
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — provides `IMLog`/`IMLog<T>`, the only permitted logging interface under MSTD0003

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
