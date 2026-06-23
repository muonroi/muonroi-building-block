# Muonroi.RuleEngine.Runtime

> Runtime orchestration layer for the Muonroi Rule Engine — provides file-backed ruleset storage, in-memory caching with hot-reload, Redis pub/sub invalidation, CloudEvents bridging, execution tracing, and pluggable signing/audit for rule evaluation pipelines.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Runtime.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Runtime/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.RuleEngine.Runtime` is the infrastructure layer that connects the abstract rule engine contracts (`Muonroi.RuleEngine.Abstractions`) and core orchestrator (`Muonroi.RuleEngine.Core`) to real-world storage, caching, and observability.
It ships a file-backed `IRuleSetStore`, an optional Postgres store, in-memory runtime caching invalidated through `IRuleSetChangeNotifier` (in-process or Redis pub/sub), HMAC-SHA-256 and RSA audit signing, and a CloudEvents bridge that publishes ruleset lifecycle changes to any `IEventSink`.
It also provides multi-dialect rule adapters (FEEL, JavaScript via Jint, Liquid, Scriban, decision tables, sub-flow graphs) so externally-authored rules can be executed without code recompilation.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Runtime --prerelease
```

## Quick Start

Register the file-backed store and execution services, then optionally enable Redis hot-reload for multi-node deployments:

```csharp
// Program.cs
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Tracing;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Core store + RulesEngineService + IRuleSetRuntimeCache
builder.Services.AddRuleEngineStore(builder.Configuration);

// Optional: replace in-process notifier with Redis pub/sub hot-reload
string? redisConnection = builder.Configuration.GetConnectionString("RuleEngineRedis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddMRuleEngineWithRedisHotReload(redisConnection);
}

// Optional: CloudEvents bridge — every ruleset lifecycle change emits a CloudEvent
builder.Services.AddRuleEventBridge();

// Optional: execution tracing backed by Redis
builder.Services.AddRuleEngineTracing(options =>
{
    options.DefaultTtl = TimeSpan.FromHours(48);
    options.RedactedFieldNames.Add("accountNumber");
});

WebApplication app = builder.Build();
app.Run();
```

Ruleset files are resolved from the path configured under `RuleStore:RootPath` (defaults to a `rules/` folder next to the application binary).

## Features

- **File-backed `IRuleSetStore`** — reads/writes ruleset JSON artifacts from disk; path traversal is blocked by `AllowedPathSegmentPattern`
- **Postgres-backed store** — `PostgresRuleSetStore` via EF Core + Npgsql for durable persistence (registered separately)
- **In-memory `IRuleSetRuntimeCache`** — hot-invalidated through `IRuleSetChangeNotifier`; configurable absolute expiry via `RuntimeCacheMinutes`
- **Redis hot-reload** — `RedisRuleSetChangeNotifier` subscribes to a configurable pub/sub channel (`muonroi:ruleset:changed`) so all nodes pick up ruleset changes without restart
- **CloudEvents bridge** — `CloudEventPublishingNotifier` decorates any `IRuleSetChangeNotifier` to forward lifecycle events to `IEventSink`; `RuleExecutionEventPublisher` and `IEventDrivenRuleEvaluator` integrate with event-driven pipelines
- **Multi-dialect adapters** — `FeelRuleAdapter`, `JavaScriptRuleAdapter` (Jint), `LiquidRuleAdapter`, `DecisionTableRuleAdapter`, `SubFlowRuleAdapter`, `ConnectorRuleAdapter`, `FeelRuleAdapter`
- **Flow graph execution** — `RuleGraphParser` applies Kahn's topological sort; `ContextAdaptedRule` and `ReflectionContextFactory`/`ReflectionContextProjector` bridge typed contexts to `FactBag`
- **Ruleset signing** — `HmacSha256RuleSetSigner` and `RsaRuleSetAuditSigner`; optional `RequireSignature` enforcement in the store
- **Audit trail** — `IRuleSetAuditStore` / `FileRuleSetAuditStore` with `RuleSetAuditEntry` records; RSA-signed entries via `RsaRuleSetAuditSigner`
- **Rule toggles** — `RuleOptions.RuleToggles` and `TenantRuleToggles` for per-rule and per-tenant feature flags evaluated by `RuleEngine<T>`
- **Execution tracing** — `IRuleExecutionTracer` + `RuleExecutionTracer` backed by `RedisRuleTraceStore`; PII field redaction via `ITraceRedactor`; per-tenant TTL overrides
- **Debugger mode** — `IRuleDebuggerModeService` with Redis-backed on/off flag; `RuleTracingEndpoints` for HTTP management
- **Maker-checker + canary** — `RequireApproval` and `EnableCanary` in `RuleControlPlaneOptions`; `PercentageRuleActivationStrategy` for gradual rollout
- **OTel telemetry** — `RuntimeTelemetryDescriptor` exposes cache hit/miss counters, eviction counter, and hot-reload lag histogram

