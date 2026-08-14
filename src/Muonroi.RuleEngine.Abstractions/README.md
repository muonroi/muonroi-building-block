# Muonroi.RuleEngine.Abstractions

> Core contracts for evaluating complex business logic, dynamic decision tables, and saga-based compensations.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.RuleEngine.Abstractions` package provides the foundational contracts for the Muonroi Rule Engine. In complex enterprise applicationsâ€”especially multi-tenant systemsâ€”business rules (e.g., pricing calculations, eligibility checks, validation logic) change rapidly and often differ per tenant. Hardcoding these rules in application logic leads to brittle systems.

This package defines the interfaces required to decouple business logic from application flow. It supports compiled C# rules, dynamic Decision Tables (DMN-lite), and Saga-style compensation strategies. By standardizing around an `IMRuleOrchestrator`, rules can be evaluated centrally with full observability, tenant isolation, and predictable execution modes.

## Features

- **Core Rule Contracts**: Defines `IRule` and `ICompensatableRule` for implementing discrete units of business logic with optional rollback capabilities.
- **Rule Orchestration**: The `IMRuleOrchestrator` interface defines how collections of rules are executed against a specific `IRuleContext` and `FactBag`.
- **Execution Modes**: Supports robust error handling strategies via the `ExecutionMode` enum (`AllOrNothing`, `BestEffort`, `CompensateOnFailure`).
- **Dynamic Authoring**: Provides metadata attributes (`[MRuleContextDescription]`, `[MRuleCatalogEntry]`) and the `IRuleAuthoringManifestProvider` to allow UIs to dynamically generate rule-builder interfaces.
- **Canary Rollouts**: Contracts for managing rule lifecycle, approvals (`IRuleSetApprovalService`), and safe canary deployments (`ICanaryRolloutService`).
- **Hooks & Telemetry**: Exposes `IHookHandler` and `IRuleEventListener` for intercepting rule execution lifecycle events.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Abstractions
```

## Quick Start

### Defining a Rule Context

A Rule Context represents the state required to evaluate a set of rules. It acts as the strongly-typed payload passed through the engine.

```csharp
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Authoring;

[MRuleContextDescription("Discount Context", "Context used for calculating order discounts.")]
public class OrderDiscountContext : IRuleContext
{
    [MRuleFactDescription("Total Order Amount", "The total monetary value of the order before discounts.")]
    public decimal TotalAmount { get; set; }
    
    [MRuleFactDescription("Customer Tier", "The loyalty tier of the customer (e.g., Gold, Silver).")]
    public string CustomerTier { get; set; } = string.Empty;

    public bool IsValid() => TotalAmount >= 0;
}
```

### Implementing a Code-Based Rule

While rules can be defined dynamically (e.g., via decision tables), you can also write compiled rules by implementing `IRule`.

```csharp
using Muonroi.RuleEngine.Abstractions;
using System.Threading;
using System.Threading.Tasks;

[MRuleCatalogEntry("Gold Tier Discount", "Applies a 10% discount to Gold tier customers.", RuleType.Validation)]
public class GoldTierDiscountRule : IRule
{
    public string Name => "GoldTierDiscount";
    public string Group => "Pricing";

    public Task<RuleResult> EvaluateAsync(IRuleContext context, FactBag factBag, CancellationToken cancellationToken)
    {
        if (context is OrderDiscountContext orderContext && orderContext.CustomerTier == "Gold")
        {
            // Apply discount logic here or mutate the FactBag
            factBag.Set(new FactKey("DiscountPercentage", typeof(decimal)), 10m);
            return Task.FromResult(RuleResult.SuccessResult());
        }

        return Task.FromResult(RuleResult.SkippedResult("Customer is not Gold tier."));
    }
}
```

### Saga Pattern Support (Compensatable Rules)

For operations that mutate state across distributed systems, implement `ICompensatableRule`. If a subsequent rule fails, the orchestrator (if configured for `ExecutionMode.CompensateOnFailure`) will call `CompensateAsync` on previously successful rules.

```csharp
public class ReserveInventoryRule : ICompensatableRule
{
    public string Name => "ReserveInventory";
    public string Group => "OrderFulfillment";

    public async Task<RuleResult> EvaluateAsync(IRuleContext context, FactBag factBag, CancellationToken token)
    {
        return RuleResult.SuccessResult();
    }

    public async Task CompensateAsync(IRuleContext context, FactBag factBag, CancellationToken token)
    {
        // Compensate logic
    }
}
```

## API Reference

### Execution Models

- `IMRuleOrchestrator`: The primary entry point for executing rule sets.
- `IRuleContext`: The strongly-typed data passed into the rule execution pipeline.
- `FactBag`: A thread-safe, loosely-typed dictionary for sharing transient state between rules during execution.
- `OrchestratorResult`: Contains detailed feedback about the execution.

### Adapters

- `IContextProjector`: Projects domain entities (like a Database Model) into an `IRuleContext`.
- `IContextFactory`: Instantiates rule contexts.

## Ecosystem Combinations

### + Muonroi.RuleEngine.Core â†’ Concrete Orchestration
The core engine binds to these abstractions, implementing `IMRuleOrchestrator` to evaluate the rules according to the selected `ExecutionMode`.

### + Muonroi.Quota.Abstractions â†’ Execution Budgets
An implementation of `IHookHandler` can inject `ITenantQuotaTracker` to decrement a tenant's evaluation limits each time `IMRuleOrchestrator.EvaluateAsync` is called, throwing a `QuotaExceededException` if they exceed their SaaS plan.

### + Muonroi.Observability â†’ Lifecycle Metrics
Implementing `IRuleEventListener` allows you to export rule execution durations, cache hits, and validation failures directly to `RuleEngineTelemetryDescriptor` OTel meters.

### Full Rule Engine Stack
```csharp
builder.Services
    .AddRuleEngineCore(config)
    .AddTenantContext(config)
    .AddMuonroiObservability(config)
    .AddInMemoryTenantQuotas();
```

## Samples
- [`Quickstart.RuleEngine.Abstractions`](../../samples/Quickstart.RuleEngine.Abstractions)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
