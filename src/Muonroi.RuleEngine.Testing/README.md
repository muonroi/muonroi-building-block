# Muonroi.RuleEngine.Testing

> Test helpers for Muonroi rule orchestration — a fluent builder, an orchestrator spy, and `FactBag` assertions with no external test framework required.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Testing.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Testing/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package supplements `Muonroi.RuleEngine.Core` for unit and integration-style rule tests. It wires a lightweight in-process DI container so rules and orchestrators run exactly as they do in production, and it exposes assertion helpers for the `FactBag` that rules produce. There is no dependency on xUnit, NUnit, or any other test framework — use it alongside whichever runner your project already uses.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Testing --prerelease
```

## Quick Start

### Test a single rule by type

```csharp
// Given a rule that reads the context and writes to FactBag:
MRuleTestResult result = await MRuleTestBuilder<OrderContext>
    .ForRule<DiscountRule>()
    .WithContext(ctx => ctx.OrderTotal = 150m)
    .WithFact("currency", "USD")
    .ExecuteAsync();

Assert.True(result.IsSuccess);
result.Facts.MShould()
    .Contain("discount", 15m)
    .NotContain("error");
```

### Test a full orchestrator pipeline

```csharp
MRuleTestResult result = await MRuleTestBuilder<OrderContext>
    .ForOrchestrator(engine =>
    {
        engine.AddRule<DiscountRule>();
        engine.AddRule<ShippingRule>();
    })
    .WithContext(new OrderContext { OrderTotal = 200m })
    .ExecuteAsync();

Assert.Contains("DiscountRule", result.ExecutedRuleCodes!);
```

### Inspect execution with the spy

```csharp
MRuleOrchestratorSpy<OrderContext> spy = new(
    rules: [new DiscountRule(), new ShippingRule()]);

FactBag facts = await spy.ExecuteAsync(new OrderContext { OrderTotal = 300m });

MRuleExecutionRecord first = spy.ExecutionRecords[0];
Assert.True(first.IsSuccess);
Assert.True(first.Duration < TimeSpan.FromSeconds(1));
```

## Features

- **`MRuleTestBuilder<TContext>`** — fluent builder that bootstraps a `ServiceCollection`, runs a single `IRule<TContext>` or a full `RuleOrchestrator<TContext>`, and returns a typed `MRuleTestResult`.
- **`ForRule<TRule>()`** — resolves the rule from DI so its constructor dependencies can be satisfied via `WithService<T>()`.
- **`ForRule(IRule<TContext>)`** — accepts a pre-built rule instance for inline or mock rules.
- **`ForOrchestrator(Action<MRuleEngineBuilder<TContext>>)`** — configures and runs the real `RuleOrchestrator<TContext>`, capturing the execution order via an internal `IRuleEventListener`.
- **`WithContext` / `WithFact` / `WithService`** — seed the context, pre-populate `FactBag` entries, and register additional services before execution.
- **`MRuleOrchestratorSpy<TContext>`** — wraps `RuleOrchestrator<TContext>` and records per-rule execution data (`RuleCode`, `IsSuccess`, `Duration`, per-fact `Changes`) without any mocking framework.
- **`MFactBagAssertions` / `FactBagAssertions`** — extension methods (`.MShould()` / `.Should()`) returning `MFactBagAssertion` with `.Contain(key[, value])` and `.NotContain(key)` helpers that throw `MInternalException` on failure.
- **No external test framework dependency** — works with xUnit, NUnit, MSTest, or plain console runners.

## API Reference

| Type | Purpose |
|------|---------|
| `MRuleTestBuilder<TContext>` | Fluent builder; entry point for single-rule and orchestrator tests |
| `MRuleTestResult` | Record returned by `ExecuteAsync()`: `IsSuccess`, `Facts`, `RuleResult`, `Exception`, `ExecutedRuleCodes` |
| `MRuleOrchestratorSpy<TContext>` | Wraps `RuleOrchestrator<TContext>`; exposes `ExecutionRecords`, `BeforeSnapshot`, `AfterSnapshot` |
| `MRuleExecutionRecord` | Immutable record: `RuleCode`, `IsSuccess`, `Duration`, `Changes` (old/new per-fact map) |
| `MFactBagAssertion` | Fluent assertion: `.Contain(key[, value])`, `.NotContain(key)` |
| `MFactBagAssertions` | Extension class exposing `.MShould()` on `FactBag` |
| `FactBagAssertions` | Extension class exposing `.Should()` on `FactBag` (alias) |

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — `IRule<TContext>`, `FactBag`, `RuleResult`, `IRuleEventListener<TContext>` contracts consumed by this package
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — `RuleOrchestrator<TContext>`, `MRuleEngineBuilder<TContext>`, and the `AddRuleEngine<TContext>()` extension used internally by `ForOrchestrator`

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
