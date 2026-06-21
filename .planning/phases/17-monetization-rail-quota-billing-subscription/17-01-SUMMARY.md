---
phase: 17-monetization-rail-quota-billing-subscription
plan: 01
subsystem: billing
tags: [billing, monetization, oss, seam, abstractions]
requires: [Muonroi.Quota.Abstractions, Muonroi.Logging.Abstractions, Muonroi.Core.Abstractions]
provides:
  - "Muonroi.Billing.Abstractions (OSS package)"
  - "IBillingProvider seam (record + invoice-preview)"
  - "IUsageAggregator contract (impl in 17-02)"
  - "RecordOnlyBillingProvider default impl (MON-02)"
  - "BillingServiceCollectionExtensions.AddRecordOnlyBilling DI"
affects: [17-02, 17-03]
tech-stack:
  added: []
  patterns: [record-only default + No Silent Catch, TryAddSingleton seam override, QuotaType-keyed dimension]
key-files:
  created:
    - src/Muonroi.Billing.Abstractions/Muonroi.Billing.Abstractions.csproj
    - src/Muonroi.Billing.Abstractions/GlobalUsings.cs
    - src/Muonroi.Billing.Abstractions/IBillingProvider.cs
    - src/Muonroi.Billing.Abstractions/BillableEvent.cs
    - src/Muonroi.Billing.Abstractions/UsageLineItem.cs
    - src/Muonroi.Billing.Abstractions/PricingPlan.cs
    - src/Muonroi.Billing.Abstractions/IUsageAggregator.cs
    - src/Muonroi.Billing.Abstractions/RecordOnlyBillingProvider.cs
    - src/Muonroi.Billing.Abstractions/BillingServiceCollectionExtensions.cs
    - tests/Muonroi.Billing.Abstractions.Tests/Muonroi.Billing.Abstractions.Tests.csproj
    - tests/Muonroi.Billing.Abstractions.Tests/GlobalUsings.cs
    - tests/Muonroi.Billing.Abstractions.Tests/RecordOnlyBillingProviderTests.cs
  modified:
    - Muonroi.BuildingBlock.sln
    - OSS-BOUNDARY.md
decisions: [D-02, D-03, D-05]
metrics:
  duration: ~15m
  completed: 2026-06-21
---

# Phase 17 Plan 01: Billing Seam (Muonroi.Billing.Abstractions) Summary

Product-agnostic billing seam shipped as new OSS package `Muonroi.Billing.Abstractions`: `IBillingProvider`/`IUsageAggregator`/`BillableEvent`/`UsageLineItem`/`PricingPlan` contracts plus a record-only default provider — keyed on `QuotaType`, zero payment-SDK dependency (D-02).

## What Was Built

- **Task 1 (MON-01):** New net8.0 OSS package mirroring `Muonroi.Quota.Abstractions` csproj shape (GeneratePackageOnBuild, GenerateDocumentationFile, PackageId, AssemblyVersion 1.0.0.0). ProjectReferences: `Muonroi.Core.Abstractions`, `Muonroi.Quota.Abstractions` (for `QuotaType`), `Muonroi.Logging.Abstractions` (for `IMLog<T>`). NO payment SDK. Added to `Muonroi.BuildingBlock.sln` and listed under OSS Packages in `OSS-BOUNDARY.md` (not Commercial).
- **Task 2 (MON-02, TDD):** `RecordOnlyBillingProvider` records events in-memory, logs-then-swallows on sink failure (No Silent Catch, T-17-01), compute-only preview (D-03). DI via `AddRecordOnlyBilling` (TryAddSingleton). 3 xunit tests, all green.

## EXACT Public Contract Shapes Shipped (downstream 17-02 / 17-03 depend on these)

Namespace: `Muonroi.Billing.Abstractions`

