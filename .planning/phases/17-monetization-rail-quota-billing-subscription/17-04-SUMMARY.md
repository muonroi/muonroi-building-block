---
phase: 17-monetization-rail-quota-billing-subscription
plan: 04
subsystem: license-server
tags: [subscription, renewal, quota-limits, rsa-signing, ef-migration, MON-07, D-04]
requires:
  - "existing LicenseSigningService (RSA SignAsync + BuildLicenseSigningData)"
  - "ILicenseRepository GetByKeyAsync/UpdateLicenseAsync"
provides:
  - "SubscriptionService.RenewAsync (subscription renewal lifecycle reusing RSA signing)"
  - "expiry/grace computation (IsWithinGrace/IsExpired)"
  - "TierQuotaLimits.For(LicenseTier) tier->quota-limit map consumed by control-plane (17-03)"
  - "POST /api/v1/keys/{licenseKey}/renew + GET /api/v1/keys/tiers/{tier}/limits"
affects:
  - "muonroi-control-plane (17-03 consumes tier->limit mapping)"
  - "muonroi-building-block (TenantQuotaPresets finite-vs-unlimited alignment)"
tech-stack:
  added: []
  patterns: ["minimal-API endpoint group extension", "EF Core Npgsql AddColumn migration", "RSA re-sign via existing signing service"]
key-files:
  created:
    - "muonroi-license-server/src/Muonroi.LicenseServer/Services/SubscriptionService.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/TierQuotaLimits.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Storage/Migrations/20260621012757_Phase17Subscription.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Storage/Migrations/20260621012757_Phase17Subscription.Designer.cs"
    - "muonroi-license-server/tests/Muonroi.LicenseServer.Tests/SubscriptionServiceTests.cs"
  modified:
    - "muonroi-license-server/src/Muonroi.LicenseServer/Storage/Entities/LicenseRecord.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Storage/LicenseServerDbContext.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Storage/Migrations/LicenseServerDbContextModelSnapshot.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Services/Contracts.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Endpoints/KeyEndpoints.cs"
    - "muonroi-license-server/src/Muonroi.LicenseServer/Program.cs"
    - "muonroi-license-server/tests/Muonroi.LicenseServer.Tests/InMemoryLicenseRepository.cs"
    - "muonroi-license-server/tests/Muonroi.LicenseServer.Tests/EndpointTests.cs"
decisions:
  - "Renew extends from current ExpiresAt when still valid, else from now (so renewal always lands in the future)"
  - "Grace state is computed (IsWithinGrace = now <= ExpiresAt + GraceHours), never stored"
  - "Revoked-license renewal rejected via MValidationException (renewal must not resurrect a revoked license)"
  - "TierQuotaLimits: Free/Licensed finite, Enterprise unlimited (long.MaxValue sentinel)"
metrics:
  duration: "~30m"
  completed: "2026-06-21"
  tasks: 3
  files: 13
---

# Phase 17 Plan 04: Subscription + Renewal Lifecycle (license-server) Summary

Subscription/renewal lifecycle added to muonroi-license-server (D-04 / MON-07): `LicenseRecord` gains `RenewalCount`/`LastRenewedAt`/`GraceHours`, a new `SubscriptionService.RenewAsync` extends `ExpiresAt` and re-signs the payload through the EXISTING RSA `LicenseSigningService` (no forked crypto), a deterministic `TierQuotaLimits.For(LicenseTier)` map is exposed for control-plane to consume, and two authz-gated endpoints (renew + tier-limits) were wired onto the existing `/api/v1/keys` group. Migration `Phase17Subscription` generated (not applied to any DB).

## What Was Built (per task)

