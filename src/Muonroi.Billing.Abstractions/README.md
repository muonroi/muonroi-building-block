# Muonroi.Billing.Abstractions

> Product-agnostic billing seam contracts for Muonroi multi-tenant applications — no payment-SDK dependency required.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Billing.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Billing.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package defines the shared billing contracts that every Muonroi product line (PDF, rule-engine, storyflow) bills through using the same metered-dimension key (`QuotaType`). It ships a record-only default provider and a default usage aggregator that perform no external calls; a payment-processor adapter (e.g. Stripe) is a deferred, separate implementation wired behind the `IBillingProvider` seam. There is no runtime behavior in this package beyond the default providers — consuming packages depend only on the contracts and override them at host registration time.

## Installation

```bash
dotnet add package Muonroi.Billing.Abstractions --prerelease
```

## Quick Start

### Register the default record-only rail

```csharp
// Program.cs (or service registration in your host)
using Muonroi.Billing.Abstractions;

// Wires IBillingProvider → RecordOnlyBillingProvider
// and   IUsageAggregator → UsageAggregator (TryAddSingleton — overridable).
// Prerequisite: ITenantQuotaStore must already be registered by the host.
builder.Services.AddRecordOnlyBilling();
```

### Record a billable event

```csharp
public class PdfRenderService(IBillingProvider billing)
{
    public async Task RenderAsync(string tenantId, CancellationToken ct)
    {
        // ... render logic ...

        await billing.RecordAsync(new BillableEvent(
            TenantId: tenantId,
            Dimension: QuotaType.PdfPages,
            Quantity: 1,
            OccurredAt: DateTimeOffset.UtcNow), ct);
    }
}
```

### Aggregate usage into a priced invoice preview

```csharp
public class InvoiceService(IUsageAggregator aggregator)
{
    public async Task<IReadOnlyList<UsageLineItem>> PreviewAsync(
        string tenantId, CancellationToken ct)
    {
        var plan = new PricingPlan(
            tier: TenantTier.Pro,
            unitRates: new Dictionary<QuotaType, decimal>
            {
                [QuotaType.PdfPages] = 0.01m,
            },
            flatBaseAmount: 9.99m);

        // Compute-only — no external call, no charge.
        return await aggregator.AggregateAsync(
            tenantId, plan,
            periodStart: DateTime.UtcNow.AddMonths(-1),
            periodEnd: DateTime.UtcNow, ct);
    }
}
```

### Implement a custom billing provider (e.g. Stripe adapter)

Register your adapter **before** calling `AddRecordOnlyBilling`; `TryAddSingleton` ensures the default does not overwrite it:

```csharp
builder.Services.AddSingleton<IBillingProvider, StripeAdapter>();
builder.Services.AddRecordOnlyBilling(); // TryAdd — StripeAdapter wins
```

## Features

- `IBillingProvider` — fire-and-forget `RecordAsync` that never throws to the caller; compute-only `PreviewInvoiceAsync` that never charges
- `IUsageAggregator` — rolls per-tenant metered usage into deterministically priced `UsageLineItem`s via a `PricingPlan`
- `BillableEvent` — immutable record keyed on `QuotaType` (dimension), tenant ID, quantity, and timestamp
- `UsageLineItem` — priced rollup record with a `Create` factory that computes `Amount = Quantity * UnitRate`
- `PricingPlan` — tier-scoped rate table (`QuotaType → decimal`) plus an optional flat base amount; missing dimensions price at `0m`
- `RecordOnlyBillingProvider` — thread-safe in-memory default; sink failures are logged and swallowed (never blocks callers); exposes `RecordedEvents` for test assertions
- `UsageAggregator` — default aggregator; reads `ITenantQuotaStore`, emits per-dimension lines in deterministic `QuotaType` enum order, appends the flat-base line last
- `AddRecordOnlyBilling` DI extension — wires both defaults via `TryAddSingleton` so a real payment adapter registered first takes precedence

## API Reference

| Type | Purpose |
|------|---------|
| `IBillingProvider` | Primary billing seam: `RecordAsync` + `PreviewInvoiceAsync` |
| `IUsageAggregator` | Rolls metered usage into priced line items via a `PricingPlan` |
| `BillableEvent` | Immutable record for a single metered occurrence |
| `UsageLineItem` | Priced rollup for one dimension; `Create` factory computes amount |
| `UsageLineItem.FlatBaseDescription` | Canonical constant identifying the flat-base line item |
| `PricingPlan` | Rate table keyed by `QuotaType` + optional flat base; `GetUnitRate` returns `0m` for unpriced dimensions |
| `RecordOnlyBillingProvider` | Default `IBillingProvider`; in-memory, non-blocking, testable via `RecordedEvents` |
| `UsageAggregator` | Default `IUsageAggregator`; depends on `ITenantQuotaStore` |
| `BillingServiceCollectionExtensions.AddRecordOnlyBilling` | Registers both defaults via `TryAddSingleton` |

## Configuration

`AddRecordOnlyBilling` has no options class. The only host prerequisite is that `ITenantQuotaStore` is registered before `UsageAggregator` is resolved (the aggregator reads it to obtain per-tenant metered usage).

To override either seam, register your implementation before calling `AddRecordOnlyBilling`:

```csharp
// Override the provider only
services.AddSingleton<IBillingProvider, MyPaymentAdapter>();
services.AddRecordOnlyBilling(); // UsageAggregator still wired; IBillingProvider not overwritten
```

## Samples

No dedicated sample exists for this package yet. The record-only rail is wired inside the monetization host as part of Phase 17 — see `Muonroi.Quota.Abstractions` for the `ITenantQuotaStore` contract that `UsageAggregator` depends on.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Quota.Abstractions`](../Muonroi.Quota.Abstractions/) — provides `ITenantQuotaStore` and `QuotaType`; required by `UsageAggregator`
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides guard utilities (`MGuard`) used internally
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — provides `IMLog<T>` used by `RecordOnlyBillingProvider` for non-silent error logging

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
