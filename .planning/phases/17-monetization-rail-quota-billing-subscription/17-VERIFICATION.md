---
phase: 17
verified: 2026-06-21T10:05:00Z
status: passed
score: 8/8 MON requirements verified (SC1..SC5 all pass)
overrides_applied: 0
re_verification:
  previous_status: none
  note: initial verification
---

# Phase 17: Monetization Rail — Enforced Quota + Usage→Billing + Subscription — Verification Report

**Phase Goal:** Turn record-only metering + placeholder pricing into an enforced, billable cross-repo rail (hard quota enforcement at the control-plane API boundary, usage→priced line items + invoice-preview, an `IBillingProvider` seam with record-only default, subscription/renewal lifecycle in license-server) with ZERO change to the OSS engine (SC5).
**Verified:** 2026-06-21
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (per Success Criterion)

| # | Truth (SC) | Status | Evidence |
|---|-----------|--------|----------|
| SC1 | MON-05: tenant over tier limit gets HTTP 429 at control-plane boundary via registered `UseQuotaEnforcement()` + scoped→singleton tracker fix; OSS render path never blocked | ✓ VERIFIED | `Program.cs:356` `AddTenantQuotaManagement()`, `:365` `AddSingleton<ITenantQuotaTracker, InMemoryTenantQuotaTracker>()` (the documented scoped→singleton DI-lifetime fix), `:606` `app.UseQuotaEnforcement()`, `:703` `SeedDefaultTenantQuotaAsync`. Tests run in-process: `QuotaEnforcementTests.OverApiLimit_TenantScopedRequest_Returns429AtBoundary` (429 + Retry-After + `rate_limit_exceeded`), `NoTenantHeader_AnonymousSurface_NotBlockedByEnforcement` (no-tenant/OSS path), `FreshTenant_FirstRequest_NotBlocked` → 5/5 passed. Enforcement is API-layer middleware, NOT in `Muonroi.Pdf`. |
| SC2 | MON-06: usage aggregates to priced `UsageLineItem`s; invoice-preview returns computed amount (compute-only); `PricingEndpoints` placeholders replaced by PricingPlan-sourced values | ✓ VERIFIED | `InvoicePreviewEndpoints.cs:81` calls `IUsageAggregator.AggregateAsync` then `:83` `IBillingProvider.PreviewInvoiceAsync` (no `RecordAsync`/charge). `PricingEndpoints.cs:53/75/100` `price = FormatPrice(pricingPlanProvider.GetPlanForLicenseTier(...))` — `$20`/`Contact us` literals gone. `InvoicePreviewEndpointTests` passed (part of 5/5 above). |
| SC3 | MON-01/02: `Muonroi.Billing.Abstractions` has the 4 contracts; record-only default (No Silent Catch); no payment SDK at build/test; provider+aggregator DI-registered | ✓ VERIFIED | 4 contracts present: `IBillingProvider.cs`, `IUsageAggregator.cs`, `UsageLineItem.cs` (+`PricingPlan.cs`/`BillableEvent.cs`). `RecordOnlyBillingProvider.cs:54-64` catch logs via `_logger.Error(ex,...)` then swallows (No Silent Catch); `:75` PreviewInvoice compute-only. csproj has zero payment package (only DI.Abstractions + Core/Quota/Logging refs); `Stripe` appears only in 2 XML doc-comments. `BillingServiceCollectionExtensions.cs:26-27` `AddRecordOnlyBilling` TryAddSingleton wires BOTH `IBillingProvider` + `IUsageAggregator`. Billing suite 11/11 passed in-process. |
| SC4 | MON-07: license-server subscription fields + EF migration + renew endpoint + tier→limit endpoint; renewal re-signs via existing RSA machinery | ✓ VERIFIED | Migration `20260621012757_Phase17Subscription.cs` adds GraceHours/LastRenewedAt/RenewalCount. `KeyEndpoints.cs:113` `POST /{licenseKey}/renew`→`RenewAsync` (auth `license-generate`); `:138` `GET /tiers/{tier}/limits`→`TierQuotaLimits.For`. `SubscriptionService.cs:75-76` re-signs via existing `LicenseSigningService.BuildLicenseSigningData` + `signingService.SignAsync` (no forked crypto); `:56` rejects revoked; `:67-89` extends ExpiresAt. License-server suite 26/26 passed in-process. |
| SC5 | MON-08: OSS `Muonroi.Pdf` byte-identical; leak-guard exists+passes; no billing/quota leak; all 3 repos' affected suites green | ✓ VERIFIED | `git status --porcelain src/Muonroi.Pdf tests/Muonroi.Pdf.Tests/Golden` → EMPTY; no phase-17 commit touched `src/Muonroi.Pdf`. `OssBoundaryBillingLeakTests.cs` (3 tests incl. non-vacuous counter-assertion) → 3/3 passed in-process. `OSS-BOUNDARY.md:67` lists billing under OSS Packages, not Commercial. Affected suites: building-block leak-guard 3/3 + billing 11/11; license-server 26/26; control-plane quota+invoice 5/5 — all run in my own process, 0 failures. |