## Configuration

### `RuleStore` section (`RuleStoreConfigs`)

```json
{
  "RuleStore": {
    "RootPath": "rules",
    "UseContentRoot": true,
    "MaxRuleSetSizeBytes": 1048576,
    "RequireSignature": false,
    "AllowedPathSegmentPattern": "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$",
    "EnableRuntimeCache": true,
    "RuntimeCacheMinutes": 10,
    "RuleChangeChannel": "muonroi:ruleset:changed",
    "RequireApproval": false,
    "NotifyOnStateChange": true
  }
}
```

### `RuleControlPlane` section (`RuleControlPlaneOptions`)

```json
{
  "RuleControlPlane": {
    "RequireApproval": false,
    "NotifyOnStateChange": true,
    "EnableCanary": true,
    "AuditSignerKeyId": "ruleset-control-plane",
    "AuditPrivateKeyPemPath": "/run/secrets/ruleset-signing.pem"
  }
}
```

### `RuleTracing` section (`RuleTracingOptions`)

```json
{
  "RuleTracing": {
    "DefaultTtl": "24:00:00",
    "ScanPageSize": 200,
    "MaxQueryResults": 1000,
    "Database": 0,
    "TraceKeyPrefix": "rule-trace",
    "DebuggerKeyPrefix": "rule-debugger:enabled",
    "ModeCacheDuration": "00:00:10",
    "RedactedFieldNames": ["password", "token", "ssn", "creditCard"]
  }
}
```

### `RuleOptions` (per-rule toggles)

```csharp
builder.Services.Configure<RuleOptions>(options =>
{
    options.RuleToggles["FRAUD_CHECK"] = false;         // disable globally
    options.TenantRuleToggles["tenant-42"] = new Dictionary<string, bool>
    {
        ["HIGH_VALUE_ORDER"] = false                    // disable for one tenant
    };
});
```

## API Reference

