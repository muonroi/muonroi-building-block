---
phase: 17-monetization-rail-quota-billing-subscription
plan: 03
subsystem: control-plane
tags: [monetization, quota-enforcement, billing, invoice-preview, pricing, MON-05, MON-06, cross-repo]
requires:
  - "Muonroi.Quota.Abstractions (AddTenantQuotaManagement, QuotaEnforcementMiddleware)"
  - "Muonroi.Billing.Abstractions (AddRecordOnlyBilling, IUsageAggregator, IBillingProvider, PricingPlan)"
  - "license-server tier->limit mapping (17-04, consumed via LicenseTier->TenantTier bridge)"
provides:
  - "control-plane host registers app.UseQuotaEnforcement() (MON-05 / D-01)"
  - "GET /api/v1/billing/invoice-preview (compute-only, MON-06 / D-03)"
  - "ControlPlanePricingPlanProvider (real PricingPlan replacing placeholder prices, MON-06 / D-05)"
  - "default-tenant quota seeded from licensed tier at startup"
affects: [17-05]
tech-stack:
  added: []
  patterns:
    - "scoped->singleton tracker override to satisfy ctor-injected middleware resolved from root provider"
    - "tenant resolved from ISystemExecutionContextAccessor, never client-supplied (T-17-21)"
    - "tier-sourced default-tenant quota seeding (replaces hard-coded Free fallback)"
key-files:
  created:
    - "muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/Billing/ControlPlanePricingPlanProvider.cs"
    - "muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Endpoints/InvoicePreviewEndpoints.cs"
    - "muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/QuotaEnforcementTests.cs"
    - "muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/InvoicePreviewEndpointTests.cs"
  modified:
    - "muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Muonroi.ControlPlane.Host.csproj"
    - "muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Program.cs"
    - "muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Endpoints/PricingEndpoints.cs"
decisions: [D-01, D-03, D-05]
metrics:
  duration: "~12m"
  completed: "2026-06-21"
  tasks: 3
  files: 7
requirements: [MON-05, MON-06]
---

# Phase 17 Plan 03: Wire Billing Rail into Control-Plane Host Summary

The control-plane host now enforces the tenant API quota at the API boundary (`app.UseQuotaEnforcement()`, MON-05/D-01 — a tenant over its tier `MaxApiRequestsPerMinute` gets HTTP 429), exposes a compute-only `GET /api/v1/billing/invoice-preview` backed by `IUsageAggregator` + a real `PricingPlan` (MON-06/D-03), and sources `PricingEndpoints` prices from `ControlPlanePricingPlanProvider` instead of placeholder strings (D-05). The OSS render path is never touched (SC5).

## What Was Built (per task)

### Task 1 — Quota enforcement + billing rail DI + PricingPlan provider (commit 8490ddc)
- Added direct host `ProjectReference`s to `Muonroi.Quota.Abstractions` + `Muonroi.Billing.Abstractions`.
- `Program.cs` DI: `AddTenantQuotaManagement()` + `AddRecordOnlyBilling()` (which wires BOTH `IBillingProvider` and `IUsageAggregator` per 17-02) + `AddSingleton<ControlPlanePricingPlanProvider>()`.
- **DI-lifetime fix (the blocker):** immediately after `AddTenantQuotaManagement()`, `builder.Services.AddSingleton<ITenantQuotaTracker, InMemoryTenantQuotaTracker>();`. `AddTenantQuotaManagement` registers the tracker as `TryAddScoped`, but `QuotaEnforcementMiddleware` takes `ITenantQuotaTracker` as a constructor param, so `UseMiddleware<T>` resolves it from the ROOT provider → `InvalidOperationException` at the first request (not caught by build). The tracker is stateless (delegates to the singleton store) so a singleton lifetime is correct. The Task-3 429 test exercises this real DI path. The middleware itself was NOT modified.
- `ControlPlanePricingPlanProvider`: per-tier `UnitRates` for `QuotaType.PdfRendersPerDay` + `QuotaType.ApiRequestsPerMinute` plus `FlatBaseAmount`, with a documented `LicenseTier -> TenantTier` bridge (`MapLicenseTier`/`GetPlanForLicenseTier`).

