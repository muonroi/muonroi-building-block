---
phase: 16-pdf-enterprise-governance-controlplane-integration
verified: 2026-06-21T10:00:00Z
status: passed
score: 14/14 must-haves verified (1 resolved by developer approval)
overrides_applied: 0
human_verification_resolved:
  - test: "D-04 rollback test coverage uses inline helper, not the actual ScorePdfSsimAsync handler"
    resolution: "APPROVED by developer 2026-06-21. Implementation verified correct (handler matches the inline test logic exactly; SC2 rollback behavior proven by code review + replicated-logic tests). Inline-helper replication accepted as sufficient to close SC2 for this phase."
    follow_up: "DEFERRED: add an HTTP-level / WebApplicationFactory integration test that invokes POST /api/canary/pdf/score with a rolloutId and asserts ICanaryRolloutService.RollbackCanaryAsync fires when ssim < threshold — to guard against future endpoint-wiring regressions (parameter binding / route / guard). Not blocking phase close."
---

# Phase 16: PDF Enterprise Governance + ControlPlane Integration — Verification Report

**Phase Goal:** Deepen Muonroi.Pdf.Enterprise from the thin v1.0 stubs (Phase 9) into the shared Muonroi enterprise rails (licensing, anti-tamper, audit/compliance, quota, SLO) with ZERO changes to the OSS engine Muonroi.Pdf. Gap-closure on Phase 9, not a rebuild.
**Verified:** 2026-06-21
**Status:** passed (1 human-verify item resolved by developer approval — see frontmatter `human_verification_resolved`)
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Unlicensed pdf.designer/registry/canary throws FeatureNotLicensedException via LicenseFeatureGate.EnsureFeatureOrThrow | ✓ VERIFIED | `src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs` lines 22-32: `EnsureFeatureOrThrow` calls `licenseGuard.HasFeature` and throws `new FeatureNotLicensedException(capabilityKey)` on false |
| 2 | Licensed pdf.* capability passes (IsEnabled returns true) when ActivationProof.Features[] contains the key | ✓ VERIFIED | `LicenseFeatureGate.IsEnabled` line 18-19 delegates directly to `licenseGuard.HasFeature`; LicenseFeatureGateTests.cs Test 2 and Test 4 cover this path with LicenseTier.Licensed state |
| 3 | OSS Muonroi.Pdf references nothing under *.Enterprise (SC5 one-way boundary intact) | ✓ VERIFIED | `src/Muonroi.Pdf/Muonroi.Pdf.csproj` grep for "Enterprise" returns no matches; `git status src/Muonroi.Pdf/` shows nothing to commit — no changes in this phase |
| 4 | Muonroi.Pdf.Enterprise.csproj references Muonroi.Governance.Enterprise | ✓ VERIFIED | `Muonroi.Pdf.Enterprise.csproj` lines 22-23: explicit `<ProjectReference Include="..\Muonroi.Governance.Enterprise\Muonroi.Governance.Enterprise.csproj" />` with SC5 comment |
| 5 | pdf.designer/pdf.registry/pdf.canary present in all FOUR parallel governance registries | ✓ VERIFIED | `LicenseCapabilityResolver.cs`: consts at lines 64-68, FeatureToCapability at lines 85-87, CapabilityKeys at lines 104-106; `MEnterpriseFailClosedMatrix.cs`: BlocksAllEnterpriseCapabilities lines 51-53 |
| 6 | AddPdfEnterprise binds LicenseFeatureGate via TryAddSingleton (not AlwaysAllowFeatureGate) | ✓ VERIFIED | `PdfEnterpriseServiceExtensions.cs` line 51: `services.TryAddSingleton<IFeatureGate, LicenseFeatureGate>()`; LicenseFeatureGateTests.cs Test 3 asserts resolved type is `LicenseFeatureGate` and is NOT `AlwaysAllowFeatureGate` |
| 7 | EnterprisePdfServiceWrapper records one metered event per render; a metering failure never blocks the render | ✓ VERIFIED | `EnterprisePdfServiceWrapper.cs` lines 66-85: `RecordMeteringAsync` calls `IncrementUsageAsync(tenantId, QuotaType.PdfRendersPerDay, pageCount, ct)` in a try/catch that logs and swallows; Tests 1 and 2 in EnterprisePdfServiceWrapperTests.cs cover both paths |
| 8 | sp.GetRequiredService<IMPdfService>() resolves to EnterprisePdfServiceWrapper (SC3 active wiring) | ✓ VERIFIED | `PdfEnterpriseServiceExtensions.cs` lines 55-80: hand-decoration captures and removes prior IMPdfService descriptor, re-registers via factory constructing `EnterprisePdfServiceWrapper`; EnterprisePdfServiceWrapperTests.cs Test 4 (line 253-271) asserts `Assert.IsType<EnterprisePdfServiceWrapper>(resolved)` |
| 9 | PdfAuditControlPlaneStore.Load() returns pdf.template.* audit events only; registered as IMControlPlaneStore | ✓ VERIFIED | `PdfAuditControlPlaneStore.cs` line 54: `entry.Action.StartsWith(PdfTemplatePrefix, StringComparison.OrdinalIgnoreCase)` where `PdfTemplatePrefix = "pdf.template."`; `Program.cs` line 347: `builder.Services.AddSingleton<IMControlPlaneStore, PdfAuditControlPlaneStore>()`; 7 tests in PdfAuditControlPlaneStoreTests.cs |
| 10 | Render-time audit NOT included (D-03 deferred: only pdf.template.* publish events) | ✓ VERIFIED | `PdfAuditControlPlaneStore.cs` contains no render-event source; only queries `IRuleSetAuditStore` and filters on "pdf.template." prefix |
| 11 | D-04: POST /api/canary/pdf/score with rolloutId and SSIM < threshold calls RollbackCanaryAsync and returns AutoRolledBack=true | ✓ VERIFIED (code path only — see human verification) | `CanaryEndpoints.cs` lines 51-138: signature includes `ICanaryRolloutService canaryService`, `IOptions<PdfCanaryOptions> canaryOptions`, `Guid? rolloutId`; conditional block lines 119-128 calls `canaryService.RollbackCanaryAsync(...)` and sets `autoRolledBack = true` when `rolloutId.HasValue && ssim < SsimThreshold` |
| 12 | D-04: without rolloutId, endpoint is a pure calculator and does NOT call RollbackCanaryAsync | ✓ VERIFIED | Lines 118-128 of CanaryEndpoints.cs: guard requires `rolloutId.HasValue`; PdfCanaryScoringTests Test 2 asserts `RollbackCalls.Should().BeEmpty()` |
| 13 | RequireCapability gates pdf.registry and pdf.canary (not only pdf.designer) | ✓ VERIFIED | `RequireCapability.test.tsx` lines 116-216: 4 test cases for pdf.registry (locked/mentions-key/children-granted/children-denied) and 4 for pdf.canary; `RequireCapability.tsx` unchanged (generic over capability string) |
| 14 | KnownPdfCapabilities (license-server) recognizes all three pdf.* keys | ✓ VERIFIED | `KnownPdfCapabilities.cs` lines 14-34: PdfDesigner, PdfRegistry, PdfCanary constants; `All` contains all three; `IsKnown` uses pattern-match returning true for all three |