**Score:** 8/8 MON requirements (5/5 success criteria) verified.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Muonroi.Billing.Abstractions/{IBillingProvider,IUsageAggregator,UsageLineItem,PricingPlan,BillableEvent}.cs` | 4+ contracts, OSS package | ✓ VERIFIED | All present; csproj net8.0, no payment SDK |
| `src/Muonroi.Billing.Abstractions/RecordOnlyBillingProvider.cs` | record-only default, No Silent Catch | ✓ VERIFIED | logs+swallows on sink failure; compute-only preview |
| `src/Muonroi.Billing.Abstractions/UsageAggregator.cs` | IUsageAggregator impl reading ITenantQuotaStore | ✓ VERIFIED | `GetUsageAsync` + `GetUnitRate` wired, deterministic ordering |
| `src/Muonroi.Quota.Abstractions/TenantQuota.cs` | tier-sourced finite MaxPdfRendersPerDay | ✓ VERIFIED | Free=50, Starter=500, Pro=5_000, Enterprise=int.MaxValue |
| `muonroi-control-plane/.../Program.cs` | UseQuotaEnforcement + DI + tracker fix | ✓ VERIFIED | lines 356/365/370/507/606/703 |
| `muonroi-control-plane/.../Endpoints/InvoicePreviewEndpoints.cs` | compute-only invoice-preview | ✓ VERIFIED | AggregateAsync→PreviewInvoiceAsync, no charge |
| `muonroi-control-plane/.../Services/Billing/ControlPlanePricingPlanProvider.cs` | real PricingPlan source | ✓ VERIFIED | per-tier rates + flat base; LicenseTier→TenantTier bridge |
| `muonroi-license-server/.../Services/SubscriptionService.cs` | renew + grace, reuse RSA | ✓ VERIFIED | re-sign via existing signer; reject revoked |
| `muonroi-license-server/.../TierQuotaLimits.cs` | tier→limit map | ✓ VERIFIED | For(LicenseTier); Free/Licensed finite, Enterprise Unlimited |
| `muonroi-license-server/.../Migrations/*_Phase17Subscription.cs` | EF migration for new columns | ✓ VERIFIED | AddColumn GraceHours/LastRenewedAt/RenewalCount |
| `tests/Muonroi.Pdf.Tests/Service/OssBoundaryBillingLeakTests.cs` | automated SC5 guard | ✓ VERIFIED | 3 tests, non-vacuous, passed |

### Key Link Verification

| From | To | Via | Status |
|------|-----|-----|--------|
| Program.cs | QuotaEnforcementMiddleware | `UseQuotaEnforcement()` + `AddTenantQuotaManagement()` | ✓ WIRED |
| InvoicePreviewEndpoints | IUsageAggregator.AggregateAsync | endpoint handler | ✓ WIRED |
| UsageAggregator | ITenantQuotaStore.GetUsageAsync | ctor-injected store read | ✓ WIRED |
| UsageAggregator | PricingPlan.GetUnitRate | per-dimension lookup | ✓ WIRED |
| RecordOnlyBillingProvider | IMLog | `catch{ logger.Error(...) }` | ✓ WIRED (No Silent Catch) |
| SubscriptionService | LicenseSigningService (RSA) | re-sign on renew | ✓ WIRED (no forked crypto) |
| KeyEndpoints | SubscriptionService.RenewAsync | minimal-API POST | ✓ WIRED |
| OssBoundaryBillingLeakTests | Muonroi.Pdf referenced assemblies | reflection over GetReferencedAssemblies | ✓ WIRED |

### Behavioral Spot-Checks / Probe Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| OSS boundary leak-guard (SC5/MON-08) | `dotnet test ...Muonroi.Pdf.Tests --filter OssBoundaryBillingLeakTests` | Passed: 3, Failed: 0 | ✓ PASS |
| Billing seam + pricing + tier caps (MON-01..04) | `dotnet test ...Muonroi.Billing.Abstractions.Tests` | Passed: 11, Failed: 0 | ✓ PASS |
| Subscription/renew/RSA re-sign + tier-limits (MON-07) | `dotnet test ...Muonroi.LicenseServer.Tests` | Passed: 26, Failed: 0 | ✓ PASS |
| 429 enforcement + compute-only invoice-preview (MON-05/06) | `dotnet test ...ControlPlane.Host.Tests --filter QuotaEnforcementTests\|InvoicePreviewEndpointTests` | Passed: 5, Failed: 0 | ✓ PASS |
| OSS byte-identical (SC5) | `git status --porcelain src/Muonroi.Pdf tests/Muonroi.Pdf.Tests/Golden` | EMPTY | ✓ PASS |

### Requirements Coverage

MON-01..MON-08 are not in `REQUIREMENTS.md` (defined only in ROADMAP/CONTEXT/PLAN frontmatter for this phase). Mapped to shipped code:

| Req | Maps to | Status |
|-----|---------|--------|
| MON-01 | Billing seam contracts (IBillingProvider/IUsageAggregator/UsageLineItem/PricingPlan) | ✓ SATISFIED |
| MON-02 | RecordOnlyBillingProvider + AddRecordOnlyBilling | ✓ SATISFIED |
| MON-03 | UsageAggregator impl (priced rollup) | ✓ SATISFIED |
| MON-04 | tier-sourced finite MaxPdfRendersPerDay | ✓ SATISFIED |
| MON-05 | UseQuotaEnforcement registered (429) | ✓ SATISFIED |
| MON-06 | invoice-preview + PricingPlan-sourced pricing | ✓ SATISFIED |
| MON-07 | subscription/renew lifecycle + tier→limit map | ✓ SATISFIED |
| MON-08 | OSS leak-guard + byte-identical | ✓ SATISFIED |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| (none) | No TBD/FIXME/XXX in billing source; no stub returns; no empty catch | — | clean |
| control-plane AccountServiceTests.cs | pre-existing CS8425 async-iterator warning | ℹ️ Info | NOT introduced by Phase 17; out of scope |

### Gaps Summary

None. All five success criteria are observably true in the shipped code and confirmed by tests executed in this verifier's own process (not relying on SUMMARY claims). The one-way Enterprise/billing→OSS boundary (SC5) is intact: `git status` for `src/Muonroi.Pdf` + Golden is empty, no phase-17 commit touched the OSS engine, and the reflection leak-guard (with a non-vacuous counter-assertion proving the quota seam lives Enterprise-side) passes. The Stripe/payment adapter is correctly deferred — present only as prose in XML doc-comments, zero build/test dependency.

---

_Verified: 2026-06-21T10:05:00Z_
_Verifier: Claude (gsd-verifier)_
