# Phase 16: PDF Enterprise Governance/ControlPlane Integration — Research

**Researched:** 2026-06-20
**Domain:** .NET Enterprise licensing / quota / compliance / canary rollback wired onto existing
`Muonroi.Governance.Enterprise` + `Muonroi.Quota` rails
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 Fail-closed license gate:** Replace `AlwaysAllowFeatureGate` with a real `IFeatureGate`
  bound to governance `ActivationProof` + `MEnterpriseFailClosedMatrix`. For an enterprise
  capability (`pdf.designer` / `pdf.registry` / `pdf.canary`) that is not licensed,
  `EnsureFeatureOrThrow` throws `FeatureNotLicensedException`. Open-core boundary explicit:
  `IMPdfService.RenderAsync` (no enterprise feature) is NEVER gated; only registry/designer/canary
  add-ons are gated.

- **D-02 Record-only per-tenant / per-render metering via `Muonroi.Quota`:** never blocks a
  production render; one metered event per render, tagged with tenant id, page count as metered
  dimension. Hard-quota enforcement deferred.

- **D-03 Publish/version events only into Compliance evidence pack:** feed the existing 6
  control-plane audit events (`pdf.template.{created,updated,submitted,approved,rejected,activated}`)
  into the evidence pack. Render-time audit deferred.

- **D-04 Canary auto-rollback at control-plane policy layer (WS-B):** when SSIM < threshold,
  control-plane triggers `ICanaryRolloutService.RollbackCanaryAsync` before 100% traffic. Engine
  only scores via existing `SsimScorer`; no rollback/operational logic in OSS.

### Claude's Discretion

- Exact DI seam for providing `ActivationProof` to `Pdf.Enterprise` (host-supplied via
  `EnterpriseGovernanceServiceExtensions`), the precise `IFeatureGate` implementation class name,
  where the metering hook physically sits (Enterprise service wrapper around `IMPdfService` vs
  control-plane render path), and the `Muonroi.Quota` abstraction method shape.
- Control-plane rollback policy mechanics (threshold config source, traffic-shift steps).

### Deferred Ideas (OUT OF SCOPE)

- Render-time compliance audit (every render to evidence pack).
- Hard quota enforcement (blocking render over limit).
- Designer P95 / hot-reload production load-test.
- Cross-service TCIS cutover follow-ups 9.5b-e.
- Flexbox / rendering-engine work.
</user_constraints>

---

## Summary

Phase 16 is a **gap-closure** phase on Phase 9. Every component it touches already exists; the
work is wiring, not building.  The four locked decisions each map to a distinct and well-understood
seam in the codebase.

**D-01** requires a new `LicenseFeatureGate : IFeatureGate` class inside `Muonroi.Pdf.Enterprise`
that delegates to the singleton `ILicenseGuard` (already registered by `AddMEnterpriseGovernance`).
The `ActivationProof` is carried inside `LicenseState.ActivationProof` (a singleton in DI).
`HasFeature(capabilityKey)` on `ILicenseGuard` routes through `LicenseCapabilityResolver.HasAccess`
which checks `LicenseState.Features[]` — the same string array written by the license-server into
the RSA-signed proof. **Critical gap found:** the `pdf.*` capability keys are NOT registered in
`LicenseCapabilityResolver.CapabilityKeys` or `MEnterpriseFailClosedMatrix.BlocksAllEnterpriseCapabilities`.
Both must be extended as part of D-01.

**D-02** requires calling `ITenantQuotaTracker.IncrementUsageAsync(tenantId, QuotaType.???, pageCount)`
after every render. **Critical gap found:** `QuotaType` enum has no PDF member. A new
`QuotaType.PdfRendersPerDay` value must be added before D-02 can be wired.

**D-03** requires an `IMControlPlaneStore` implementation in the control-plane repo that the
`MComplianceExportService` polls. The 6 audit events (`PdfTemplateAuditActions.*`) are already
written to `IRuleSetAuditStore` by `PdfTemplateRegistryService`; the gap is surfacing them through
an `IMControlPlaneStore` adapter so `MComplianceExportService` includes them in the evidence chain.

**D-04** is entirely in the control-plane: extend the `POST /api/canary/pdf/score` endpoint (already
live, using `SsimScorer`) so that when the returned SSIM is below `PdfCanaryOptions.SsimThreshold`
(default 0.95), it calls `ICanaryRolloutService.RollbackCanaryAsync(rolloutId, "system",
"SSIM below threshold", ct)`. `ICanaryRolloutService` and `CanaryRolloutRecord` already exist in
`Muonroi.RuleEngine.Abstractions`.

**Primary recommendation:** tackle D-01 + D-02 (WS-A building-block) as one wave, D-03 (WS-B/C
control-plane compliance) as a second, D-04 (WS-B canary rollback) as a third, then WS-D
license-server audit in a fourth.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Feature gate enforcement (D-01) | `Muonroi.Pdf.Enterprise` | `Muonroi.Governance.Enterprise` (LicenseGuard) | SC5: Enterprise→OSS one-way; gate lives in Enterprise layer binding to Governance |
| Render metering (D-02) | `Muonroi.Pdf.Enterprise` (wrapper around `IMPdfService`) | `Muonroi.Quota.Abstractions` | Never in OSS engine; metering hook wraps the OSS call site |
| Compliance evidence pack (D-03) | Control-plane (IMControlPlaneStore adapter) | `Muonroi.Governance.Enterprise` (MComplianceExportService polls it) | Audit events are already in the control-plane DB; compliance machinery pulls from `IMControlPlaneStore` |
| Canary auto-rollback (D-04) | Control-plane (`POST /api/canary/pdf/score` extension) | `ICanaryRolloutService` | Score is already computed; rollback call is one line after scoring |
| License entitlement source (WS-D) | `muonroi-license-server` (`KnownPdfCapabilities`) | `ActivationProof.Features[]` | Claim-agnostic RSA pipeline; no schema change needed |
| UI capability gating (WS-C) | `muonroi-ui-engine` (`<RequireCapability>`) | — | Already shipped `pdf.designer` gate in Phase 9.3; extend for registry/canary if needed |

