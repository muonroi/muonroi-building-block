# Muonroi.RuleEngine.Core

> Typed rule orchestration pipeline for .NET — register rules, hooks, and listeners, then execute them in dependency order against any context object.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Core.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Core/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.RuleEngine.Core` is the runtime implementation layer of the Muonroi rule engine stack. It provides `RuleOrchestrator<TContext>`, the fluent `MRuleEngineBuilder<TContext>`, execution-mode routing (`RuleExecutionMode`), workflow orchestration (`MRuleWorkflowRunner`), structured tracing, and audit hooks. It sits on top of `Muonroi.RuleEngine.Abstractions` (contracts) and integrates with `Muonroi.Integration.Connectors` for event bridge and webhook support.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Core --prerelease
```

## Quick Start

**1. Register the engine (Program.cs)**

```csharp
using Muonroi.RuleEngine.Core;

builder.Services.AddRuleEngine<OrderRequest>();          // registers orchestrator + router
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);  // scans IRule<T> and IHookHandler<T>
```

**2. Implement a rule**

```csharp
using Muonroi.RuleEngine.Abstractions;

[RuleGroup("order-discount")]
public sealed class HighValueOrderRule : IRule<OrderRequest>
{
    public string Code => "HIGH_VALUE_ORDER";
    public int Order => 0;

    public Task<RuleResult> EvaluateAsync(OrderRequest context, FactBag facts, CancellationToken ct)
    {
        if (context.Amount <= 0)
            return Task.FromResult(RuleResult.Failure("Amount must be greater than zero."));

        bool isHighValue = context.Amount >= 1000m;
        facts.Set("discountRate", isHighValue ? 0.10m : 0m);
        facts.Set("isHighValueOrder", isHighValue);
        return Task.FromResult(RuleResult.Passed());
    }
}
```

**3. Execute rules**

```csharp
public sealed class OrdersController(RuleOrchestrator<OrderRequest> orchestrator) : ControllerBase
{
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateAsync([FromBody] OrderRequest request, CancellationToken ct)
    {
        FactBag facts = await orchestrator.ExecuteAsync(request, cancellationToken: ct);
        decimal discountRate = facts.Get<decimal>("discountRate");
        return Ok(new { DiscountRate = discountRate });
    }
}
```

## Features

- **Dependency-ordered execution** — rules declare `DependsOn` (by code) and `Dependencies` (by type); the orchestrator topologically sorts them and detects cycles at startup.
- **Fluent builder** — `AddRuleEngine<TContext>()` returns `MRuleEngineBuilder<TContext>` with `.AddRule<T>()`, `.AddHook<T>()`, and `.AddListener<T>()` for inline registration without assembly scanning.
- **Assembly scanning** — `AddRulesFromAssemblies(Assembly[])` registers all `IRule<T>` and `IHookHandler<T>` implementations, and maps `[RuleGroup]` / `[TenantRuleGroup]` keys to keyed DI entries.
- **Execution-mode routing** — `IMRuleExecutionRouter<TContext>` routes between `Rules`, `Traditional`, `Parallel`, and `ABTest` modes; weights and difference logging are controlled via `MRuleEngineOptions`.
- **Structured result** — `ExecuteWithResultAsync(context, ExecutionMode, ...)` returns `OrchestratorResult` with per-rule pass/fail, error list, and compensation errors.
- **Saga / compensation** — rules that implement `ICompensatableRule<TContext>` are rolled back in reverse order when `ExecutionMode.CompensateOnFailure` is used.
- **Workflow orchestration** — `MRuleWorkflowRunner<TContext>` drives step-by-step workflows (rule tasks, service tasks, gateways) defined via `MRuleWorkflowDefinition<TContext>`.
- **Tracing and audit** — `IRuleExecutionTracer` captures per-phase `RuleTraceEntry` records (BeforeEval, AfterEval, AfterExec, Error, Compensate); `RuleAuditLogger` and `AuditTrailHook` provide structured logging out of the box.
- **Tenant quota enforcement** — when `ITenantQuotaTracker` is registered, the orchestrator enforces `ConcurrentExecutions` and `RuleEvaluationsPerSecond` quotas before each rule.
- **OpenTelemetry metrics** — `RuleEngineTelemetry` emits `rules.matched`, `rules.fired`, and `rule.eval.duration_ms` metrics with `rule.id` and `rule.set.version` tags.
- **Event bridge and webhooks** — `AddRuleEventBridge()` / `AddRuleWebhook(WebhookOptions)` wire `IEventSink` and `IRuleWebhookNotifier` for external event fan-out.

