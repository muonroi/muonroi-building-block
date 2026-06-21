---
phase: 17-monetization-rail-quota-billing-subscription
plan: 02
subsystem: billing
tags: [billing, monetization, usage-aggregation, quota, tier-limits, tdd]
requires: [Muonroi.Billing.Abstractions, Muonroi.Quota.Abstractions, Muonroi.Core.Abstractions]
provides:
  - "UsageAggregator (IUsageAggregator impl, MON-03)"
  - "AddRecordOnlyBilling wires BOTH IBillingProvider + IUsageAggregator"
  - "Tier-sourced finite MaxPdfRendersPerDay presets (MON-04)"
affects: [17-03]
tech-stack:
  added: []
  patterns: [deterministic per-dimension pricing, flat-base-line-by-Description, MGuard.NotNull (MSTD), TryAddSingleton seam]
key-files:
  created:
    - src/Muonroi.Billing.Abstractions/UsageAggregator.cs
    - tests/Muonroi.Billing.Abstractions.Tests/UsageAggregatorTests.cs
    - tests/Muonroi.Billing.Abstractions.Tests/TierQuotaLimitTests.cs
  modified:
    - src/Muonroi.Billing.Abstractions/BillingServiceCollectionExtensions.cs
    - src/Muonroi.Quota.Abstractions/TenantQuota.cs
decisions: [D-03, D-04, D-05]
metrics:
  duration: ~20m
  completed: 2026-06-21
---

# Phase 17 Plan 02: UsageAggregator + Tier-Sourced PDF Render Caps Summary

`UsageAggregator` deterministically prices a single tenant's metered usage (read via
`ITenantQuotaStore.GetUsageAsync`) into `UsageLineItem`s through a `PricingPlan` (MON-03/D-05),
and the four tier presets now carry a finite per-tier `MaxPdfRendersPerDay` for non-Enterprise
tiers (MON-04/D-04) instead of always `int.MaxValue`.

## What Was Built

- **Task 1 (MON-03, TDD):** `UsageAggregator : IUsageAggregator` in `Muonroi.Billing.Abstractions`.
  Constructor injects `ITenantQuotaStore`. `AggregateAsync` calls `GetUsageAsync(tenantId, ct)`,
  iterates `CurrentUsage` ordered by `(int)QuotaType` (deterministic), computes
  `unitRate = plan.GetUnitRate(dimension)` and `Amount = Quantity * UnitRate` per dimension
  (via `UsageLineItem.Create`), then appends the optional flat-base line LAST. 5 xunit tests, green.
- **Task 2 (MON-04, TDD):** Replaced `MaxPdfRendersPerDay = int.MaxValue` with finite per-tier caps
  for Free/Starter/Professional; Enterprise unchanged (unlimited). Rewrote the property XML doc to
  state it is now the enforced per-tier daily cap (Phase 17), dropping the "record-only Phase 16"
  wording. 3 xunit tests, green.

## Chosen finite MaxPdfRendersPerDay per tier

| Tier | MaxPdfRendersPerDay |
|------|---------------------|
| Free | 50 |
| Starter | 500 |
| Professional | 5_000 |
| Enterprise | int.MaxValue (unlimited) |

Monotonically increasing (Free < Starter < Professional), scaled consistently with the existing
per-tier 1:10:100 ramp used by other daily limits (e.g. `MaxConnectorExecutionsPerDay` 100/1000/10000).
Enterprise stays `int.MaxValue`, which the tracker's `GetLimit` short-circuits to "allowed".

## Final `AddRecordOnlyBilling` registration set

```csharp
public static IServiceCollection AddRecordOnlyBilling(this IServiceCollection services)
{
    services.TryAddSingleton<IBillingProvider, RecordOnlyBillingProvider>(); // 17-01
    services.TryAddSingleton<IUsageAggregator, UsageAggregator>();           // 17-02 (new)
    return services;
}
```

The existing extension method was **extended** (not duplicated): the SINGLE call
`AddRecordOnlyBilling()` now wires BOTH seams, so 17-03 calls only this one method and gets the
billing provider and the usage aggregator. Both use `TryAddSingleton` so a payment-processor adapter
or a custom aggregator can override either seam by registering its own impl first (D-02). The host
must register `ITenantQuotaStore` itself (the aggregator depends on it; this method does not register it).