---

## Standard Stack

### Core (all pre-existing, no new packages)

| Library / Class | Location | Purpose | Version |
|-----------------|----------|---------|---------|
| `ILicenseGuard` | `src/Muonroi.Governance.Abstractions/License/ILicenseGuard.cs` | Runtime feature check: `HasFeature(key)`, `EnsureFeature(key)` | in-repo |
| `LicenseState` | `src/Muonroi.Governance.Abstractions/License/LicenseState.cs` | Singleton holding `ActivationProof`, `Features[]`, `Tier` | in-repo |
| `LicenseCapabilityResolver` | `src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs` | `HasAccess(state, key)` + `CapabilityKeys` set + `FeatureToCapability` map — must be extended with `pdf.*` | in-repo |
| `MEnterpriseFailClosedMatrix` | `src/Muonroi.Governance.Enterprise/License/MEnterpriseFailClosedMatrix.cs` | `ShouldBlock(feature, reason)` — must add `pdf.*` to `BlocksAllEnterpriseCapabilities` | in-repo (internal) |
| `EnterpriseGovernanceServiceExtensions.AddMEnterpriseGovernance` | `src/Muonroi.Governance.Enterprise/EnterpriseGovernanceServiceExtensions.cs` | DI registration seam; extend for `IFeatureGate` | in-repo |
| `ITenantQuotaTracker` | `src/Muonroi.Quota.Abstractions/ITenantQuotaTracker.cs` | `IncrementUsageAsync(tenantId, type, amount, ct)` — metering seam | in-repo |
| `QuotaType` | `src/Muonroi.Quota.Abstractions/QuotaType.cs` | Enum — must add `PdfRendersPerDay` | in-repo |
| `IMComplianceEvidencePackService` | `src/Muonroi.Governance.Enterprise/Compliance/IMComplianceEvidencePackService.cs` | `GenerateAsync(request, ct)` — evidence pack generator | in-repo |
| `IMControlPlaneStore` | `src/Muonroi.Governance.Enterprise/ControlPlane/IControlPlaneStore.cs` | Store interface polled by `MComplianceExportService` | in-repo |
| `ICanaryRolloutService` | `src/Muonroi.RuleEngine.Abstractions/Rules/ICanaryRolloutService.cs` | `RollbackCanaryAsync(id, by, reason, ct)` — D-04 hook | in-repo |
| `PdfCanaryOptions` | `muonroi-control-plane/src/.../Options/PdfCanaryOptions.cs` | `SsimThreshold = 0.95` | in-repo |
| `KnownPdfCapabilities` | `muonroi-license-server/src/.../KnownPdfCapabilities.cs` | `pdf.designer`, `pdf.registry`, `pdf.canary` — already in license-server | in-repo |

### No new packages required

All required machinery already exists in-repo. No NuGet additions needed.

---

## Package Legitimacy Audit

> This phase installs no new external packages. Section is not applicable.

---

## Architecture Patterns

### System Architecture Diagram

```
ActivationProof (file / online)
        |
        v
LicenseStore.LoadActivationProof()
        |
        v
LicenseState (singleton in DI)
    .Features[] = ["pdf.designer","pdf.registry","pdf.canary",...]
    .ActivationProof = <RSA signed proof>
        |
        v
ILicenseGuard.HasFeature(capabilityKey)
        |
        v
[NEW] LicenseFeatureGate : IFeatureGate (in Muonroi.Pdf.Enterprise)
    .IsEnabled(key)         -- delegates to ILicenseGuard.HasFeature
    .EnsureFeatureOrThrow() -- throws FeatureNotLicensedException if !IsEnabled
        |
        |--- called by: IMPdfTemplateRegistry (gate: pdf.registry)
        |--- called by: PDF Designer endpoints (gate: pdf.designer)
        |--- called by: Canary score endpoint (gate: pdf.canary)
        |
        v
[OSS boundary: IMPdfService.RenderAsync --- NEVER GATED]

Render path (Enterprise side):
IMPdfService.RenderAsync(html, dest, opts, ct)   <-- OSS, ungated
        |
[NEW] EnterprisePdfServiceWrapper wraps this call
        |
        v
ITenantQuotaTracker.IncrementUsageAsync(
    tenantId, QuotaType.PdfRendersPerDay, pageCount, ct)  <-- record-only, D-02
        |
        v (fire-and-forget or background; never blocks render return)

Control-plane audit chain (D-03):
PdfTemplateRegistryService
  .SubmitAsync / .ApproveAsync / .ActivateAsync
        |
        v
IRuleSetAuditStore.AppendAsync(
    RuleSetAuditEntry { Action = PdfTemplateAuditActions.Approved, ... })
        |
[NEW] PdfAuditControlPlaneStore : IMControlPlaneStore
  reads RuleSetAuditStore entries for pdf.template.* events
        |
        v
MComplianceExportService polls IMControlPlaneStore
  --> MComplianceEvidencePackService.GenerateAsync(request)

Canary auto-rollback (D-04):
POST /api/canary/pdf/score
  SsimScorer.Compare(baseline, candidate, w, h) --> score
        |
  if score < PdfCanaryOptions.SsimThreshold (0.95)
        |
        v
ICanaryRolloutService.RollbackCanaryAsync(
    rolloutId, "system", "SSIM below threshold", ct)
```

### Recommended Project Structure Changes

