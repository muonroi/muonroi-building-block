# Muonroi.Rules

> **Deprecated.** This package contains the legacy rule definition base classes and FEEL evaluator. Use [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) for all new work.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Rules.svg)](https://www.nuget.org/packages/Muonroi.Rules/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package was the original home of the Muonroi rule execution stack: a file-backed ruleset store, an in-memory/Redis change notifier, an HMAC-SHA256 ruleset signer, a FEEL expression evaluator, and a composable `IBusinessRule<TContext>` contract. All types carry an `[Obsolete]` attribute pointing to the successor package. The package is retained only for backward compatibility and will be removed in a future release.

## Installation

```bash
dotnet add package Muonroi.Rules --prerelease
```

> Prefer the replacement: `dotnet add package Muonroi.RuleEngine.Runtime --prerelease`

## Quick Start

The primary DI entry point is `AddRuleEngineStore`. All types are marked obsolete; the compiler will emit a warning on use.

```csharp
// Program.cs (legacy path — prefer Muonroi.RuleEngine.Runtime)
#pragma warning disable CS0618

builder.Services.AddRuleEngineStore(builder.Configuration);

// Optional: expose the FEEL playground controller and UI manifest contributor
builder.Services.AddFeelWeb();

#pragma warning restore CS0618
```

`appsettings.json` section consumed by `RuleStoreConfigs`:

```json
{
  "RuleStore": {
    "RootPath": "rules",
    "UseContentRoot": true,
    "MaxRuleSetSizeBytes": 1048576,
    "RequireSignature": false,
    "EnableRuntimeCache": true,
    "RuntimeCacheMinutes": 10,
    "RuleChangeChannel": "muonroi:ruleset:changed"
  }
}
```

Implementing the composable rule contract:

```csharp
#pragma warning disable CS0618
public class MinimumAgeRule : IBusinessRule<OrderContext>
{
    public string Code => "MIN_AGE";

    public Task<bool> IsSatisfiedAsync(OrderContext context, CancellationToken ct = default)
        => Task.FromResult(context.CustomerAge >= 18);
}
#pragma warning restore CS0618
```

Evaluating a FEEL expression directly:

```csharp
#pragma warning disable CS0618
bool result = FeelEvaluator.Evaluate(
    "amount >= 100 and status in ('active', 'pending')",
    new Dictionary<string, object> { ["amount"] = 150.0, ["status"] = "active" });
#pragma warning restore CS0618
```

## Features

- `IBusinessRule<TContext>` — composable business rule contract with a string `Code` and async `IsSatisfiedAsync`
- `RuleEngine<T>` — runs registered `IBusinessRule<T>` implementations with per-rule toggle support via `RuleOptions`
- `RulesEngineService` — facade over the `RulesEngine` library for JSON-defined workflow rules
- `IRuleSetStore` / `FileRuleSetStore` — file-backed versioned ruleset persistence with path-traversal protection
- `IRuleSetRuntimeCache` / `RuleSetRuntimeCache` — in-memory hot cache invalidated by `IRuleSetChangeNotifier`
- `IRuleSetChangeNotifier` — auto-selects Redis pub/sub (`RedisRuleSetChangeNotifier`) or in-process (`InMemoryRuleSetChangeNotifier`) based on DI registration
- `IRuleSetSigner` / `HmacSha256RuleSetSigner` — optional HMAC-SHA256 artifact signing and verification
- `FeelEvaluator` — evaluates FEEL (Friendly Enough Expression Language) boolean and value expressions
- `FeelStandardLibrary` — built-in FEEL functions (string, date, list, math)
- `DecisionTableImporter` — imports DMN-style decision tables
- `FeatureFlagEvaluator` — tenant-scoped feature flag evaluation
- `RuleOptions` — per-rule and per-tenant toggle dictionaries (case-insensitive)
- `AddFeelWeb()` — registers the FEEL playground controller and `IUiEngineManifestContributor`

## Configuration

### DI Registration

```csharp
// Registers FileRuleSetStore, RulesEngineService, runtime cache, and change notifier.
// Reads the "RuleStore" configuration section.
services.AddRuleEngineStore(configuration);

// Optional: adds FeelController + FeelPlaygroundManifestContributor
services.AddFeelWeb();
```

Redis change notifications activate automatically when `IConnectionMultiplexer` is already registered in DI. Otherwise the in-memory notifier is used.

### `RuleStoreConfigs` (`"RuleStore"` section)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `RootPath` | `string?` | `"rules"` subfolder | Folder containing ruleset files. Relative paths resolve against content root or `AppContext.BaseDirectory`. |
| `UseContentRoot` | `bool` | `true` | When `true`, resolves relative `RootPath` against `IHostEnvironment.ContentRootPath`. |
| `MaxRuleSetSizeBytes` | `int` | `1048576` | Maximum allowed size for a single ruleset artifact (bytes). |
| `RequireSignature` | `bool` | `false` | Reject unsigned ruleset artifacts when `true`. |
| `AllowedPathSegmentPattern` | `string` | `^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$` | Regex guard against path traversal in tenant/workflow segments. |
| `EnableRuntimeCache` | `bool` | `true` | Cache loaded ruleset payloads in memory. |
| `RuntimeCacheMinutes` | `int` | `10` | Absolute cache expiration in minutes. |
| `RuleChangeChannel` | `string` | `"muonroi:ruleset:changed"` | Redis pub/sub channel for cross-node cache invalidation. |

### `RuleOptions`

Inject `RuleOptions` to toggle individual rules or apply per-tenant overrides:

```json
{
  "RuleOptions": {
    "RuleToggles": { "MIN_AGE": true, "CREDIT_CHECK": false },
    "TenantRuleToggles": {
      "tenant-xyz": { "CREDIT_CHECK": true }
    }
  }
}
```

## API Reference

| Type | Purpose |
|------|---------|
| `IBusinessRule<TContext>` | Composable rule contract: `Code` + `IsSatisfiedAsync` |
| `RuleEngine<T>` | Runs `IBusinessRule<T>` implementations; respects `RuleOptions` toggles |
| `RuleOptions` | `RuleToggles` and `TenantRuleToggles` dictionaries for per-rule enable/disable |
| `RulesEngineService` | Executes JSON-defined workflow rules via the `RulesEngine` library |
| `IRuleSetStore` | Save, get, version, and activate ruleset JSON artifacts |
| `FileRuleSetStore` | File-backed `IRuleSetStore` implementation |
| `IRuleSetRuntimeCache` | In-memory ruleset cache contract |
| `IRuleSetChangeNotifier` | Publishes and subscribes to ruleset change events |
| `RedisRuleSetChangeNotifier` | Redis pub/sub change notifier (auto-selected when Redis is registered) |
| `InMemoryRuleSetChangeNotifier` | In-process fallback change notifier |
| `IRuleSetSigner` / `HmacSha256RuleSetSigner` | Optional HMAC-SHA256 ruleset signing and verification |
| `RuleStoreConfigs` | Strongly typed options for the `"RuleStore"` configuration section |
| `FeelEvaluator` | `Evaluate(expr, vars) → bool` and `EvaluateValue(expr, vars) → object?` |
| `FeelStandardLibrary` | Built-in FEEL standard functions |
| `DecisionTableImporter` | Imports DMN-style decision tables |
| `FeatureFlagEvaluator` | Tenant-scoped feature flag evaluation |
| `RuleEngineServiceCollectionExtensions` | `AddRuleEngineStore(IServiceCollection, IConfiguration)` |
| `FeelWebExtensions` | `AddFeelWeb(IServiceCollection)` |

## Samples

No dedicated sample exists for this package. Refer to the samples for the replacement package:

- [`RuleEngineSample`](../../samples/RuleEngineSample/) — demonstrates the current `Muonroi.RuleEngine.Runtime` API

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)
- **Status: Deprecated** — all public types carry `[Obsolete]`. Migrate to `Muonroi.RuleEngine.Runtime`.

## Related Packages

- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — **successor package**: full rule execution stack with EF-backed store, canary rollout, approval workflow, OTel telemetry, and RSA signing
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — shared contracts (`IRule<TContext>`, `FactBag`, `RuleResult`, `IMRuleOrchestrator`)
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — orchestrator, workflow runner, audit hooks
- [`Muonroi.RuleEngine.SourceGenerators`](../Muonroi.RuleEngine.SourceGenerators/) — source generator for rule extraction and DI registration

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) at the repository root.
