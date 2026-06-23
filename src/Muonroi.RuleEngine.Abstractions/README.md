# Muonroi.RuleEngine.Abstractions

> Contracts-only package that defines the rule engine interfaces, execution model, and shared value types used across the entire Muonroi Rule Engine ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships **contracts only** — interfaces, records, enums, and attributes. It contains no orchestration runtime, DI registrations, or rule-execution logic. Depend on it when authoring rules or writing code that consumes orchestration results without a direct dependency on a concrete engine.

For a working rule engine, add [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) (base orchestrator + DI wiring) or one of the specialised adapters such as [`Muonroi.AspNetCore.RuleEngine`](../Muonroi.AspNetCore.RuleEngine/).

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Abstractions --prerelease
```

## Quick Start

Implement `IRule<TContext>` to define a rule. The context type must implement `IRuleContext`.

```csharp
using Muonroi.RuleEngine.Abstractions;

// 1. Define the context
public sealed class OrderContext : IRuleContext
{
    public decimal Amount { get; init; }
    public string CustomerType { get; init; } = "";

    private bool _halted;
    public void HaltGroup() => _halted = true;
}

// 2. Implement a rule
[RuleGroup("order-discount")]
public sealed class HighValueOrderRule : IRule<OrderContext>
{
    public string Code => "HIGH_VALUE_ORDER";
    public int Order => 0;

    public Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct)
    {
        bool isHighValue = ctx.Amount >= 1000m;
        decimal discount = isHighValue ? 0.10m : 0m;
        facts.Set("discountRate", discount);
        facts.Set("isHighValueOrder", isHighValue);
        return Task.FromResult(RuleResult.Passed());
    }
}
```

Register and execute via [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/):

```csharp
// Program.cs (requires Muonroi.RuleEngine.Core)
using Muonroi.RuleEngine.Core;