## Configuration

```csharp
builder.Services
    .AddRuleEngine<OrderRequest>(options =>
    {
        options.ExecutionMode = RuleExecutionMode.Rules;   // Rules | Traditional | Parallel | ABTest
        options.TraditionalWeight = 0.3;                   // only used in ABTest mode
        options.RulesWeight = 0.7;
        options.LogDifferences = true;                     // log fact diffs between modes
    })
    .AddRule<HighValueOrderRule>()
    .AddHook<MyAuditHook>()
    .AddListener<MyEventListener>();

// Optional: workflow execution safeguards
builder.Services.ConfigureRuleWorkflow(opts => opts.MaxSteps = 512);
```

`MRuleEngineOptions` is bound from `services.Configure<MRuleEngineOptions>()` and is also addressable via `IOptions<MRuleEngineOptions>` in any rule or hook.

## API Reference

| Type | Purpose |
|------|---------|
| `RuleOrchestrator<TContext>` | Core pipeline — topologically sorts rules, runs hooks, traces, enforces quotas, emits OTel metrics. `ExecuteAsync` returns `FactBag`; `ExecuteWithResultAsync` returns `OrchestratorResult`. |
| `MRuleEngineBuilder<TContext>` | Fluent builder returned by `AddRuleEngine<TContext>()`. Chain `.AddRule<T>()`, `.AddHook<T>()`, `.AddListener<T>()`. |
| `MRuleEngineOptions` | Runtime options: `ExecutionMode`, `TraditionalWeight`, `RulesWeight`, `LogDifferences`. |
| `IMRuleExecutionRouter<TContext>` | Routes `ExecuteAsync(context, traditionalPath?, modeOverride?)` to the correct execution branch. |
| `IMRuleWorkflowRunner<TContext>` | Executes an `MRuleWorkflowDefinition<TContext>` step-by-step, returning `MRuleWorkflowResult<TContext>`. |
| `MRuleWorkflowDefinition<TContext>` | Declares workflow steps, start step, and transitions. |
| `MRuleWorkflowStep<TContext>` | A single step (rule task, service task, or gateway) within a workflow. |
| `MRuleWorkflowOptions` | `MaxSteps` (default 256) — guards against cyclic workflow transitions. |
| `IRuleExecutionTracer` | Receives `RuleTraceEntry` records per rule phase. Implement to persist traces to any store. |
| `IRuleTraceStore` | Persistence contract consumed by tracer implementations. |
| `IRuleDebuggerModeService` | Enables/disables per-tenant debug tracing at runtime. |
| `ITraceRedactor` | Redacts sensitive fact values before traces are written. |
| `RuleAuditLogger<TContext>` | Built-in `IRuleEventListener<TContext>` that logs matched and fired events via `IMLog`. |
| `AuditTrailHook<TContext>` | Built-in `IHookHandler<TContext>` that writes audit trail entries at `BeforeRule` / `AfterRule` hook points. |
| `IEventSink` | Receives domain events emitted by the rule pipeline; registered via `AddRuleEventBridge()`. |
| `IRuleWebhookNotifier` | Delivers rule events to configured HTTP webhook endpoints. |
| `IFeatureFlagClient` | Contract for feature-flag evaluation integrated into rule conditions. |
| `FeatureFlagEvaluator` | Default implementation of `IFeatureFlagClient`. |

## Samples

- [Quickstart.RuleEngine](../../samples/Quickstart.RuleEngine/) — minimal ASP.NET Core API: `AddRuleEngine<OrderRequest>()`, `AddRulesFromAssemblies`, and `RuleOrchestrator<T>` injection in a controller.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — contracts (`IRule<T>`, `IHookHandler<T>`, `IRuleEventListener<T>`, `FactBag`, `RuleResult`); required by all rule implementations.
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — file-backed `IRuleSetStore`, `RulesEngineService`, and hot-reload support.
- [`Muonroi.RuleEngine.EntityFrameworkCore`](../Muonroi.RuleEngine.EntityFrameworkCore/) — Postgres-backed rule store via `AddMRuleEngineWithPostgres()`.
- [`Muonroi.RuleEngine.SourceGenerators`](../Muonroi.RuleEngine.SourceGenerators/) — compile-time dispatcher generation for rule contexts.
- [`Muonroi.RuleEngine.Testing`](../Muonroi.RuleEngine.Testing/) — test harness and assertion helpers for rules.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