### Task 1 — Subscription fields + EF migration (commit 197ecc9)
- `LicenseRecord`: `int RenewalCount` (default 0), `DateTimeOffset? LastRenewedAt`, `int GraceHours` (default 24, matching the Enterprise revocation-grace convention).
- `OnModelCreating`: `RenewalCount` default 0, `GraceHours` default 24 (DB-level defaults backfill existing rows).
- Migration **`20260621012757_Phase17Subscription`** generated via `dotnet ef migrations add` against Npgsql design-time factory; model snapshot updated. Confirmed present via `dotnet ef migrations list`. **NOT applied to any live DB** (generation-only, per plan).

### Task 2 — SubscriptionService + TierQuotaLimits + tests (commit d5f63b9)
- `SubscriptionService.RenewAsync(RenewLicenseRequest, ct)`: looks up by key (`MNotFoundException` if absent), rejects `IsRevoked` (`MValidationException`), computes new `ExpiresAt` (from current expiry if still valid, else from now), rebuilds `LicensePayload`, re-signs via `signingService.SignAsync(LicenseSigningService.BuildLicenseSigningData(payload))`, updates `SignedPayload`/`SigningKeyId`/`RenewalCount`/`LastRenewedAt`/`UpdatedAt`, persists via `UpdateLicenseAsync`. **No Silent Catch**: not-found, re-sign failure, and malformed-stored-payload paths all log with module/operation/context before rethrow or fallback.
- Static `IsWithinGrace(record, now)` / `IsExpired(record, now)`: `now <= ExpiresAt + GraceHours`.
- `TierQuotaLimits.For(LicenseTier)` returns `IReadOnlyDictionary<string,long>` keyed by dimension name.
- `RenewLicenseRequest` / `RenewLicenseResult` records added to `Contracts.cs`.
- `SubscriptionService` registered in `Program.cs` DI (`AddScoped`).
- 5 `SubscriptionServiceTests` (the 4 required behavior cases + tier split) green.

### Task 3 — Endpoints (commit b25c40a)
- `POST /api/v1/keys/{licenseKey}/renew` — binds `RenewKeyRequest { int ValidDays }` (license key from route), calls `RenewAsync`, returns `{ licenseKey, expiresAt, signedPayload, signingKeyId, renewalCount }`. Gated `RequireAuthorization("license-generate")` (issuance-class, T-17-10). 404 flows from the service's `MNotFoundException`.
- `GET /api/v1/keys/tiers/{tier}/limits` — returns `{ tier, limits }` from `TierQuotaLimits.For`; `BadRequest` on unparseable tier. Gated `RequireAuthorization("license-read")`.
- Endpoint test added (renew extends expiry + increments renewalCount; tier-limits exposes `PdfRendersPerDay`; bad tier → 400).

## Exact Routes / Mapping Shape / Fields (for 17-03 consumption)

**Renew endpoint:** `POST /api/v1/keys/{licenseKey}/renew`
- Body: `{ "validDays": <int> }` (coerced to >= 1 server-side)
- Response 200: `{ licenseKey, expiresAt, signedPayload, signingKeyId, renewalCount }`
- Authz policy: `license-generate`

**Tier→limit endpoint:** `GET /api/v1/keys/tiers/{tier}/limits`
- Response 200: `{ "tier": "<TierName>", "limits": { "PdfRendersPerDay": <long>, "ApiRequestsPerMinute": <long> } }`
- `400` on unparseable tier; authz policy: `license-read`

**`TierQuotaLimits` mapping shape** (`Muonroi.LicenseServer.TierQuotaLimits`):
- `static IReadOnlyDictionary<string,long> For(LicenseTier tier)`
- Dimension keys (stable strings, `TierQuotaLimits.Dimensions`): `PdfRendersPerDay`, `ApiRequestsPerMinute`
- `Unlimited = long.MaxValue` sentinel; `IsUnlimited(long)` helper
- Values:
  | Tier | PdfRendersPerDay | ApiRequestsPerMinute |
  |------|------------------|----------------------|
  | Free | 50 | 20 |
  | Licensed | 5000 | 100 |
  | Enterprise | Unlimited (long.MaxValue) | Unlimited |
- Unknown tiers fall back to the most-restrictive (Free) map.