```csharp
// BillableEvent.cs — sealed record, QuotaType-keyed (product-agnostic, D-02)
public sealed record BillableEvent(
    string TenantId,
    QuotaType Dimension,
    long Quantity,
    DateTimeOffset OccurredAt);

// UsageLineItem.cs — sealed record. Amount = Quantity * UnitRate (D-05)
public sealed record UsageLineItem(
    QuotaType Dimension,
    long Quantity,
    decimal UnitRate,
    decimal Amount,
    string? Description = null)
{
    public const string FlatBaseDescription = "Flat tier base";
    public static UsageLineItem Create(QuotaType dimension, long quantity, decimal unitRate, string? description = null);
}

// PricingPlan.cs — sealed class (D-05: per-unit x tier + optional flat base)
public sealed class PricingPlan
{
    public PricingPlan(TenantTier tier, IReadOnlyDictionary<QuotaType, decimal>? unitRates = null, decimal flatBaseAmount = 0m);
    public TenantTier Tier { get; }
    public decimal FlatBaseAmount { get; }
    public IReadOnlyDictionary<QuotaType, decimal> UnitRates { get; }
    public decimal GetUnitRate(QuotaType dimension); // returns configured rate or 0m when absent
}
// NOTE: TenantTier lives in Muonroi.Quota.Abstractions (Free, Starter, Professional, ...).

// IUsageAggregator.cs — interface (impl lands in 17-02)
public interface IUsageAggregator
{
    Task<IReadOnlyList<UsageLineItem>> AggregateAsync(
        string tenantId, PricingPlan plan, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default);
}

// IBillingProvider.cs — interface (seam)
public interface IBillingProvider
{
    Task RecordAsync(BillableEvent billableEvent, CancellationToken ct = default);
    Task<IReadOnlyList<UsageLineItem>> PreviewInvoiceAsync(
        string tenantId, IReadOnlyList<UsageLineItem> lineItems, CancellationToken ct = default);
}

// RecordOnlyBillingProvider.cs — sealed class : IBillingProvider (default impl, MON-02)
public sealed class RecordOnlyBillingProvider : IBillingProvider
{
    public RecordOnlyBillingProvider(
        IMLog<RecordOnlyBillingProvider>? logger = null,
        Action<BillableEvent>? sink = null);
    public IReadOnlyList<BillableEvent> RecordedEvents { get; } // observable snapshot
}
```

### EXACT DI extension method name

```csharp
// BillingServiceCollectionExtensions.cs
public static IServiceCollection AddRecordOnlyBilling(this IServiceCollection services);
// registers services.TryAddSingleton<IBillingProvider, RecordOnlyBillingProvider>()
```

## Decisions Made

- **D-02:** Seam keyed on `QuotaType` (never "pdf"); record-only default; no payment SDK at build/test time.
- **D-03:** `PreviewInvoiceAsync` compute-only — returns line items verbatim, no charge.
- **D-05:** `UsageLineItem.Amount = Quantity * UnitRate`; `PricingPlan` = per-dimension unit rate + optional flat base. No proration/tax/multi-currency.

## Deviations from Plan

**1. [Rule 3 - Blocking] Softened one XML-doc cref in IBillingProvider.cs**
- **Found during:** Task 1 build.
- **Issue:** `<see cref="RecordOnlyBillingProvider"/>` in the IBillingProvider doc-comment failed CS1574 (type defined in Task 2; TreatWarningsAsErrors promotes it to error).
- **Fix:** Changed that single forward reference to `<c>RecordOnlyBillingProvider</c>` (plain code text). All other crefs resolve.
- **Files modified:** src/Muonroi.Billing.Abstractions/IBillingProvider.cs
- **Commit:** fc505c31

**2. [Rule 3 - Blocking / clarification] Added `UsageLineItem.Create` factory + `FlatBaseDescription` const**
- **Issue:** The plan specifies `Amount = Quantity * UnitRate` as an invariant. A positional record alone cannot enforce it. Added a static `Create` factory that computes Amount, plus the `FlatBaseDescription` const the orchestrator flagged downstream plans need.
- **Files modified:** src/Muonroi.Billing.Abstractions/UsageLineItem.cs
- **Commit:** fc505c31
- The primary record constructor remains public (callers may pass an explicit Amount), so this is purely additive.

**3. [Design choice — Claude's Discretion per D-02] `RecordOnlyBillingProvider` sink delegate**
- Added an optional `Action<BillableEvent>? sink` ctor param so the record-only provider can drive a downstream recording side-effect AND so Test 2 can inject a throwing sink to prove the catch+log+swallow path. Defaults to no-op; no external dependency introduced.

## Test Results

`dotnet test tests/Muonroi.Billing.Abstractions.Tests/Muonroi.Billing.Abstractions.Tests.csproj -c Debug`
→ **Passed! Failed: 0, Passed: 3, Skipped: 0, Total: 3.** Build: 0 warnings, 0 errors (TreatWarningsAsErrors on → MSTD-compliant).

TDD gate: RED verified via compile failure (`CS0246 RecordOnlyBillingProvider could not be found`) before implementation; GREEN after.

## No Stripe / Payment SDK

Grep under `src/Muonroi.Billing.Abstractions` + tests: the only `Stripe` token is one prose mention in an XML doc-comment ("payment-processor (e.g. Stripe) adapter is a deferred..."). Zero `Stripe`/payment package or assembly reference. `OSS-BOUNDARY.md` lists `Muonroi.Billing.Abstractions` under OSS Packages only.

## Known Stubs

None. `IUsageAggregator` is an intentional contract-only interface; its implementation is explicitly scoped to plan 17-02.

## Threat Flags

None. No new security surface beyond the threat_model's registered items (T-17-01 mitigated by catch+log+swallow; T-17-02 mitigated — no payment SDK, PreviewInvoice compute-only).

## Self-Check: PASSED
- All 12 created files present on disk (verified below).
- Both commits present in git log: fc505c31, 8966bf03.