```
src/Muonroi.Pdf.Enterprise/
├── IFeatureGate.cs                     [EXISTS — keep interface as-is]
├── AlwaysAllowFeatureGate.cs           [EXISTS — keep for OSS/dev]
├── FeatureNotLicensedException.cs      [EXISTS — keep]
├── CapabilityKeys.cs                   [EXISTS — keep]
├── License/
│   └── LicenseFeatureGate.cs           [NEW — real IFeatureGate binding ILicenseGuard]
├── Metering/
│   └── EnterprisePdfServiceWrapper.cs  [NEW — wraps IMPdfService + IncrementUsageAsync]
└── Extensions/
    └── PdfEnterpriseServiceExtensions.cs [NEW — AddPdfEnterprise(services)]

src/Muonroi.Governance.Abstractions/License/
└── LicenseCapabilityResolver.cs        [MODIFY — add pdf.* to CapabilityKeys + FeatureToCapability]

src/Muonroi.Governance.Enterprise/License/
└── MEnterpriseFailClosedMatrix.cs      [MODIFY — add pdf.* to BlocksAllEnterpriseCapabilities]

src/Muonroi.Quota.Abstractions/
└── QuotaType.cs                        [MODIFY — add PdfRendersPerDay]

muonroi-control-plane:
src/Host/Muonroi.ControlPlane.Host/
├── Endpoints/CanaryEndpoints.cs        [MODIFY — wire rollback when SSIM < threshold]
├── Options/PdfCanaryOptions.cs         [EXISTS — SsimThreshold=0.95]
└── Services/Compliance/
    └── PdfAuditControlPlaneStore.cs    [NEW — IMControlPlaneStore adapter over IRuleSetAuditStore]
```

### Pattern 1: LicenseFeatureGate — binding IFeatureGate to ILicenseGuard

**What:** New `LicenseFeatureGate` implements `IFeatureGate` by delegating to the existing
`ILicenseGuard` singleton. Because `ILicenseGuard.HasFeature` routes through
`LicenseCapabilityResolver.HasAccess`, which reads `LicenseState.Features[]`, and the
`ActivationProof.Features[]` array is populated by the license-server claim-agnostic RSA pipeline
with `pdf.designer` / `pdf.registry` / `pdf.canary` strings, the gate is fully wired without any
new cryptographic code.

**When to use:** Injected wherever `IFeatureGate` is resolved; replaces `AlwaysAllowFeatureGate`
in production DI.

```csharp
// Source: evidence from ILicenseGuard.cs, LicenseCapabilityResolver.cs, LicenseState.cs
// in src/Muonroi.Governance.Abstractions/License/

using Muonroi.Governance.License;

namespace Muonroi.Pdf.Enterprise.License;

/// <summary>
/// Real IFeatureGate bound to the shared Muonroi license guard.
/// Delegates to ILicenseGuard.HasFeature which reads ActivationProof.Features[]
/// via LicenseCapabilityResolver.HasAccess — no new crypto required.
/// </summary>
public sealed class LicenseFeatureGate(ILicenseGuard licenseGuard) : IFeatureGate
{
    public bool IsEnabled(string capabilityKey)
        => licenseGuard.HasFeature(capabilityKey);

    public void EnsureFeatureOrThrow(string capabilityKey)
    {
        if (!IsEnabled(capabilityKey))
            throw new FeatureNotLicensedException(capabilityKey);
    }
}
```

**DI registration** (in `AddPdfEnterprise` extension, called after `AddMEnterpriseGovernance`):

```csharp
// Source: pattern from EnterpriseGovernanceServiceExtensions.cs
services.TryAddSingleton<IFeatureGate, LicenseFeatureGate>();
// AlwaysAllowFeatureGate.Instance remains available for unit tests via direct construction.
```

### Pattern 2: EnterprisePdfServiceWrapper — render metering (D-02)

**What:** Decorator wrapping the OSS `IMPdfService`. After the render completes, records page count
via `ITenantQuotaTracker.IncrementUsageAsync`. Never throws; metering failure is logged and
swallowed (record-only, no blocking per D-02).

```csharp
// Source: evidence from ITenantQuotaTracker.cs, QuotaType.cs, IMPdfService (ABST-01..03)
namespace Muonroi.Pdf.Enterprise.Metering;

public sealed class EnterprisePdfServiceWrapper(
    IMPdfService inner,
    ITenantQuotaTracker quotaTracker,
    ITenantContext tenantContext,
    IMLog<EnterprisePdfServiceWrapper>? logger = null) : IMPdfService
{
    public async Task RenderAsync(
        string html, Stream destination, PdfRenderOptions options, CancellationToken ct)
    {
        PdfRenderResult result = await inner.RenderToBytesAsync(html, options, ct)
            // ... (or stream overload)
        await RecordMeteringAsync(tenantContext.TenantId, result.PageCount, ct);
    }

    private async Task RecordMeteringAsync(string? tenantId, int pageCount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return;
        try
        {
            await quotaTracker.IncrementUsageAsync(
                tenantId, QuotaType.PdfRendersPerDay, pageCount, ct);
        }
        catch (Exception ex)
        {
            logger?.Warn("[PDF] Metering record failed (non-blocking): {Message}", ex.Message);
        }
    }
}
```

### Pattern 3: LicenseCapabilityResolver extension (required for D-01)

The `CapabilityKeys` HashSet and `BlocksAllEnterpriseCapabilities` method in
`MEnterpriseFailClosedMatrix` are the ONLY places that enumerate known enterprise capabilities.
Both must include `pdf.*` keys or `ILicenseGuard.HasFeature("pdf.designer")` will always return
`false` for non-Enterprise tiers, and the fail-closed matrix will not block on missing policy.

```csharp
// In LicenseCapabilityResolver.Capabilities:
public const string PdfDesigner  = "pdf.designer";
public const string PdfRegistry  = "pdf.registry";
public const string PdfCanary    = "pdf.canary";

// In CapabilityKeys HashSet — add all three.
// In FeatureToCapability map — pdf.* keys already ARE the canonical capability key
//   (same pattern as "connectors" -> Capabilities.Connectors), so add:
//   ["pdf.designer"] = Capabilities.PdfDesigner (etc.)
```

### Pattern 4: Compliance evidence pack adapter (D-03)

`MComplianceExportService` already reads from `IEnumerable<IMControlPlaneStore>`. A new
`PdfAuditControlPlaneStore : IMControlPlaneStore` in the control-plane wraps the
`IRuleSetAuditStore` and returns `pdf.template.*` audit records as `MControlPlaneAuditRecord`
entries within a `MControlPlaneRegistry.AuditTrail`. Registered via
`services.AddSingleton<IMControlPlaneStore, PdfAuditControlPlaneStore>()`.

