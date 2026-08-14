# Muonroi.RuleEngine.NRules

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.NRules.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.NRules/)

> NRules forward-chaining inference engine integration for the Muonroi ecosystem.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.NRules
```

## Overview
Integrates the powerful NRules Rete-based rules engine into the Muonroi pipeline. It wraps execution via `NRulesEngine`, resolving and executing rules marked with `RuleAttribute` based on `RuleOptions`. It also provides `NRulesController` to manage rules and the `NRulesManifestContributor` to sync rules with the ecosystem's authoring schemas.

## Features
- **Inference Engine**: Execute complex, forward-chaining logic via `NRulesEngine`.
- **Declarative Rules**: Map traditional NRules classes into the ecosystem using `RuleAttribute`.
- **Configuration**: Expose dynamic toggles and behavior via `RuleOptions`.
- **Manifest Integration**: Propagate schema information natively with `NRulesManifestContributor`.
- **API Endpoints**: Monitor and trigger NRules state via `NRulesController`.

## Quick Start
```csharp
// Register NRules execution engine
builder.Services.AddMuonroiNRules(options =>
{
    options.RuleAssemblies = new[] { typeof(DiscountRule).Assembly };
});

// Resolve and execute inference
var nRulesEngine = provider.GetRequiredService<NRulesEngine>();
await nRulesEngine.ExecuteAsync(session);
```

## Ecosystem Combinations

### + RuleEngine.Core → Hybrid Execution
Combine the forward-chaining logic of NRules with the sequential orchestrator pipeline (`IMRuleOrchestrator<TContext>`). Use NRules for complex pattern matching, and the core engine for procedural validations.

### + RuleEngine.Runtime.Web → Unified Manifest
`NRulesManifestContributor` automatically injects NRules schema definitions into the standard `/api/v1/rule-flow/contract` endpoint, allowing UI builders to see both normal rules and NRules definitions seamlessly.

## Samples
- [`Quickstart.RuleEngine`](../../samples/Quickstart.RuleEngine)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