**Subscription fields added to `LicenseRecord`:** `RenewalCount` (int, default 0), `LastRenewedAt` (DateTimeOffset?, nullable), `GraceHours` (int, default 24).

**Alignment note (17-02):** Finite-for-Free/Licensed, Unlimited-for-Enterprise mirrors the `TenantQuotaPresets` finite-vs-unlimited decision. Building-block `TenantQuota.MaxPdfRendersPerDay` is currently `int.MaxValue` for all tiers (Phase 16 record-only); the finite values above fit in `int` range so they map cleanly when 17-02 wires finite caps.

## Migration

- Name: **`20260621012757_Phase17Subscription`**
- Adds columns to `Licenses`: `GraceHours` (integer NOT NULL default 24), `LastRenewedAt` (timestamptz nullable), `RenewalCount` (integer NOT NULL default 0).
- Generated by `dotnet ef migrations add` (Npgsql design-time factory); snapshot updated; not applied to a DB.

## Threat Model Compliance
- **T-17-10 (EoP, renew endpoint):** mitigated — `RequireAuthorization("license-generate")`, behind `AdminApiKeyMiddleware`.
- **T-17-11 (Tampering, re-sign):** mitigated — reuses `LicenseSigningService.SignAsync` + `BuildLicenseSigningData`; no new/forked crypto. Verified by test asserting the re-signed payload verifies against the active RSA key.
- **T-17-12 (Spoofing, revoked renewal):** mitigated — `RenewAsync` rejects `IsRevoked`; covered by test.
- **T-17-SC (no new packages):** held — no NuGet packages added to the project. `dotnet-ef` is a CLI dev tool (already installed globally), not a project dependency. Package Legitimacy Gate not triggered.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Minimal-API body binding 400 on renew route**
- **Found during:** Task 3 (endpoint test)
- **Issue:** Binding the renew body to `RenewLicenseRequest` (which has a `required string LicenseKey`) caused JSON deserialization to 400 when the client posts only `{ validDays }`, since the license key comes from the route, not the body.
- **Fix:** Introduced a dedicated `KeyEndpoints.RenewKeyRequest { int ValidDays }` body DTO; the handler builds the service-level `RenewLicenseRequest` from the route key + body ValidDays.
- **Files modified:** `KeyEndpoints.cs`
- **Commit:** b25c40a

**2. [Rule 3 - Blocking] `Options.Create` namespace collision in test**
- **Found during:** Task 2
- **Issue:** `Options.Create(...)` resolved to the `Muonroi.LicenseServer.Options` namespace, not `Microsoft.Extensions.Options.Options`.
- **Fix:** Fully-qualified `Microsoft.Extensions.Options.Options.Create` (same pattern as `LicenseIssuerServiceTests`); removed the unused `using Microsoft.Extensions.Options;`.
- **Files modified:** `SubscriptionServiceTests.cs`
- **Commit:** d5f63b9

### Tooling note (not a code deviation)
- `dotnet-ef` was not on PATH for the shell; invoked via `$HOME/.dotnet/tools` (it was already installed globally). No project package added.

## Test Result (Pre-Push Test Gate)
`dotnet test tests/Muonroi.LicenseServer.Tests/Muonroi.LicenseServer.Tests.csproj -c Debug` → **Passed! Failed: 0, Passed: 26, Skipped: 0, Total: 26.**

## Known Stubs
None. All new surface is wired (renew endpoint → service → repo → RSA signer; tier-limits endpoint → TierQuotaLimits). `ApiRequestsPerMinute` is provided as a second deterministic dimension alongside the required `PdfRendersPerDay`; both are real values, not placeholders.

## Self-Check: PASSED
- All created files present (SubscriptionService.cs, TierQuotaLimits.cs, 20260621012757_Phase17Subscription.cs, SubscriptionServiceTests.cs, 17-04-SUMMARY.md).
- All license-server commits exist: 197ecc9, d5f63b9, b25c40a.