### Pattern 5: Canary score + auto-rollback (D-04)

Extend `ScorePdfSsimAsync` in `CanaryEndpoints.cs`. Caller must supply `rolloutId` in the
multipart request or query string. After scoring, if `ssim < options.SsimThreshold`:

```csharp
// Source: evidence from ICanaryRolloutService.cs, CanaryEndpoints.cs, PdfCanaryOptions.cs
await canaryService.RollbackCanaryAsync(
    rolloutId,
    rolledBackBy: "system",
    reason: $"SSIM {ssim:F4} below threshold {options.SsimThreshold}",
    cancellationToken);
```

### Anti-Patterns to Avoid

- **Gating `IMPdfService.RenderAsync` directly:** SC5 violation. The OSS engine must have zero
  awareness of `IFeatureGate`. The gate belongs on the Enterprise wrapper or the registry/designer
  entry points, not on the render method itself.
- **Using `AlwaysAllowFeatureGate` in production DI registration:** It is the no-op stub. The
  planner must replace it, not supplement it.
- **Calling `ILicenseGuard.EnsureFeature` instead of `IFeatureGate.EnsureFeatureOrThrow`:** The
  former throws `MInternalException` (governance-specific), the latter throws
  `FeatureNotLicensedException` (PDF-specific, no governance dep for callers). Use the PDF
  exception to preserve the clean boundary.
- **Blocking the render on quota error:** D-02 is record-only. The `IncrementUsageAsync` path must
  be fire-and-log-on-failure; any exception from the tracker must NOT propagate to the caller.
- **Writing metering in `Muonroi.Pdf` (OSS):** The OSS engine has no quota dependency. The
  metering wrapper lives exclusively in `Muonroi.Pdf.Enterprise`.
- **Skipping the `Muonroi.snk` strong-naming requirement on `Muonroi.Pdf.Enterprise`:** The csproj
  already has `<SignAssembly>true</SignAssembly>` and `<AssemblyOriginatorKeyFile>../../Muonroi.snk`.
  Any new file in `Muonroi.Pdf.Enterprise` is automatically signed; no change needed. But if a
  NEW `*.csproj` is created that references `Muonroi.Governance.Enterprise`, it must also be
  strong-named and marked `<IsCommercialPackage>true`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| RSA signature verification of ActivationProof | Custom RSA verifier | `LicenseVerifier` (already registered by `AddLicenseProtection`) | The verifier is already wired at startup; `LicenseState.IsValid` reflects the result |
| Feature entitlement lookup | Custom string-matching against ActivationProof.Features[] | `ILicenseGuard.HasFeature(key)` | Already handles tier overrides, wildcards, legacy feature-to-capability mapping |
| Per-tenant quota storage | Custom counter dict | `ITenantQuotaTracker.IncrementUsageAsync` | Handles concurrency, backing store abstraction, daily reset |
| Compliance export chain | Custom hash-chain file writer | `MComplianceEvidencePackService.GenerateAsync` | Already does HMAC-signed hash chains, JSON serialization, file pruning |
| SSIM scoring | Custom SSIM implementation | `SsimScorer.Compare` (already in `Muonroi.Pdf.Enterprise.Quality`) | Wang/Bovik 2004 reference implementation, tested, ships in the Phase 9 binary |
| Canary state machine | Custom rollout record / status transitions | `ICanaryRolloutService.RollbackCanaryAsync` | Status enum, DB record, actor audit already implemented in `CanaryRolloutService` |

---

## Runtime State Inventory

> This is a gap-closure phase on Phase 9, not a rename/rebrand/migration. No string
> renaming or key migration is required. Section applies only minimally.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | No existing `pdf.designer/registry/canary` quota usage records — `QuotaType.PdfRendersPerDay` is a new enum member | code edit (add enum value + `GetLimit` switch arm in `InMemoryTenantQuotaTracker`) |
| Live service config | `PdfCanaryOptions.SsimThreshold = 0.95` in control-plane appsettings | None — already present; extend endpoint to use it |
| OS-registered state | None — no task scheduler / pm2 involvement | None verified |
| Secrets/env vars | No new secrets; existing RSA public key for ActivationProof verification already downloaded by `LicenseActivator` | None |
| Build artifacts | `Muonroi.Pdf.Enterprise` already has `1.0.0-alpha.14/16` nuspec in obj/ | Rebuild after changes; version bump in CPM |

---

## Common Pitfalls

### Pitfall 1: `pdf.*` keys not registered in `LicenseCapabilityResolver`

**What goes wrong:** `ILicenseGuard.HasFeature("pdf.designer")` returns `false` even for a fully
licensed tenant, because `LicenseCapabilityResolver.ResolveCapability` returns `null` for unknown
keys — and `LicenseState.Tier == Enterprise` short-circuits first (line 122 of `LicenseCapabilityResolver.cs`),
so Enterprise tenants are fine, but `LicenseTier.Licensed` tenants with explicit `pdf.*` features
in their proof will be incorrectly denied.

**Root cause:** `CapabilityKeys` HashSet and `FeatureToCapability` dictionary in
`LicenseCapabilityResolver` do not contain `pdf.*`. Confirmed by reading
`src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs` lines 81–95.

**How to avoid:** In the same PR as D-01, extend `LicenseCapabilityResolver.Capabilities` with
`PdfDesigner/PdfRegistry/PdfCanary` constants, add them to `CapabilityKeys`, and add
`["pdf.designer"] = Capabilities.PdfDesigner` etc. to `FeatureToCapability`.

**Warning signs:** Unit test `LicenseState.HasFeature("pdf.designer")` returns `false` for a
`LicenseTier.Licensed` state with `Features = ["pdf.designer"]`.

### Pitfall 2: `MEnterpriseFailClosedMatrix` excludes `pdf.*` from fail-closed blocking