| Type | Purpose |
|------|---------|
| `RuleEngineServiceCollectionExtensions` | DI entry point: `AddRuleEngineStore`, `AddMRuleEngineWithRedisHotReload`, `AddRuleEventBridge` / `AddRuleEngineEventBridge` |
| `RuleTracerServiceCollectionExtensions` | `AddRuleEngineTracing` — wires `IRuleExecutionTracer`, `ITraceRedactor`, `IRuleTraceStore`, `IRuleDebuggerModeService` |
| `IRuleSetStore` | Load, save, activate, archive rulesets |
| `IRuleSetAuditStore` | Persist and page through `RuleSetAuditEntry` records |
| `IRuleSetRuntimeCache` | In-memory cache with hot-invalidation |
| `IRuleSetChangeNotifier` | Publishes and subscribes to ruleset change signals |
| `RulesEngineService` | Main scoped service facade for rule set operations |
| `RuleEngine<T>` | `AddRule`, `RemoveRule`, `ExecuteAsync`, `GetCatalog` — runtime rule management |
| `RuleStoreConfigs` | Options for disk storage (section `RuleStore`) |
| `RuleControlPlaneOptions` | Control plane options: approval, canary, signing (section `RuleControlPlane`) |
| `RuleOptions` | Per-rule and per-tenant toggle maps (`RuleToggles`, `TenantRuleToggles`) |
| `RuleTracingOptions` | Tracing TTL, Redis database, PII redaction, per-tenant retention (section `RuleTracing`) |
| `FileRuleSetStore` | File-system `IRuleSetStore` implementation |
| `FileRuleSetAuditStore` | File-system `IRuleSetAuditStore` implementation |
| `RedisRuleSetChangeNotifier` | Redis pub/sub `IRuleSetChangeNotifier` |
| `InMemoryRuleSetChangeNotifier` | In-process `IRuleSetChangeNotifier` (default when no Redis) |
| `RuleSetRuntimeCache` | `IRuleSetRuntimeCache` backed by `IMemoryCache` |
| `HmacSha256RuleSetSigner` | HMAC-SHA-256 `IRuleSetSigner` |
| `RsaRuleSetAuditSigner` | RSA `IRuleSetAuditSigner` |
| `CloudEventPublishingNotifier` | Decorator that forwards change events to `IEventSink` |
| `RuleExecutionEventPublisher` | Publishes rule execution outcomes as events |
| `IEventDrivenRuleEvaluator` | Evaluates rules in an event-driven pipeline; result via `EventEvaluationStatus` |
| `FeelRuleAdapter<TContext>` | Evaluates FEEL expressions; writes facts with `__node.{code}.{path}` scoping |
| `JavaScriptRuleAdapter<TContext>` | Evaluates JS rules via Jint |
| `LiquidRuleAdapter<TContext>` | Evaluates Liquid template rules via Scriban |
| `DecisionTableRuleAdapter<TContext>` | Delegates to `IDecisionTableExecutor` |
| `SubFlowRuleAdapter<TContext>` | Executes nested rule sub-flows with cycle detection |
| `ConnectorRuleAdapter<TContext>` | Bridges to external connector rules |
| `RuleGraphParser` | Parses flow graph JSON and produces topologically sorted `RuleGraphEntry` lists |
| `ContextAdaptedRule<TChild>` | Adapts a typed `IRule<TChild>` for execution against a `FactBagRuleContext` |
| `ReflectionContextFactory<TContext>` | `IContextFactory<TContext>` via reflection |
| `ReflectionContextProjector<TContext>` | `IContextProjector<TContext>` via reflection |
| `PercentageRuleActivationStrategy<T>` | `IRuleActivationStrategy<T>` for canary/percentage-based activation |
| `MRuleAuthoringManifestRegistry` | Discovers `IRuleAuthoringManifestProvider` implementations for rule schema publishing |
| `MRuleContextJsonRegistry` | Type-safe deserialization registry for rule context types |
| `RuntimeTelemetryDescriptor` | `ITelemetryDescriptor` exposing OTel cache and hot-reload metrics |

## Samples

- [MultiTenantSaaS](../../samples/MultiTenantSaaS/) — multi-tenant pricing API demonstrating `AddMRuleEngineWithRedisHotReload` for cross-node hot-reload
- [Quickstart.RuleEngine.Runtime.Web](../../samples/Quickstart.RuleEngine.Runtime.Web/) — full runtime governance web surface with REST endpoints and SignalR hub via `Muonroi.RuleEngine.Runtime.Web`

## Compatibility

- Target framework: `net8.0`
- Requires `Microsoft.AspNetCore.App` framework reference
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — contracts this package implements (`IRuleSetStore`, `IRuleSetSigner`, adapters)
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — rule orchestrator, workflow runner, and audit hooks
- [`Muonroi.RuleEngine.DecisionTable`](../Muonroi.RuleEngine.DecisionTable/) — decision table executor consumed by `DecisionTableRuleAdapter`
- [`Muonroi.RuleEngine.Runtime.Web`](../Muonroi.RuleEngine.Runtime.Web/) — REST + SignalR web surface built on top of this package

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
