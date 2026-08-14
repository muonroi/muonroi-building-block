# Muonroi.RuleEngine.DecisionTable

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.DecisionTable.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.DecisionTable/)

> Decision table execution engine supporting DMN-style rule execution with various hit policies and FEEL expressions.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.DecisionTable
```

## Overview
Provides support for representing and executing business rules as Decision Tables within the Muonroi ecosystem. It uses `IDecisionTableExecutor` and `DecisionTableExecutor` to evaluate a `DecisionTable` against input facts, applying a `HitPolicy` (Unique, Any, First, All, Collect, RuleOrder) to return a `DecisionTableExecutionResult`.

## Features
- **Table Execution**: `DecisionTableExecutor` evaluates input facts against decision tables and outputs matched rows.
- **Hit Policies**: Supports standard DMN hit policies through the `HitPolicy` enum.
- **FEEL Expressions**: Cells are evaluated using `IFeelCellEvaluator`, with implementations like `FullFeelCellEvaluator` and `SimplifiedFeelCellEvaluator`.
- **Validation**: Ensures correctness using `DecisionTableValidator`, `OverlapDetector`, `MultiColumnOverlapDetector`, and `GapDetector`.
- **Storage Abstractions**: Persist tables via `IDecisionTableStore`, with `InMemoryDecisionTableStore` and `EfCoreDecisionTableStore`.

## Quick Start
```csharp
// Evaluate a decision table
var executor = provider.GetRequiredService<IDecisionTableExecutor>();
var result = await executor.ExecuteAsync(decisionTable, inputFacts, cancellationToken);
```

## Ecosystem Combinations

### + RuleEngine.Core → Decision Tables as Rules
Decision tables implement `IRule<TContext>` — the orchestrator evaluates them alongside compiled C# rules with the same `FactBag` pipeline.

### + Tenancy.Core → Per-Tenant Table Versions
Each tenant can have its own version of a decision table active simultaneously via `RuleSetStatus` and canary rollout.

### + RuleEngine.Runtime → Persisted, Hot-Reloadable Tables
Tables stored in Postgres or SQL Server. Update a row in the database — the change applies on the next evaluation cycle without restart.

### + FEEL (Muonroi.Rules) → Rich Cell Expressions
`FullFeelCellEvaluator` supports the complete FEEL expression language in table cells: date ranges, list membership, arithmetic comparisons.

### Full Rule Engine Production Stack
```csharp
builder.Services
    .AddDecisionTableExecution()
    .AddFullFeelEvaluator()
    .AddInMemoryDecisionTableStore();
```

## Samples
- [`Quickstart.DecisionTable`](../../samples/Quickstart.DecisionTable)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