builder.Services.AddRuleEngine<OrderContext>();
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);
```

See [Quickstart.RuleEngine](../../samples/Quickstart.RuleEngine/) for a complete working example.

## Features

- `IRule<TContext>` — base rule contract with `Code`, `Order`, `DependsOn`, `HookPoint`, and `EvaluateAsync` / `ExecuteAsync`
- `ICompensatableRule<TContext>` — extends `IRule<TContext>` with `CompensateAsync` for Saga-pattern rollback
- `IRuleContext` — minimal context marker with `HaltGroup()` to short-circuit a rule group
- `FactBag` — typed key/value store shared between rules during a single orchestration run; supports `Set<T>`, `Get<T>`, `TryGet<T>`, `Remove`, and `JsonElement` auto-conversion
- `IMRuleOrchestrator<TContext>` — primary orchestration entry point; returns `OrchestratorResult`
- `OrchestratorResult` — immutable execution summary with `IsSuccess`, `ExecutionMode`, per-rule `RuleResults`, aggregated `Errors`, and `CompensationErrors`
- `ExecutionMode` — three strategies: `AllOrNothing` (default, stops on first failure), `BestEffort` (aggregates all failures), `CompensateOnFailure` (stops + compensates in reverse order)
- `RuleExecutionMode` — routing enum for gradual migration: `Traditional`, `Rules`, `Hybrid`, `Shadow`
- `IRuleFactory<TContext>` — DI-based rule instantiation contract
- `IRuleEventListener<TContext>` — hook interface for observing rule lifecycle events
- `IHookHandler<TContext>` — interface for pre/post rule execution hooks
- Authoring attributes — `[RuleGroup]`, `[TenantRuleGroupAttribute]`, `[ExtractAsRuleAttribute]`, `[RuleModeAttribute]`, `[MRuleCatalogEntryAttribute]`, `[MRuleContextDescriptionAttribute]`, `[MRuleFactDescriptionAttribute]`
- Authoring manifest models — `MRuleAuthoringManifest`, `MRuleAuthoringEntry`, `MFactEntry`, `MFactSchemaNode`, `MFactSchemaField`
- Decision table models — `RawDecisionTable`, `RawHitPolicy`, FEEL expression types
- Canary rollout contracts — `ICanaryRolloutService`, `CanaryRolloutRecord`, `StartCanaryRequest`
- Rule set approval contracts — `IRuleSetApprovalService`, `RuleSetRecord`, `RuleSetStatus`

## Configuration

This package contains no DI registrations or runtime configuration. Configuration (options, assembly scanning, quota hooks) lives in `Muonroi.RuleEngine.Core`. See that package's README for `AddRuleEngine<TContext>` and `MRuleEngineOptions`.

## API Reference

| Type | Purpose |
|------|---------|
| `IRule<TContext>` | Base rule contract; implement to define a rule |
| `ICompensatableRule<TContext>` | Rule with Saga compensation support |
| `IRuleContext` | Context marker; must be implemented by every context type |
| `FactBag` | Typed shared-state bag passed between rules |
| `RuleResult` | Per-rule outcome; factory methods `Passed()`, `Success()`, `Failure(errors)` |
| `IMRuleOrchestrator<TContext>` | Run all registered rules and return an `OrchestratorResult` |
| `OrchestratorResult` | Immutable run summary; factory methods `Success(...)`, `Failure(...)` |
| `ExecutionMode` | `AllOrNothing` / `BestEffort` / `CompensateOnFailure` |
| `RuleExecutionMode` | `Traditional` / `Rules` / `Hybrid` / `Shadow` |
| `HookPoint` | Enum for hook placement relative to rule execution |
| `IRuleFactory<TContext>` | DI-based rule factory contract |
| `IRuleEventListener<TContext>` | Lifecycle event observer contract |
| `IHookHandler<TContext>` | Pre/post execution hook contract |
| `[RuleGroup(key)]` | Groups rules into a named execution group |
| `[TenantRuleGroupAttribute(workflow, tenant)]` | Tenant-scoped rule group |
| `[ExtractAsRuleAttribute(code)]` | Marks a method for rule source-generator extraction |
| `[RuleModeAttribute(mode)]` | Declares the `RuleExecutionMode` for a rule |
| `MRuleAuthoringManifest` | Describes an entire rule catalog for tooling/authoring UIs |
| `ICanaryRolloutService` | Contract for canary rollout management |
| `IRuleSetApprovalService` | Contract for rule set approval workflows |
| `RawDecisionTable` | Raw model for decision table rules |

## Samples

- [Quickstart.RuleEngine](../../samples/Quickstart.RuleEngine/) — minimal rule with `IRule<TContext>`, `FactBag`, and `RuleResult`
- [Quickstart.AspNetCore.RuleEngine](../../samples/Quickstart.AspNetCore.RuleEngine/) — ASP.NET Core integration with middleware pipeline
- [Quickstart.RuleEngine.EntityFrameworkCore](../../samples/Quickstart.RuleEngine.EntityFrameworkCore/) — persisted rule sets via EF Core
- [Quickstart.RuleEngine.NRules](../../samples/Quickstart.RuleEngine.NRules/) — NRules backend adapter
- [Quickstart.RuleEngine.Proliferation](../../samples/Quickstart.RuleEngine.Proliferation/) — rule proliferation and catalog management
- [Quickstart.RuleEngine.Runtime.Web](../../samples/Quickstart.RuleEngine.Runtime.Web/) — runtime web API for rule management

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — concrete orchestrator, DI extensions (`AddRuleEngine<T>`, `AddRulesFromAssemblies`), and `MRuleEngineOptions`
- [`Muonroi.AspNetCore.RuleEngine`](../Muonroi.AspNetCore.RuleEngine/) — ASP.NET Core middleware integration
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — full runtime with rule set persistence, FEEL expressions, and workflow cache
- [`Muonroi.RuleEngine.Runtime.Web`](../Muonroi.RuleEngine.Runtime.Web/) — REST API layer for runtime rule management
- [`Muonroi.RuleEngine.EntityFrameworkCore`](../Muonroi.RuleEngine.EntityFrameworkCore/) — EF Core rule set store
- [`Muonroi.RuleEngine.DecisionTable`](../Muonroi.RuleEngine.DecisionTable/) — decision table evaluation engine
- [`Muonroi.RuleEngine.NRules`](../Muonroi.RuleEngine.NRules/) — NRules backend adapter
- [`Muonroi.RuleEngine.SourceGenerators`](../Muonroi.RuleEngine.SourceGenerators/) — source generator that scaffolds rule registrations from `[ExtractAsRuleAttribute]`
- [`Muonroi.RuleEngine.Testing`](../Muonroi.RuleEngine.Testing/) — fluent test helpers and `MFactBagAssertions`

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