**What goes wrong:** If a host has `MissingSignedPolicy` (e.g. production without signed policy),
`MEnterpriseFailClosedMatrix.ShouldBlock("pdf.designer", MissingSignedPolicy)` returns `false`
because `BlocksAllEnterpriseCapabilities` only checks the hardcoded list
(`core.runtime`, `auth.rbac_plus`, ... — confirmed `src/Muonroi.Governance.Enterprise/License/MEnterpriseFailClosedMatrix.cs` lines 39–51).

**Root cause:** `pdf.*` not in the static list.

**How to avoid:** Add `pdf.designer/registry/canary` to `BlocksAllEnterpriseCapabilities`. Note
`MEnterpriseFailClosedMatrix` is `internal static` — change is internal to
`Muonroi.Governance.Enterprise`.

**Warning signs:** Test passes for the gate even when `LicenseConfigs.FailMode = Hard` and no
signed policy exists.

### Pitfall 3: `QuotaType.PdfRendersPerDay` missing from `GetLimit` switch arm

**What goes wrong:** `InMemoryTenantQuotaTracker.GetLimit` has a `type switch` with no default arm
for the new enum value → runtime `MatchFailureException` (pattern matching exhaustion) or it falls
through returning 0 (integer default), blocking renders even in D-02 record-only mode.

**Root cause:** Adding enum values to `QuotaType` requires updating the switch in
`InMemoryTenantQuotaTracker.GetLimit` (line 44, `src/Muonroi.Quota.Abstractions/InMemoryTenantQuotaTracker.cs`).

**How to avoid:** Add `QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay` (with
`TenantQuota.MaxPdfRendersPerDay = int.MaxValue` defaulting to unlimited). Also add a corresponding
`InMemoryTenantQuotaStore` property / `TenantQuotaPresets.Free` entry.

### Pitfall 4: `MComplianceExportService` reads `pdf.template.*` only if an `IMControlPlaneStore` exposes them

**What goes wrong:** The compliance service already polls `IEnumerable<IMControlPlaneStore>` (line
13, `MComplianceExportService` constructor). But `PdfTemplateRegistryService` writes to
`IRuleSetAuditStore`, not to an `IMControlPlaneStore`. If no adapter is registered, the 6 audit
events are invisible to the evidence pack.

**Root cause:** `IMControlPlaneStore` returns a `MControlPlaneRegistry` with an `AuditTrail` list
of `MControlPlaneAuditRecord`. No existing implementation bridges the rule-engine audit store
(`IRuleSetAuditStore`) to the governance compliance store.

**How to avoid:** Implement `PdfAuditControlPlaneStore : IMControlPlaneStore` in the control-plane
that queries `IRuleSetAuditStore` for entries with `Action.StartsWith("pdf.template.")` and maps
them to `MControlPlaneAuditRecord`. Register as `services.AddSingleton<IMControlPlaneStore,
PdfAuditControlPlaneStore>()`.

### Pitfall 5: canary rollout ID not available at `/api/canary/pdf/score` call time

**What goes wrong:** The existing `ScorePdfSsimAsync` endpoint (confirmed
`CanaryEndpoints.cs` lines 46–112) accepts multipart PNG bytes but has no `rolloutId` parameter.
D-04 needs a rollout ID to call `RollbackCanaryAsync`. If the caller forgets to include it, the
auto-rollback cannot fire.

**Root cause:** Phase 9 designed the score endpoint as a pure calculator; rollback was manual.
Phase 16 must add `rolloutId` as an optional query parameter (or form field) and conditionally
invoke rollback when SSIM < threshold AND `rolloutId` is provided.

**How to avoid:** Add `Guid? rolloutId = null` query parameter. Document that when provided and
SSIM < threshold, rollback fires automatically. When omitted, endpoint remains pure calculator.

### Pitfall 6: Strong-naming mismatch if Muonroi.snk is not present in CI

**What goes wrong:** `Muonroi.Pdf.Enterprise.csproj` has `<SignAssembly>true</SignAssembly>` and
`<AssemblyOriginatorKeyFile>../../Muonroi.snk</AssemblyOriginatorKeyFile>`. When the csproj adds
a `ProjectReference` to `Muonroi.Governance.Enterprise` (which is also strong-named), both must
use the same key file. The key is at repo root `Muonroi.snk`.

**How to avoid:** Verify `../../Muonroi.snk` is accessible from `src/Muonroi.Pdf.Enterprise/`
before adding the project reference. It is (the key is at the solution root, 2 levels up from
`src/`). No change needed — same relative path applies.

---

## Code Examples

### D-01: LicenseFeatureGate construction + DI

```csharp
// src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs
// Source: ILicenseGuard.cs, FeatureNotLicensedException.cs (both verified by direct read)

using Muonroi.Governance.License;

namespace Muonroi.Pdf.Enterprise.License;

public sealed class LicenseFeatureGate(ILicenseGuard licenseGuard) : IFeatureGate
{
    public bool IsEnabled(string capabilityKey)
        => licenseGuard.HasFeature(capabilityKey);

    public void EnsureFeatureOrThrow(string capabilityKey)
    {
        if (!licenseGuard.HasFeature(capabilityKey))
            throw new FeatureNotLicensedException(capabilityKey);
    }
}
```

```csharp
// src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs
// Source: pattern from EnterpriseGovernanceServiceExtensions.cs

using Muonroi.Pdf.Enterprise.License;
using Muonroi.Pdf.Enterprise.Metering;

namespace Muonroi.Pdf.Enterprise.Extensions;

public static class PdfEnterpriseServiceExtensions
{
    public static IServiceCollection AddPdfEnterprise(this IServiceCollection services)
    {
        // Replaces AlwaysAllowFeatureGate with the real gate.
        // AddMEnterpriseGovernance must be called first (registers ILicenseGuard).
        services.TryAddSingleton<IFeatureGate, LicenseFeatureGate>();
        services.TryAddSingleton<EnterprisePdfServiceWrapper>();
        return services;
    }
}
```

### D-01: `LicenseCapabilityResolver` additions