**Score:** 13/14 truths verified (truth 11 has code-path evidence only; the test exercises an inline helper, not the live handler — escalated to human verification)

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs` | Real IFeatureGate bound to ILicenseGuard | ✓ VERIFIED | Exists; sealed primary-ctor class; `throw new FeatureNotLicensedException` at line 29; `licenseGuard.HasFeature` at lines 18 and 24 |
| `src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs` | AddPdfEnterprise registering LicenseFeatureGate + wrapper | ✓ VERIFIED | Exists; `TryAddSingleton<IFeatureGate, LicenseFeatureGate>` at line 51; `AddSingleton<IMPdfService>(sp => ... new EnterprisePdfServiceWrapper(...)` at line 69 |
| `src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs` | IMPdfService decorator metering page count | ✓ VERIFIED | Exists; implements all three IMPdfService methods; `IncrementUsageAsync` with `QuotaType.PdfRendersPerDay` at line 77; try/catch with logger.Error at line 83 |
| `src/Muonroi.Quota.Abstractions/QuotaType.cs` | Contains PdfRendersPerDay | ✓ VERIFIED | Line 35: `PdfRendersPerDay` with XML-doc noting record-only, no hard cap in Phase 16 |
| `src/Muonroi.Quota.Abstractions/TenantQuota.cs` | MaxPdfRendersPerDay = int.MaxValue in all 4 presets | ✓ VERIFIED | Line 96: property with `= int.MaxValue`; lines 161, 193, 225, 257: all four presets set to `int.MaxValue` |
| `src/Muonroi.Quota.Abstractions/InMemoryTenantQuotaTracker.cs` | GetLimit switch arm for PdfRendersPerDay | ✓ VERIFIED | Line 58: `QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay` |
| `muonroi-control-plane/src/Host/.../Services/Compliance/PdfAuditControlPlaneStore.cs` | IMControlPlaneStore adapter | ✓ VERIFIED | Exists; implements `IMControlPlaneStore`; `StartsWith("pdf.template.")` at line 54; `logger?.LogError(...)` in catch at line 47-48 |
| `muonroi-control-plane/tests/.../PdfAuditControlPlaneStoreTests.cs` | D-03 coverage (7 tests) | ✓ VERIFIED | Exists; covers filter/field-mapping/error-resilience/ordering/save-noop |
| `muonroi-control-plane/src/Host/.../Endpoints/CanaryEndpoints.cs` | ScorePdfSsimAsync with optional rolloutId + conditional rollback | ✓ VERIFIED | Modified at commit de7c210; `RollbackCanaryAsync(` at line 122; `SsimThreshold` at lines 120 and 125 |
| `muonroi-control-plane/tests/.../PdfCanaryScoringTests.cs` | D-04 rollback/no-rollback coverage | ⚠ PARTIAL | Exists with 3 new test methods; tests use `InvokeScoreWithRollbackLogicAsync` (inline helper that replicates handler logic) rather than invoking the actual `ScorePdfSsimAsync` endpoint |
| `muonroi-ui-engine/.../tests/license/RequireCapability.test.tsx` | pdf.registry and pdf.canary gating tests | ✓ VERIFIED | 8 new test cases (4 per capability) at lines 116-216; commit 3dae4fb; RequireCapability.tsx unchanged |
| `tests/Muonroi.Pdf.Governance.Tests/LicenseFeatureGateTests.cs` | LIC-01 coverage (4 tests) | ✓ VERIFIED | Exists; 4 tests covering throw/pass/DI-binding/Licensed-tier registry |
| `tests/Muonroi.Pdf.Governance.Tests/EnterprisePdfServiceWrapperTests.cs` | D-02 metering + non-blocking-failure coverage (6 tests) | ✓ VERIFIED | Exists; Tests 1-4 plus 2 quota tracker tests |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `LicenseFeatureGate.cs` | `ILicenseGuard.HasFeature` | Constructor injection + delegation | ✓ WIRED | Lines 15-19, 24: `licenseGuard.HasFeature(capabilityKey)` called in both `IsEnabled` and `EnsureFeatureOrThrow` |
| `MEnterpriseFailClosedMatrix.cs` | `LicenseCapabilityResolver.Capabilities.Pdf*` | BlocksAllEnterpriseCapabilities OR-chain | ✓ WIRED | Lines 51-53: all three `.Equals(...)` clauses present |
| `EnterprisePdfServiceWrapper.cs` | `ITenantQuotaTracker.IncrementUsageAsync` | Post-render call in try/catch | ✓ WIRED | Line 77: `IncrementUsageAsync(tenantId, QuotaType.PdfRendersPerDay, pageCount, ct)` |
| `InMemoryTenantQuotaTracker.cs` | `TenantQuota.MaxPdfRendersPerDay` | GetLimit switch arm | ✓ WIRED | Line 58: `QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay` |
| `PdfEnterpriseServiceExtensions.cs` | `IMPdfService` (active binding) | Hand-decorator: remove prior descriptor, re-register as factory | ✓ WIRED | Lines 55-80: `services.Remove(innerDescriptor)` + `services.AddSingleton<IMPdfService>(sp => new EnterprisePdfServiceWrapper(...))` |
| `PdfAuditControlPlaneStore.Load` | `IRuleSetAuditStore.QueryAsync` | `GetAwaiter().GetResult()` sync bridge + Action.StartsWith filter | ✓ WIRED | Lines 39-43: `.QueryAsync(...).GetAwaiter().GetResult()` then filter at line 54 |
| `Program.cs` | `PdfAuditControlPlaneStore` | `AddSingleton<IMControlPlaneStore, PdfAuditControlPlaneStore>` | ✓ WIRED | Line 347 of Program.cs confirmed |
| `CanaryEndpoints.ScorePdfSsimAsync` | `ICanaryRolloutService.RollbackCanaryAsync` | Conditional call when ssim < SsimThreshold and rolloutId present | ✓ WIRED (code path confirmed) | Lines 119-127: correct guard and call; tests verify via logic replication |
| `CanaryEndpoints.ScorePdfSsimAsync` | `PdfCanaryOptions.SsimThreshold` | IOptions<PdfCanaryOptions> injection | ✓ WIRED | Lines 55-57, 120: `canaryOptions.Value.SsimThreshold` used in condition and reason string |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `EnterprisePdfServiceWrapper` | `result.PageCount` | Delegates to `inner.RenderAsync` which is the real `MPdfService` | Yes — inner result from OSS engine is passed to metering | ✓ FLOWING |
| `PdfAuditControlPlaneStore` | `page.Items` (audit entries) | `IRuleSetAuditStore.QueryAsync` (real EF/PostgreSQL-backed store in control-plane) | Yes — real audit events from DB | ✓ FLOWING |
| `LicenseFeatureGate` | `licenseGuard.HasFeature(...)` result | `ILicenseGuard` backed by `LicenseState` from RSA-verified `ActivationProof` | Yes — real ActivationProof chain | ✓ FLOWING |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| LicenseFeatureGate throws on unlicensed pdf.designer | Codebase check: `LicenseFeatureGateTests.cs` Test 1 — FakeLicenseGuard returns false; `Assert.Throws<FeatureNotLicensedException>` | Test exists and is substantive | ✓ PASS |
| EnterprisePdfServiceWrapper is active IMPdfService | Codebase check: `EnterprisePdfServiceWrapperTests.cs` Test 4 — `Assert.IsType<EnterprisePdfServiceWrapper>(resolved)` | Test exists and asserts type | ✓ PASS |
| PdfAuditControlPlaneStore filters non-pdf events | Codebase check: `PdfAuditControlPlaneStoreTests.cs` — verified 7 tests including filter test | Tests exist and cover filter | ✓ PASS |
| Metering failure never blocks render | Codebase check: `EnterprisePdfServiceWrapperTests.cs` Test 2 — `ThrowingQuotaTracker` → render completes | Test exists | ✓ PASS |

---

### Probe Execution

No probes declared or discovered in this phase. Step 7c: SKIPPED (no probe-*.sh scripts for this phase).

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| SC1 | 16-01 | LicenseFeatureGate bound; pdf.* in all 4 registries; OSS untouched | ✓ SATISFIED | LicenseFeatureGate.cs, PdfEnterpriseServiceExtensions.cs, LicenseCapabilityResolver.cs, MEnterpriseFailClosedMatrix.cs all verified |
| SC2 | 16-04, 16-05 | Canary auto-rollback on SSIM < threshold; per-tenant publish propagation | ⚠ PARTIAL | Handler code path verified by code review (lines 119-128); test coverage uses inline logic replication not the live handler. SC2 cache-isolation: confirmed via SignalRPdfTemplateChangeNotifier lines 28-34 (per-tenant group + opt-in AllTenantsGroup) |
| SC3 | 16-02, 16-03 | Per-tenant render metering via Muonroi.Quota as active IMPdfService; compliance evidence pack includes pdf.template.* events | ✓ SATISFIED | EnterprisePdfServiceWrapper wired as active IMPdfService (Test 4 confirmed); PdfAuditControlPlaneStore registered AddSingleton as IMControlPlaneStore |
| SC4 | 16-05 | Full suites green; OSS engine byte-identical (no golden re-baseline) | ✓ SATISFIED (executor claim — spot-verified) | SUMMARY claims 1,724/0 building-block; 479/0 control-plane; 54/0 ui-engine. OSS engine: `git status src/Muonroi.Pdf/` confirmed clean. Spot-verified: all test files exist and are substantive; no stubs found. Note: verifier did not re-run `dotnet test` (trusting executor SUMMARY per instructions with code-level spot-verification). |
| SC5 (boundary) | All plans | One-way Enterprise→OSS; no OSS source touched | ✓ SATISFIED | `Muonroi.Pdf.csproj` grep for "Enterprise" returns no matches; `git status src/Muonroi.Pdf/` shows working tree clean |
| D-01 | 16-01 | Fail-closed; AlwaysAllowFeatureGate not in production DI | ✓ SATISFIED | TryAddSingleton<IFeatureGate, LicenseFeatureGate>; AlwaysAllowFeatureGate never registered in AddPdfEnterprise |
| D-02 | 16-02 | Record-only metering; never blocks render | ✓ SATISFIED | try/catch + logger.Error + swallow in RecordMeteringAsync; TenantQuota.MaxPdfRendersPerDay = int.MaxValue |
| D-03 | 16-03 | Publish/version events only in compliance pack; render-time audit deferred | ✓ SATISFIED | PdfAuditControlPlaneStore filters on "pdf.template." prefix; no render-event source |
| D-04 | 16-04 | Rollback policy at control-plane only; engine only scores | ✓ SATISFIED (code path) | CanaryEndpoints.cs modified; rollback logic confined to control-plane; SsimScorer.Compare unchanged |
| WS-C | 16-05 | RequireCapability gates pdf.registry and pdf.canary | ✓ SATISFIED | 8 new test cases in RequireCapability.test.tsx |
| WS-D | 16-05 | KnownPdfCapabilities confirms all three keys (read-only) | ✓ SATISFIED | KnownPdfCapabilities.cs verified directly |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `LicenseFeatureGate.cs` | 28-30 | `#pragma warning disable MSTD0001` | ℹ Info | Intentional and documented: `FeatureNotLicensedException : InvalidOperationException` by design; callers must catch without Governance dependency. Justification comment present. Not a stub. |
| `PdfEnterpriseServiceExtensions.cs` | 105-112 | `#pragma warning disable MSTD0001` | ℹ Info | Intentional: DI bootstrap invariant violation uses `InvalidOperationException` per DI idiom. Justification comment present. Not a stub. |
| `PdfCanaryScoringTests.cs` | 238-263 | `InvokeScoreWithRollbackLogicAsync` inline helper replicates handler logic | ⚠ Warning | Tests for D-04 don't call the actual `ScorePdfSsimAsync` handler. If the handler's wiring changes (e.g., parameter binding, condition guard), the tests won't catch it. Handler code reviewed at lines 119-128 and is correct, but this is a test quality gap. Escalated to human verification. |

No `TBD`, `FIXME`, or `XXX` markers found in any modified file.
No empty return values, hardcoded empty arrays, or placeholder implementations found in production code.
No render-time stubs in the metering or audit paths.

---

### Human Verification Required

#### 1. D-04 Canary Rollback — Test Coverage Gap

**Test:** In `muonroi-control-plane`, examine whether the three D-04 tests in `PdfCanaryScoringTests.cs` provide sufficient confidence that the actual `ScorePdfSsimAsync` endpoint handler calls `RollbackCanaryAsync` when SSIM < threshold. The tests currently call an inline helper `InvokeScoreWithRollbackLogicAsync` (lines 238-263) that is a copy of the handler logic, not the handler itself.

**Expected:** Either (a) the developer accepts that code review of CanaryEndpoints.cs lines 119-128 plus the inline-helper unit tests is sufficient evidence for SC2, OR (b) a WebApplicationFactory integration test or functional test that actually calls `POST /api/canary/pdf/score?rolloutId=<guid>` with multipart PNG data and asserts `AutoRolledBack=true` in the JSON response.

**Why human:** Programmatic verification cannot distinguish between "the inline helper tests prove the logic" (acceptable) and "the actual handler needs its own integration test" (stricter). The handler code has been read and the rollback guard matches the test helper exactly (lines 119-127 of CanaryEndpoints.cs vs lines 251-260 of the test helper). This is a test-scope judgment call, not a missing implementation.

---

### Gaps Summary

No blocking gaps found. All 14 must-have truths have confirmed implementations in the actual codebase. The one escalation (D-04 test scope) is a test-quality judgment call about whether inline logic replication is acceptable proof of SC2 closure, not a missing artifact or broken wiring.

The single human verification item exists because the D-04 `PdfCanaryScoringTests` tests exercise an inline private helper (`InvokeScoreWithRollbackLogicAsync` at lines 238-263) that replicates the handler decision logic rather than calling `CanaryEndpoints.ScorePdfSsimAsync` directly. The actual handler modification at commit `de7c210` has been read and confirmed correct. The question is whether this test structure satisfies SC2.

---

_Verified: 2026-06-21_
_Verifier: Claude (gsd-verifier)_
