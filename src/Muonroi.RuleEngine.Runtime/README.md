# Muonroi.RuleEngine.Runtime

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Runtime.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Runtime/)

> Dynamic rule execution environment providing hot-reload, persistence, caching, and workflow graph dispatch for the Muonroi ecosystem.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Runtime
```

## Overview
The runtime layer provides the infrastructure to persist, load, and hot-reload rules. It features `RulesEngineService` and `RuleEngine<T>` for rule management. Rules are persisted via `IRuleSetStore` (such as `PostgresRuleSetStore` and `FileRuleSetStore`) and stored as `RuleSetRecord` entities. It also supports complex rule execution graphs through `RuleGraphParser` and `GraphRuleDispatchAdapter`.

## Features
- **Rule Persistence**: Store rules in databases or files using `IRuleSetStore`, `RuleEngineDbContext`, and `FileRuleSetStore`.
- **Hot-Reload & Caching**: Fast rule execution with `RuleSetRuntimeCache` and live updates via `ICanaryRolloutService` and `RuleSetApprovalService`.
- **Security & Integrity**: Ensure rule validity using `IRuleSetSigner`, `HmacSha256RuleSetSigner`, and `RsaRuleSetAuditSigner`.
- **Rule Adapters**: Execute different rule types via `FeelRuleAdapter`, `DecisionTableRuleAdapter`, and `SubFlowRuleAdapter`.
- **Workflow Graphs**: Compile and dispatch complex execution flows using `RuleGraphParser` and `GraphRuleDispatchAdapter`.

## Quick Start
```csharp
// Configure runtime rule services and persistence
builder.Services.AddRuleEngineRuntime(options =>
{
    options.UsePostgresRuleSetStore(connectionString);
    options.EnableRuntimeCache();
});

// Execute rules dynamically through the RulesEngineService
var engineService = provider.GetRequiredService<RulesEngineService>();
var result = await engineService.ExecuteAsync("fraud-detection-rules", factBag);
```

## Ecosystem Combinations

### + RuleEngine.Core → Full Rule Engine Capabilities
Combines dynamic runtime persistence with the in-memory core execution orchestrator for a complete business rules engine.

### + Tenancy.Core → Multi-Tenant Rule Environments
`IRuleSetStore` partitions rule sets by tenant. Rollout rules via `ICanaryRolloutService` to specific tenants before deploying globally.

### + Observability → Runtime Telemetry
`WorkflowCacheTelemetry` provides OpenTelemetry metrics for rule cache hits/misses, eviction counters, and hot-reload lag.

## Samples
- [`Quickstart.RuleEngine`](../../samples/Quickstart.RuleEngine)
- [`LoanApproval`](../../samples/LoanApproval)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