```csharp
// In src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs
// (verified: file read lines 14-95)

public static class Capabilities
{
    // ... existing constants ...
    public const string PdfDesigner = "pdf.designer";   // NEW
    public const string PdfRegistry = "pdf.registry";   // NEW
    public const string PdfCanary   = "pdf.canary";     // NEW
}

// In CapabilityKeys HashSet — append the 3 new values.
// In FeatureToCapability dict — append:
//   [Capabilities.PdfDesigner] = Capabilities.PdfDesigner,
//   [Capabilities.PdfRegistry] = Capabilities.PdfRegistry,
//   [Capabilities.PdfCanary]   = Capabilities.PdfCanary,
// (identity mapping: the pdf.* key IS the capability key, same as Connectors pattern)
```

### D-02: `QuotaType` + `InMemoryTenantQuotaTracker`

```csharp
// In src/Muonroi.Quota.Abstractions/QuotaType.cs  (verified: file read)
/// <summary>Maximum number of PDF page-render events per day.</summary>
PdfRendersPerDay,  // NEW — append to enum

// In InMemoryTenantQuotaTracker.GetLimit switch  (verified: line 44)
QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay,  // NEW arm

// In TenantQuota.cs — new property:
public int MaxPdfRendersPerDay { get; set; } = int.MaxValue; // unlimited default
```

### D-04: Score endpoint rollback extension

```csharp
// In muonroi-control-plane/src/Host/.../Endpoints/CanaryEndpoints.cs
// Extend ScorePdfSsimAsync signature (verified: existing file lines 46-112)

private static async Task<IResult> ScorePdfSsimAsync(
    IFormFile baseline,
    IFormFile candidate,
    ICanaryRolloutService canaryService,         // NEW injection
    IOptions<PdfCanaryOptions> canaryOptions,    // NEW injection
    Guid? rolloutId,                             // NEW optional query param
    CancellationToken cancellationToken)
{
    // ... existing decode + score logic ...

    bool autoRolledBack = false;
    if (rolloutId.HasValue && rolloutId.Value != Guid.Empty
        && ssim < canaryOptions.Value.SsimThreshold)
    {
        await canaryService.RollbackCanaryAsync(
            rolloutId.Value,
            "system",
            $"SSIM {ssim:F4} below threshold {canaryOptions.Value.SsimThreshold}",
            cancellationToken);
        autoRolledBack = true;
    }

    return Results.Ok(new
    {
        Ssim           = ssim,
        Width          = decodedBaseline.widthA,
        Height         = decodedBaseline.heightA,
        AutoRolledBack = autoRolledBack,
        Threshold      = canaryOptions.Value.SsimThreshold
    });
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AlwaysAllowFeatureGate` (no-op) | `LicenseFeatureGate` binding `ILicenseGuard` | Phase 16 D-01 | Unlicensed `pdf.*` throws `FeatureNotLicensedException` |
| Manual canary rollback (operator calls `/canary/{id}/rollback`) | Auto-rollback triggered by SSIM < threshold in `POST /api/canary/pdf/score` | Phase 16 D-04 | SC2 (`PARTIAL` → `SATISFIED`) |
| No render metering | `ITenantQuotaTracker.IncrementUsageAsync(tenantId, PdfRendersPerDay, pageCount)` | Phase 16 D-02 | Billing/analytics data available; no hard cap yet |
| 6 audit events in `IRuleSetAuditStore` only | Also surfaced via `IMControlPlaneStore` into compliance evidence pack | Phase 16 D-03 | `pdf.template.*` events included in tenant compliance exports |

**Deprecated/outdated:**

- `AlwaysAllowFeatureGate.Instance` — remains available for unit tests (direct construction, not DI),
  but must NOT be registered as the DI binding in production.

---

## Open Questions (RESOLVED)

1. **`ITenantContext` vs `TenantContext.CurrentTenantId` for metering tenant resolution**
   - What we know: `EnterpriseLicenseGuardEnhancer` uses `ISystemExecutionContextAccessor` first,
     falls back to `TenantContext.CurrentTenantId` (AsyncLocal). Both paths are tested.
   - What's unclear: Which is correct for the `EnterprisePdfServiceWrapper` — inject
     `ISystemExecutionContextAccessor` or read `TenantContext.CurrentTenantId` directly?
   - Recommendation: Mirror `EnterpriseLicenseGuardEnhancer`: inject `ISystemExecutionContextAccessor?`
     and fall back to `TenantContext.CurrentTenantId`. This is the established convention.

2. **`PdfAuditControlPlaneStore` polling strategy for D-03**
   - What we know: `MComplianceExportService` calls `IMControlPlaneStore.Load()` (synchronous),
     which returns `MControlPlaneRegistry` with `AuditTrail: List<MControlPlaneAuditRecord>`.
   - What's unclear: `IRuleSetAuditStore` is async (`IAsyncEnumerable` or `Task<IReadOnlyList<>>`).
     `Load()` must be synchronous. Does `IRuleSetAuditStore` have a sync read path?
   - Recommendation: Verify `IRuleSetAuditStore` API surface; if async-only, use `.GetAwaiter().GetResult()`
     inside `Load()` with a cancellation-free call, or cache the last results from a background
     `IHostedService` that fills an in-memory list.

3. **Version bump strategy for modified shared packages** — RESOLVED
   - What we know: `LicenseCapabilityResolver` is in `Muonroi.Governance.Abstractions` and
     `QuotaType` is in `Muonroi.Quota.Abstractions` — both OSS packages. Verified: all building-block
     package versions are governed centrally by `<VersionPrefix>1.0.0</VersionPrefix>` +
     `<VersionSuffix>alpha.15</VersionSuffix>` in `muonroi-building-block/Directory.Build.props` (single
     unified prerelease version across the ecosystem), and `VERSION_GOVERNANCE.md` (at workspace root
     `D:/sources/Core/VERSION_GOVERNANCE.md`) governs third-party NuGet versions via CPM but defines no
     separate first-party assembly-version SemVer track — the whole repo ships under one coordinated
     `1.0.0-alpha.NN` tag.
   - **Resolution:** These are additive, backward-compatible public-surface changes (new enum member,
     new constants, new dictionary/HashSet entries — no removals, no signature changes). For a unified
     `1.0.0-alpha.NN` prerelease governed centrally in `Directory.Build.props`, the correct action is to
     bump `VersionSuffix` (alpha.15 → alpha.16) ONCE at the next coordinated alpha cut for the whole
     ecosystem, rather than per-package. Plans 16-01 and 16-02 each carry an acceptance criterion: the
     version bump is applied per this policy OR explicitly deferred (with the stated reason that the phase
     ships under the current alpha and the suffix bumps at the next coordinated cut). The chosen path MUST
     be recorded in each plan's SUMMARY. No `Version=` attribute may be added to any `.csproj` (CPM
     enforces NU1011).