## Flat-base line representation (Warning #3 pin)

`QuotaType` has no "flat base" member and must not change. The flat-base line is therefore emitted as:
`Dimension = default` (= `QuotaType.RuleExecutionsPerDay`, value 0 — semantically ignored for the base),
`Quantity = 0`, `UnitRate = 0m`, `Amount = plan.FlatBaseAmount`,
`Description = UsageLineItem.FlatBaseDescription` (the const `"Flat tier base"` shipped by 17-01).
Flat-base lines are identified by `Description`, never by `Dimension`. The line is appended LAST and
omitted entirely when `FlatBaseAmount <= 0`.

## Documented deterministic choice — 0-rate dimensions

Dimensions present in `CurrentUsage` but unpriced by the plan (`GetUnitRate` returns `0m`) produce a
0-amount line item (not dropped). Test 1 asserts this explicitly (`ApiRequestsPerMinute`: Quantity=50,
UnitRate=0, Amount=0). This keeps the line set a faithful mirror of metered usage for the
invoice-preview surface (17-03).

## Decisions Made

- **D-03:** Aggregation is compute-only — reads usage and produces line items; no payment call, no charge.
- **D-04:** `MaxPdfRendersPerDay` sourced from the licensed tier; finite for non-Enterprise.
- **D-05:** `Amount = Quantity * UnitRate`; total = Σ per-dimension + optional flat base. No proration/tax/multi-currency.

## Deviations from Plan

**1. [Rule 3 - Blocking] MSTD analyzer forbids raw `ArgumentNullException`**
- **Found during:** Task 1 GREEN build (MSTD0001: "Throw via MGuard or an MException-derived type; raw 'ArgumentNullException' is forbidden").
- **Fix:** Replaced both guards (ctor `quotaStore`, `AggregateAsync` `plan`) with `MGuard.NotNull(...)` from `Muonroi.Core.Abstractions.Guards` (already a project reference). `MGuard.NotNull` returns the value, so the ctor assignment stayed a one-liner.
- **Files modified:** src/Muonroi.Billing.Abstractions/UsageAggregator.cs
- **Commit:** 9ddde1dc

No other deviations — both tasks executed as written.

## Threat Model Compliance

- **T-17-03 (Information Disclosure):** `AggregateAsync` reads usage strictly via `GetUsageAsync(tenantId)`; never enumerates other tenants. Covered by `AggregateAsync_reads_only_the_supplied_tenant` (the stub store records every queried tenant id and asserts it equals only the supplied id).
- **T-17-04 (Tampering):** Only preset numbers + one property XML-doc changed in `TenantQuota.cs`. `QuotaType.cs`, the tracker `GetLimit` switch, and the `QuotaExceededException(429)` path are untouched. No file under `src/Muonroi.Pdf` (OSS engine) was modified.

## Verification

- `dotnet test tests/Muonroi.Billing.Abstractions.Tests/Muonroi.Billing.Abstractions.Tests.csproj -c Debug`
  → **Passed! Failed: 0, Passed: 11, Skipped: 0, Total: 11.** (3 RecordOnly + 5 UsageAggregator + 3 TierQuotaLimit). Build: 0 warnings, 0 errors (TreatWarningsAsErrors on → MSTD-compliant).
- TDD gates: RED verified for both tasks before implementation — Task 1 via CS0246 (`UsageAggregator` not found); Task 2 via 2 FluentAssertions failures (Free not finite / not monotonic). GREEN after each impl.
- `git show --name-only` per commit confirms only the 5 planned files were touched; no `src/Muonroi.Pdf` or `QuotaType.cs` edits.

## Known Stubs

None.

## Threat Flags

None. No new security surface beyond the registered threat_model items (T-17-03, T-17-04), both mitigated.

## Commits

- `5e6da9fe` — test(17-02): RED UsageAggregator pricing tests (MON-03)
- `9ddde1dc` — feat(17-02): UsageAggregator impl + AddRecordOnlyBilling wires IUsageAggregator (MON-03)
- `88701232` — test(17-02): RED tier-sourced MaxPdfRendersPerDay tests (MON-04)
- `1980afe5` — feat(17-02): tier-sourced finite MaxPdfRendersPerDay presets (MON-04)

## Self-Check: PASSED
