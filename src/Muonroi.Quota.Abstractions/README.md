# Muonroi.Quota.Abstractions

> Quota tracking contracts and in-memory implementations for Muonroi multi-tenant applications.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Quota.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Quota.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package defines the core quota management contracts (`ITenantQuotaTracker`, `ITenantQuotaStore`) and ships ready-to-use in-memory implementations for development and testing. It also provides `TenantQuota` limit models, tier-based presets (Free → Enterprise), and the `QuotaType` enum that enumerates every tracked resource dimension. Higher-tier packages (e.g. `Muonroi.Tenancy.SiteProfile.Web`) consume these contracts to enforce per-tenant limits at the middleware or behavior layer.

## Installation

```bash
dotnet add package Muonroi.Quota.Abstractions --prerelease
```

## Quick Start

Register the in-memory quota store and tracker, then inject `ITenantQuotaTracker` wherever enforcement is needed:

```csharp
// Program.cs
builder.Services.AddTenantQuotaManagement();   // registers InMemoryTenantQuotaStore (singleton)
                                                // and InMemoryTenantQuotaTracker (scoped)

// Apply a tier preset for a known tenant
var store = app.Services.GetRequiredService<ITenantQuotaStore>();
await store.SaveQuotaAsync("tenant-123", TenantQuotaPresets.Starter);

// In a request handler or background service
public class RuleExecutionService(ITenantQuotaTracker quota)
{
    public async Task RunAsync(string tenantId, CancellationToken ct)
    {
        bool allowed = await quota.CheckQuotaAsync(
            tenantId, QuotaType.RuleExecutionsPerDay, amount: 1, ct);

        if (!allowed)
            throw new QuotaExceededException($"Tenant {tenantId} has exceeded daily rule executions.");

        // ... execute rule ...
        await quota.IncrementUsageAsync(tenantId, QuotaType.RuleExecutionsPerDay, amount: 1, ct);
    }
}
```

## Features

- `ITenantQuotaTracker` — check quota availability and record usage in two separate steps, keeping enforcement logic decoupled from storage.
- `ITenantQuotaStore` — read/write `TenantQuota` limits and `QuotaUsage` snapshots; reset daily counters.
- `InMemoryTenantQuotaTracker` / `InMemoryTenantQuotaStore` — thread-safe volatile implementations; suitable for development, testing, and single-node deployments.
- `TenantQuotaPresets` — static factory properties (`Free`, `Starter`, `Professional`, `Enterprise`) with calibrated defaults for every quota dimension including `MaxPdfRendersPerDay`.
- `QuotaType` enum — 14 resource dimensions: `RuleExecutionsPerDay`, `ConcurrentExecutions`, `ApiRequestsPerMinute`, `RuleEvaluationsPerSecond`, `WorkflowExecutionsPerHour`, `StorageUsageMB`, `TotalRules`, `TotalDecisionTables`, `TotalWorkflows`, `TotalConnectors`, `ConnectorExecutionsPerDay`, `MessagesPerDay`, `MessagesPerMinute`, `PdfRendersPerDay`.
- `QuotaExceededException` — domain exception (HTTP 429) thrown when a limit is breached; extends `MException` with code `QUOTA_EXCEEDED`.
- `TenantTier` enum — `Free`, `Starter`, `Professional`, `Enterprise`.
- `AddTenantQuotaManagement()` extension — single-call DI registration using `TryAdd*` (safe to call multiple times).

## Configuration

```csharp
// Minimal registration — in-memory defaults
services.AddTenantQuotaManagement();

// Override the store with a persistent implementation (e.g. Redis, SQL)
// by registering before calling AddTenantQuotaManagement, or replacing afterwards:
services.AddSingleton<ITenantQuotaStore, MyRedisQuotaStore>();
services.AddTenantQuotaManagement();   // TryAddSingleton is a no-op when already registered
```

Quota limits are stored as a `TenantQuota` object per tenant. Seed limits at startup using `ITenantQuotaStore.SaveQuotaAsync` or use a preset:

| Preset | `MaxRuleExecutionsPerDay` | `MaxPdfRendersPerDay` | Notes |
|--------|---------------------------|-----------------------|-------|
| `Free` | 1 000 | 50 | Default when no quota is found |
| `Starter` | 10 000 | 500 | |
| `Professional` | 100 000 | 5 000 | |
| `Enterprise` | `int.MaxValue` | `int.MaxValue` | Unlimited |

## API Reference

| Type | Purpose |
|------|---------|
| `ITenantQuotaTracker` | Check and increment per-tenant usage; reset daily counters |
| `ITenantQuotaStore` | Persist `TenantQuota` limits and read `QuotaUsage` snapshots |
| `InMemoryTenantQuotaTracker` | Scoped in-memory tracker; delegates storage to `ITenantQuotaStore` |
| `InMemoryTenantQuotaStore` | Singleton volatile store backed by a `ConcurrentDictionary` |
| `TenantQuota` | Limit model — one property per `QuotaType` dimension |
| `TenantQuotaPresets` | Static tier presets (`Free`, `Starter`, `Professional`, `Enterprise`) |
| `QuotaType` | Enum of 14 trackable resource dimensions |
| `TenantTier` | Enum of 4 subscription tiers |
| `QuotaUsage` | Snapshot of `CurrentUsage` and `Limits` dictionaries keyed by `QuotaType` |
| `QuotaExceededException` | HTTP-429 domain exception (code `QUOTA_EXCEEDED`) |
| `TenantQuotaServiceCollectionExtensions` | `AddTenantQuotaManagement()` DI helper |

## Samples

No dedicated sample exists for this package. The integration-test suite at [`samples/TestProject.Service.IntegrationTests/SiteQuotaEnforcementTests.cs`](../../samples/TestProject.Service.IntegrationTests/SiteQuotaEnforcementTests.cs) demonstrates implementing a custom `ITenantQuotaTracker` and exercising per-tenant quota enforcement.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — base `MException` used by `QuotaExceededException`

## License

Licensed under the [Apache License 2.0](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE).