---

## Environment Availability

> Step 2.6: This phase modifies existing in-repo source files only. No external tools, services,
> or runtimes beyond the existing .NET 8 SDK and existing control-plane/license-server repos are
> required. The `Muonroi.snk` strong-name key must be present at `D:/sources/Core/muonroi-building-block/Muonroi.snk`.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | All C# compilation | Verified (existing build) | net8.0 TFM | — |
| `Muonroi.snk` | `Muonroi.Pdf.Enterprise` strong-naming | Must verify path `../../Muonroi.snk` from `src/Muonroi.Pdf.Enterprise/` | — | Build will fail without it |
| `muonroi-control-plane` repo | D-03, D-04 | Present at `D:/sources/Core/muonroi-control-plane/` | — | — |
| `muonroi-license-server` repo | WS-D (confirm only) | Present at `D:/sources/Core/muonroi-license-server/` | — | — |

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (building-block), xUnit (control-plane) |
| Config file | `tests/Muonroi.Pdf.Enterprise.Tests/` (create if absent) |
| Quick run command | `dotnet test tests/Muonroi.Pdf.Enterprise.Tests/ -x` |
| Full suite command | `dotnet test Muonroi.Pdf.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LIC-01 | `LicenseFeatureGate.EnsureFeatureOrThrow("pdf.designer")` throws `FeatureNotLicensedException` when not in proof | unit | `dotnet test tests/Muonroi.Pdf.Enterprise.Tests/ -x --filter "LicenseFeatureGate"` | Wave 0 |
| LIC-02 | `IMPdfService.RenderAsync` is NOT gated; OSS render proceeds without license | unit | `dotnet test tests/Muonroi.Pdf.Tests/` | Existing |
| D-02 | `EnterprisePdfServiceWrapper` calls `IncrementUsageAsync` after render | unit | `dotnet test tests/Muonroi.Pdf.Enterprise.Tests/ -x --filter "Metering"` | Wave 0 |
| D-02 | Metering failure (tracker throws) does NOT propagate to caller | unit | same | Wave 0 |
| D-03 | `PdfAuditControlPlaneStore.Load()` returns `pdf.template.*` events from audit store | unit | `dotnet test tests/Muonroi.ControlPlane.Host.Tests/ -x --filter "PdfAuditControlPlane"` | Wave 0 |
| D-04 | `POST /api/canary/pdf/score` with SSIM < threshold + rolloutId triggers `RollbackCanaryAsync` | unit | `dotnet test tests/Muonroi.ControlPlane.Host.Tests/ -x --filter "PdfCanary"` | Extend existing `PdfCanaryScoringTests.cs` |
| D-04 | Score endpoint without rolloutId returns score only, no rollback | unit | same | same |

### Wave 0 Gaps

- [ ] `tests/Muonroi.Pdf.Enterprise.Tests/LicenseFeatureGateTests.cs` — covers LIC-01
- [ ] `tests/Muonroi.Pdf.Enterprise.Tests/EnterprisePdfServiceWrapperTests.cs` — covers D-02
- [ ] `tests/Muonroi.ControlPlane.Host.Tests/PdfAuditControlPlaneStoreTests.cs` — covers D-03
- [ ] Extend `tests/Muonroi.ControlPlane.Host.Tests/PdfCanaryScoringTests.cs` — covers D-04

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | yes | `IFeatureGate.EnsureFeatureOrThrow` on all Enterprise entry points |
| V5 Input Validation | yes | `rolloutId` Guid parsing in canary endpoint (already done by ASP.NET route constraint `{rolloutId:guid}`) |
| V6 Cryptography | yes | RSA ActivationProof verification — handled by existing `LicenseVerifier`; never hand-rolled |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Bypassing feature gate by injecting `AlwaysAllowFeatureGate` | Elevation of privilege | `AddPdfEnterprise` registers `LicenseFeatureGate` with `TryAddSingleton` — host cannot accidentally override with the no-op if called in the right order |
| Spoofed ActivationProof (tampered JSON file) | Tampering | `LicenseVerifier` checks RSA signature on load; tampered proof results in `LicenseState.IsValid = false` |
| Quota record suppression (attacker swallows metering) | Repudiation | D-02 is record-only; non-blocking failure is logged via `IMLog` — audit trail is independent |
| SSIM score manipulation (bypass auto-rollback) | Tampering | Score is computed server-side from caller-supplied PNG bytes; caller cannot supply a fake score — they can supply manipulated images, but SSIM math is deterministic |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `QuotaType.PdfRendersPerDay` does not yet exist in the enum | D-02, QuotaType.cs | If it already exists (added elsewhere), adding it again would cause a compile error — easy to detect |
| A2 | `IRuleSetAuditStore` entries are accessible from the control-plane process at compliance export time (same DB context) | D-03 | If they are in a separate DB or separate process, the adapter design needs a different fetch strategy |
| A3 | `TenantQuota.MaxPdfRendersPerDay` property does not yet exist | D-02 | Same risk as A1 |

**Verified claims (not assumed):**

- `LicenseCapabilityResolver.CapabilityKeys` and `FeatureToCapability` do NOT contain `pdf.*` (verified: file read lines 81-95)
- `MEnterpriseFailClosedMatrix.BlocksAllEnterpriseCapabilities` does NOT contain `pdf.*` (verified: file read lines 39-51)
- `QuotaType` enum has no PDF member (verified: file read, 13 existing values)
- `AlwaysAllowFeatureGate` is a no-op singleton (verified: file read)
- `ActivationProof.Features[]` is a plain `string[]` written by the license-server claim-agnostic pipeline (verified: `KnownPdfCapabilities.cs` + `ActivationProof.cs`)
- `ICanaryRolloutService.RollbackCanaryAsync(Guid, string, string, CancellationToken)` exists (verified: file read)
- `PdfCanaryOptions.SsimThreshold = 0.95` exists in control-plane (verified: file read)
- 6 audit event strings are `PdfTemplateAuditActions.{Created/Updated/Submitted/Approved/Rejected/Activated}` (verified: file read)
- `MComplianceExportService` constructor takes `IEnumerable<IMControlPlaneStore>` (verified: file read line 13)
- `Muonroi.Pdf.Enterprise.csproj` currently has NO reference to `Muonroi.Governance.Enterprise` (verified: file read — only `ProjectReference` is `Muonroi.Pdf`)
- `LicenseState.ActivationProof` is available as a DI singleton via `LicenseState` (verified: `LicenseState.cs` + `LicenseServiceCollectionExtensions.cs` line 69)

---

## Sources

### Primary (HIGH confidence — direct file reads, all verified in this session)

| File | Key Finding |
|------|-------------|
| `src/Muonroi.Pdf.Enterprise/IFeatureGate.cs` | `IsEnabled(key): bool`, `EnsureFeatureOrThrow(key): void` — exact interface signatures |
| `src/Muonroi.Pdf.Enterprise/AlwaysAllowFeatureGate.cs` | No-op singleton, comment says "Real binding lands in Phase 9.4" — never landed |
| `src/Muonroi.Pdf.Enterprise/CapabilityKeys.cs` | `pdf.designer`, `pdf.registry`, `pdf.canary` confirmed |
| `src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj` | Only `ProjectReference` = `Muonroi.Pdf`; no Governance.Enterprise ref |
| `src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs` | `pdf.*` ABSENT from `CapabilityKeys` and `FeatureToCapability` |
| `src/Muonroi.Governance.Abstractions/License/LicenseState.cs` | `ActivationProof?` property, `HasFeature(name)` delegates to `LicenseCapabilityResolver.HasAccess` |
| `src/Muonroi.Governance.Abstractions/License/ActivationProof.cs` | `Features: string[]` — claim-agnostic, verbatim from license-server |
| `src/Muonroi.Governance.Enterprise/License/MEnterpriseFailClosedMatrix.cs` | `pdf.*` ABSENT from `BlocksAllEnterpriseCapabilities` |
| `src/Muonroi.Governance.Enterprise/EnterpriseGovernanceServiceExtensions.cs` | `AddMEnterpriseGovernance` DI extension — model for `AddPdfEnterprise` |
| `src/Muonroi.Governance.Enterprise/Compliance/IMComplianceEvidencePackService.cs` | `GenerateAsync(request, ct): Task<MComplianceEvidencePackResult>` |
| `src/Muonroi.Governance.Enterprise/Compliance/MComplianceContracts.cs` | Full contract types verified |
| `src/Muonroi.Governance.Enterprise/Compliance/MComplianceEvidencePackService.cs` | Requires `IMComplianceExportService.IsEnabled` before generating |
| `src/Muonroi.Governance.Enterprise/Compliance/MComplianceExportService.cs` | Constructor takes `IEnumerable<IMControlPlaneStore>` — adapter injection point for D-03 |
| `src/Muonroi.Governance.Enterprise/ControlPlane/IControlPlaneStore.cs` | `IMControlPlaneStore.Load(): MControlPlaneRegistry`, `Save(registry): void` |
| `src/Muonroi.Quota.Abstractions/ITenantQuotaTracker.cs` | `IncrementUsageAsync(tenantId, type, amount, ct): Task` |
| `src/Muonroi.Quota.Abstractions/QuotaType.cs` | 13 existing values; NO `PdfRendersPerDay` |
| `src/Muonroi.Quota.Abstractions/InMemoryTenantQuotaTracker.cs` | `GetLimit` switch must be extended for new enum value |
| `src/Muonroi.RuleEngine.Abstractions/Rules/ICanaryRolloutService.cs` | `RollbackCanaryAsync(Guid, string, string, ct)` confirmed |
| `src/Muonroi.RuleEngine.Abstractions/Rules/CanaryRolloutRecord.cs` | `Id`, `WorkflowName`, `Status`, `RollbackReason` fields confirmed |
| `muonroi-control-plane/src/.../Endpoints/CanaryEndpoints.cs` | `POST /api/canary/pdf/score` exists; no rolloutId param; no auto-rollback |
| `muonroi-control-plane/src/.../Options/PdfCanaryOptions.cs` | `SsimThreshold = 0.95`, `SectionName = "PdfCanary"` |
| `muonroi-control-plane/src/.../Services/PdfTemplates/PdfTemplateAuditActions.cs` | All 6 constants confirmed |
| `muonroi-license-server/src/.../KnownPdfCapabilities.cs` | `pdf.designer/registry/canary` confirmed; `All` static list |
| `.planning/PHASE-09-CLOSEOUT.md` | SC2 `PARTIAL`, 11 REST endpoints, `PdfTemplateRegistryService`, 6 audit events, `SsimScorer`, `KnownPdfCapabilities` all verified shipped |

---

## Metadata

**Confidence breakdown:**

| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | All APIs verified by direct file read |
| Architecture | HIGH | Data flow confirmed by reading both producer and consumer code |
| Pitfalls | HIGH | Gaps confirmed by direct negative evidence (string not in set, field not in enum) |
| D-03 adapter strategy | MEDIUM | `IMControlPlaneStore.Load()` is sync; `IRuleSetAuditStore` API surface not fully read |

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 (stable in-repo code; no fast-moving external dependencies)