### Task 2 — Invoice-preview endpoint + PricingPlan-sourced pricing (commit e81622a)
- `InvoicePreviewEndpoints.MapInvoicePreviewEndpoints` → `GET /api/v1/billing/invoice-preview`. Handler resolves tenant from `ISystemExecutionContextAccessor` (never client-supplied — T-17-21), resolves the tier `PricingPlan` from `LicenseState.Tier`, calls `IUsageAggregator.AggregateAsync` then `IBillingProvider.PreviewInvoiceAsync`, returns `{ tenantId, periodStart, periodEnd, tier, lineItems, totalAmount }`. Compute-only: only `PreviewInvoiceAsync` is called (no `RecordAsync`, no charge — D-03).
- `PricingEndpoints` refactored: `price` now derived from `ControlPlanePricingPlanProvider` (resolved from DI in the handler) — `$0` for Free, `$<base>/mo + $<rate>/PDF render` for paid tiers. The placeholder literals `"$20"`/`"Contact us"` are gone; the SPA shape `{ name, price, description, limits, features }` is unchanged.

### Task 3 — Integration tests + default-tenant quota seeding (commit 4d74f55)
- `QuotaEnforcementTests`: (1) seed `MaxApiRequestsPerMinute=1` for a unique tenant → 2nd request 429 with `Retry-After` header + `error="rate_limit_exceeded"` body; (2) anonymous `/api/v1/pricing` never 429'd (SC5); (3) fresh tenant first request not blocked.
- `InvoicePreviewEndpointTests`: seed `PdfRendersPerDay` usage → 200 + `totalAmount > 0` + a `PdfRendersPerDay` line whose `amount == quantity * unitRate`; a compute-only assertion comparing the stable PDF line across two previews.

## Requested record details

- **Where `UseQuotaEnforcement()` is placed:** in `Program.cs`, immediately AFTER the tenant-scoping `app.Use(...)` block that sets `ISystemExecutionContextAccessor` (the block ending ~line 570) and BEFORE the first API route group (`RouteGroupBuilder controlPlane = app.MapGroup("/api/v1/control-plane")`). It runs after `UseAuthentication`/`UseAuthorization`.
- **Invoice-preview route:** `GET /api/v1/billing/invoice-preview` (optional `periodStart`/`periodEnd` query params; default = current UTC day). Mapped via `app.MapInvoicePreviewEndpoints(!authOptions.DisableAuthorization)` alongside `app.MapPricingEndpoints()`.
- **PricingPlan numbers used (`ControlPlanePricingPlanProvider`, USD, D-05):**

  | Tier | PdfRendersPerDay rate | ApiRequestsPerMinute rate | FlatBaseAmount |
  |------|----------------------|---------------------------|----------------|
  | Free | 0 | 0 | 0 |
  | Starter | 0.05 | 0.002 | 20 |
  | Professional | 0.02 | 0.001 | 99 |
  | Enterprise | 0.01 | 0.0005 | 499 |

  LicenseTier→TenantTier: `Free→Free`, `Licensed→Professional`, `Enterprise→Enterprise`.
