# Muonroi.RuleEngine.Core

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Core.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Core/)

> Core execution orchestrator for the Muonroi Rule Engine, providing rule evaluation, dependency resolution, and FactBag state management.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Core
```

## Overview
The core orchestration layer for executing rule pipelines. It defines `RuleOrchestrator<TContext>` which implements `IMRuleOrchestrator<TContext>` to evaluate sequences of `IRule<TContext>`. State transitions between rules are managed via the `FactBag`, and each rule returns a `RuleResult` determining execution flow based on the current `ExecutionMode` (e.g. AllOrNothing, BestEffort).

## Features
- **Engine Configuration**: `MRuleEngineBuilder` for fluent setup of execution options and dependencies.
- **Rule Definitions**: `IRule<TContext>` for defining atomic pieces of logic, with properties for Code, Order, DependsOn, and HookPoint.
- **State Management**: `FactBag` for strongly typed state passing between rules via `Get<T>()` and `Set<T>()`.
- **Execution Orchestration**: `IMRuleOrchestrator<TContext>` and `RuleOrchestrator<TContext>` to execute rule chains and aggregate an `OrchestratorResult`.
- **Execution Modes**: Configure behavior on failure via the `ExecutionMode` enum (AllOrNothing, BestEffort, CompensateOnFailure).

## Quick Start
```csharp
// Configure engine dependencies and defaults
builder.Services.AddRuleEngine<OrderContext>(options =>
{
    options.Mode = ExecutionMode.BestEffort;
});

// Resolve the orchestrator and execute
var orchestrator = provider.GetRequiredService<IMRuleOrchestrator<OrderContext>>();
var result = await orchestrator.ExecuteAsync(new OrderContext(), cancellationToken);
```

## Ecosystem Combinations

### + Mediator → Rules Run Before Every Command
`MRuleEngineBehavior` in the mediator pipeline calls `IMRuleOrchestrator.ExecuteAsync()` before the handler. Business rules become hot-reloadable without touching handler code:
```csharp
public class PlaceOrderCommand : IRequest<OrderDto>, IMRuleRequest
{
    public string RuleContext => "ORDER_PLACEMENT";
}
// Handler is pure — rules evaluated by the pipeline, not the handler
```

### + Tenancy.Core → Per-Tenant Rule Quotas
`ITenantQuotaTracker` gates how many rule evaluations a tenant can execute per time window. Free tier: 1,000/day. Enterprise: unlimited.

### + Observability → Per-Rule OTel Spans
Every `IRule<TContext>.EvaluateAsync()` call becomes an OTel span via `IRuleExecutionTracer`. Diagnose slow rules in Grafana.

### Full Rule Engine Production Stack
```csharp
builder.Services
    .AddRuleEngine<OrderContext>()             // core orchestrator
    .AddRuleEngineRuntime(config)              // persistence + hot-reload
    .AddTenantContext(config)                  // per-tenant quotas
    .AddMuonroiObservability(config)           // per-rule OTel spans
    .AddMMediator(opt => opt.AddMuonroiEcosystem()); // rules in pipeline
```

## Samples
- [`Quickstart.RuleEngine`](../../samples/Quickstart.RuleEngine)
- [`LoanApproval`](../../samples/LoanApproval)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



