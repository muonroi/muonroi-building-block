# Muonroi.CodeStandards

[![NuGet Status](https://img.shields.io/nuget/v/Muonroi.CodeStandards.svg)](https://www.nuget.org/packages/Muonroi.CodeStandards/)
[![NuGet Download](https://img.shields.io/nuget/dt/Muonroi.CodeStandards.svg)](https://www.nuget.org/packages/Muonroi.CodeStandards/)

> Roslyn analyzers ensuring strict adherence to the Muonroi coding guidelines.

## Overview
Provides a suite of Roslyn-based analyzers and code fixes, such as `Mstd0001_ForbiddenThrowAnalyzer`, `Mstd0002_NullForgivingAnalyzer`, and `Mstd0003_LoggingViaMLogAnalyzer`.

## Features
- **Strict Exception Handling**: `Mstd0001_ForbiddenThrowAnalyzer` ensures proper exception types are thrown.
- **Null Safety**: `Mstd0002_NullForgivingAnalyzer` and `Mstd0002_NullForgivingCodeFix` limit the use of the null-forgiving operator (`!`).
- **Logging Standards**: `Mstd0003_LoggingViaMLogAnalyzer` enforces logging through the standard MLog structure.
- **Guard Rails**: `Mstd0004_DirectMGuardBypassAnalyzer` ensures `MGuard` is used appropriately.

## Installation
```bash
dotnet add package Muonroi.CodeStandards
```

## Quick Start
```xml
<!-- Analyzers run automatically upon installation -->
<PackageReference Include="Muonroi.CodeStandards" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

## Ecosystem Combinations
- **With Muonroi.Core**: Enforces the usage of `MGuard` instead of standard exceptions when developing core services.
- **Full Stack Example**:
```csharp
// The analyzer will flag this if standard throw is used instead of MGuard
// throw new Exception("Error"); -> MGuard.NotNull(value, nameof(value));
```

## Samples
Check out the [Samples](../../samples/) directory for full examples.

## License
MIT
