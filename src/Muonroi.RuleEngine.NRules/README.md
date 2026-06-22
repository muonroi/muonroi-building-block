# Muonroi.RuleEngine.NRules

> **[FROZEN]** NRules integration for the Muonroi rule-engine surface — no active development. Migrate to [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/).

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.NRules.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.NRules/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package wired the [NRules](https://nrules.net/) forward-chaining rule engine into the Muonroi building-block ecosystem. It provides a singleton `NRulesEngine` that scans assemblies for NRules `Rule` subclasses, respects per-rule enable/version configuration via `RuleOptions`, and optionally exposes an HTTP management surface through `NRulesController`.

**This package is frozen.** All public types carry `[Obsolete]`. For new projects use [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/), which supersedes it with the full Abstractions v1.7+ Saga-pattern execution model.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.NRules --prerelease
```

## Quick Start

The following is grounded in `samples/Quickstart.RuleEngine.NRules/src/Quickstart.RuleEngine.NRules.Api/Program.cs`.

### 1. Register the engine

```csharp
// Program.cs
builder.Services.AddNRulesEngine(
    configure: options => builder.Configuration.GetSection("NRules").Bind(options),
    assemblies: typeof(Program).Assembly);   // scan this assembly for Rule subclasses

builder.Services.AddNRulesWeb();             // optional: registers NRulesController as an MVC part

// NRulesController requires IMDateTimeService from Muonroi.Core.
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();
```

### 2. Define a rule

```csharp
using NRules.Fluent.Dsl;
using Muonroi.RuleEngine.NRules;

[Rule("HighValueOrderDiscount", "1.0")]
public sealed class HighValueOrderDiscountRule : Rule
{
    public override void Define()
    {
        Order order = null!;

        When()
            .Match(() => order, o => o.Amount > 1000m);

        Then()
            .Do(_ => order.ApplyDiscount(0.10m));
    }
}
```

### 3. Fire rules

```csharp
// Inject NRulesEngine and fire facts against it.
var order = new Order { Amount = 1500m };
nRulesEngine.Fire(order);
```

### 4. appsettings.json — enable/disable or pin a version

```json
{
  "NRules": {
    "Rules": {
      "HighValueOrderDiscount": {
        "Enabled": true,
        "Version": "1.0"
      }
    }
  }
}
```

## Features

- Assembly scanning — locates every `Rule` subclass in the supplied assemblies at startup and compiles them into a reusable `ISessionFactory`.
- Per-rule toggling — set `Enabled: false` in `RuleOptions.Rules` to exclude a rule without removing the class.
- Version pinning — `[Rule(name, version)]` + `RuleConfig.Version` lets you deploy multiple versions of a rule and activate only the one you need.
- Duplicate detection — throws `MInternalException` at startup if two enabled rules resolve to the same name, preventing silent conflicts.
- HTTP surface — `AddNRulesWeb()` registers `NRulesController` (under `api/v1/rule-engine/nrules`) as an MVC application part for runtime inspection.
- Manifest contributor — registers `NRulesManifestContributor` with the Muonroi UI-engine manifest pipeline.

## Configuration

`AddNRulesEngine` accepts an `Action<RuleOptions>` configure callback and one or more `Assembly` arguments.

### RuleOptions

| Property | Type | Description |
|----------|------|-------------|
| `Rules` | `Dictionary<string, RuleConfig>` | Per-rule configuration keyed by rule name (case-insensitive). |

### RuleConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `true` | Set to `false` to exclude this rule from the compiled session factory. |
| `Version` | `string?` | `null` | If set, only the rule whose `[Rule(..., version)]` matches this value is compiled. |

## API Reference

| Type | Purpose |
|------|---------|
| `NRulesEngine` | Singleton rule executor. Scans assemblies, filters by `RuleOptions`, compiles via NRules `RuleCompiler`, and exposes `Fire(params object[] facts)`. |
| `RuleAttribute` | Class-level attribute `[Rule(name, version)]` that gives a rule a stable identity for `RuleOptions` configuration. |
| `RuleOptions` | Options root bound from configuration; holds the `Rules` dictionary. |
| `RuleConfig` | Per-rule settings: `Enabled` and `Version`. |
| `ServiceCollectionExtensions` | `AddNRulesEngine(configure, assemblies)` — registers `NRulesEngine` as singleton. `AddNRulesWeb()` — adds `NRulesController` as MVC application part. |

## Samples

- [Quickstart.RuleEngine.NRules](../../samples/Quickstart.RuleEngine.NRules/) — demonstrates `AddNRulesEngine` + `AddNRulesWeb` with a `HighValueOrderDiscountRule` fact-matching order discounts.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — **recommended replacement**; supersedes this package with full Saga-pattern and `ExecutionMode` support.
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — shared contracts consumed by both this package and `Muonroi.RuleEngine.Runtime`.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).

> **Deprecation notice**: this package is frozen and receives no new features or bug fixes. Migrate to `Muonroi.RuleEngine.Runtime`.