- **Final control-plane test count:** `dotnet test tests/Muonroi.ControlPlane.Host.Tests/...` → **Passed! Failed: 0, Passed: 484, Skipped: 0, Total: 484** (479 prior + 5 new).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug / Rule 2 - Missing critical functionality] Default-tenant quota seeded from the licensed tier**
- **Found during:** Task 3 (full Host.Tests suite gate — 12 tests failed with HTTP 429).
- **Issue:** `QuotaEnforcementMiddleware` caps the resolved tenant by its stored quota and, for an unconfigured tenant, `InMemoryTenantQuotaTracker` falls back to `TenantQuotaPresets.Free` (`MaxApiRequestsPerMinute = 20`). The shared-fixture endpoint test classes (`ControlPlaneEndpointTests`, `PortalEndpointTests`, `CopilotDraftEndpointTests`) fire many requests against the `"default"` tenant in one host, crossing 20 req/min and getting 429. This is also a real production bug: an Enterprise-licensed control-plane would silently throttle its own dashboard/admin traffic at the Free cap.
- **Fix:** `SeedDefaultTenantQuotaAsync(app.Services)` at startup seeds the `"default"` tenant quota from the host's runtime `LicenseState.Tier`, mapped through `ControlPlanePricingPlanProvider.MapLicenseTier` to the matching `TenantQuotaPresets`. Idempotent — never overwrites a tenant that already carries an explicit quota.
- **Files modified:** `muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Program.cs`
- **Commit:** 4d74f55
- **Evidence:** before fix `Failed: 12, Passed: 472` (11 × `200/404 expected, got 429`); after fix `Failed: 0, Passed: 484`.

**2. [Rule 1 - Bug] Invoice-preview idempotency assertion corrected to the metered-dimension line**
- **Found during:** Task 3 (RED run of `InvoicePreview_IsComputeOnly_IdempotentAmount`).
- **Issue:** Comparing the full `totalAmount` across two preview requests failed (504.0010 vs 504.0005). The shared singleton `ITenantQuotaStore` legitimately records each preview request as one `ApiRequestsPerMinute` unit (the enforcement middleware increments usage), so the second preview's total reflects one extra metered API unit × the Enterprise API rate (0.0005). This is faithful metering, not a charge.
- **Fix:** the idempotency assertion compares the `PdfRendersPerDay` line `amount` (the seeded dimension under test), which is stable across previews — proving the preview is compute-only and non-mutating for that dimension. The "no charge" property is also held by the handler calling only `PreviewInvoiceAsync` (no `RecordAsync`).
- **Files modified:** `muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/InvoicePreviewEndpointTests.cs`
- **Commit:** 4d74f55

## Threat Model Compliance
- **T-17-20 (DoS):** mitigated — `UseQuotaEnforcement()` → 429 over `MaxApiRequestsPerMinute`. Proven by `QuotaEnforcementTests.OverApiLimit_TenantScopedRequest_Returns429AtBoundary`.
- **T-17-21 (Information Disclosure):** mitigated — invoice-preview tenant comes only from `ISystemExecutionContextAccessor`, never a client-supplied id; reuses the 17-02 single-tenant aggregator.
- **T-17-22 (EoP / SC5):** mitigated — enforcement is API-boundary middleware; the OSS `IMPdfService.RenderAsync` path is untouched; the anonymous/unscoped surface is not blocked (`NoTenantHeader_AnonymousSurface_NotBlockedByEnforcement`). No file under `muonroi-building-block/src/Muonroi.Pdf` was modified.
- **T-17-23 (Tampering):** mitigated — only `PreviewInvoiceAsync` is called (compute-only); no `RecordAsync`/charge in the endpoint.
- **T-17-SC (no new packages):** held — only source `ProjectReference`s added; zero NuGet packages. Package Legitimacy Gate not triggered.

## Known Stubs
None. Enforcement is registered and exercised; the invoice-preview endpoint is wired end-to-end (aggregator → preview → total); pricing is sourced from the real `PricingPlan`.

## Threat Flags
None. No new security surface beyond the registered threat_model items, all mitigated.

## Commits (control-plane code, branch develop)
- `8490ddc` — feat(17-03): register quota enforcement + billing rail DI; PricingPlan provider (MON-05/MON-06)
- `e81622a` — feat(17-03): compute-only invoice-preview endpoint + PricingPlan-sourced PricingEndpoints (MON-06)
- `4d74f55` — test(17-03): 429-enforcement + invoice-preview integration tests; default-tenant quota seeding (MON-05/MON-06)

## Self-Check: PASSED
- All created files present (verified below).
- All three control-plane commits present in git log (verified below).
