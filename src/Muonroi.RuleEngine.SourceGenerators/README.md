# Muonroi.RuleEngine.SourceGenerators

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.SourceGenerators.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.SourceGenerators/)

> Roslyn source generators and code analyzers for compile-time rule extraction and validation in the Muonroi ecosystem.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.SourceGenerators
```

## Overview
Automates rule registration and generation at compile time. Using `ExtractAsRuleGenerator`, methods decorated with `[MExtractAsRule]` are converted into strongly-typed rule classes. It also includes `RuleRegistrationGenerator` and `RuleCatalogRegistrationGenerator` to automatically generate dependency injection code, eliminating manual boilerplate.

## Features
- **Code Extraction**: `ExtractAsRuleGenerator` transforms `[MExtractAsRule]` methods into highly-optimized `.g.cs` rule classes.
- **Auto-Registration**: Generates `MGeneratedRuleRegistrationExtensions.g.cs` via `RuleRegistrationGenerator` for frictionless DI setup.
- **Ecosystem Analyzers**: Roslyn analyzers (`MBB001`–`MBB007`) enforce architectural boundaries and rule safety at compile time.
- **FEEL Validation**: Validates inline FEEL expressions at compile time via `FeelExpressionSyntaxValidator`.
- **Code Fixes**: Provides one-click Roslyn auto-fixes for detected rule engine violations.

## Quick Start
Add the package to your project. It runs automatically during compilation.

```csharp
// 1. Mark a method for rule extraction
[MExtractAsRule("FraudDetection")]
public bool IsFraudulent(OrderContext context) => context.Amount > 10000;

// 2. Register generated rules at startup
builder.Services.AddMGeneratedRules();
```

## Ecosystem Combinations

### + RuleEngine.Core → Frictionless Rule Authoring
Eliminates the need to manually implement `IRule<TContext>`. Write pure functions and let the source generator create the necessary boilerplate for the `RuleOrchestrator`.

### + RuleGen CLI → Command Line Extraction
Works alongside `tools/Muonroi.RuleGen` to extract rules to disk and verify compilation targets via `CompileCheckService`.

## Samples
- [`RuleSourceGen`](../../samples/RuleSourceGen)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



