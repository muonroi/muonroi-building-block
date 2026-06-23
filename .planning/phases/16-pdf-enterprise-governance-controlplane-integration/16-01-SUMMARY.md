---
phase: 16-pdf-enterprise-governance-controlplane-integration
plan: "01"
subsystem: pdf-enterprise-license-gate
tags: [license, feature-gate, governance, enterprise, fail-closed]
dependency_graph:
  requires: []
  provides: [IFeatureGate/LicenseFeatureGate, AddPdfEnterprise, pdf.* in all 4 governance registries]
  affects: [Muonroi.Pdf.Enterprise, Muonroi.Governance.Abstractions, Muonroi.Governance.Enterprise, Muonroi.Pdf.Governance.Tests]
tech_stack:
  added: []
  patterns: [primary-ctor injection, TryAddSingleton, MSTD0001 pragma suppression, TDD RED/GREEN]
key_files:
  created:
    - src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs
    - src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs
    - tests/Muonroi.Pdf.Governance.Tests/LicenseFeatureGateTests.cs
  modified:
    - src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj
    - src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs
    - src/Muonroi.Governance.Enterprise/License/MEnterpriseFailClosedMatrix.cs
    - tests/Muonroi.Pdf.Governance.Tests/Muonroi.Pdf.Governance.Tests.csproj
decisions:
  - "D-01 implemented: LicenseFeatureGate delegates to ILicenseGuard.HasFeature; unlicensed pdf.* throws FeatureNotLicensedException, licensed passes"
  - "MSTD0001 pragma: FeatureNotLicensedException retains InvalidOperationException base (boundary contract for callers without Governance dep); throw site suppressed with documented justification"
  - "Version bump deferred: Muonroi.Governance.Abstractions changes are additive (new Capabilities consts, CapabilityKeys entries, FeatureToCapability entries); will bump alpha.15 -> alpha.16 at next coordinated ecosystem cut per VERSION_GOVERNANCE.md policy (RESEARCH Q3 RESOLVED)"
metrics:
  duration: ~25min
  completed: "2026-06-21"
  tasks_completed: 2
  files_changed: 7
---

# Phase 16 Plan 01: LicenseFeatureGate + pdf.* Registry Registration Summary

Real fail-closed `LicenseFeatureGate : IFeatureGate` delegating to `ILicenseGuard.HasFeature`, with pdf.designer/registry/canary registered in all four governance registries and production DI bound via `AddPdfEnterprise`.

## What Was Built

### Task 1: Governance.Enterprise reference + four-registry pdf.* registration
- `Muonroi.Pdf.Enterprise.csproj`: added `ProjectReference` to `Muonroi.Governance.Enterprise` (Enterprise→Enterprise edge; OSS engine untouched — SC5 verified by `git status src/Muonroi.Pdf/`)
- `LicenseCapabilityResolver.cs`: added `PdfDesigner/PdfRegistry/PdfCanary` constants to `Capabilities` class, appended to `CapabilityKeys` HashSet, and added identity mappings to `FeatureToCapability` dict — all three regions required (RESEARCH Pitfall 1)
- `MEnterpriseFailClosedMatrix.cs`: extended `BlocksAllEnterpriseCapabilities` with pdf.designer/registry/canary OR-clauses so `MissingSignedPolicy` hosts fail-closed on pdf.* (RESEARCH Pitfall 2 / T-16-03)

### Task 2: LicenseFeatureGate + AddPdfEnterprise DI extension + tests (TDD)
- `LicenseFeatureGate.cs`: sealed primary-ctor class; `IsEnabled` delegates to `licenseGuard.HasFeature`; `EnsureFeatureOrThrow` throws `FeatureNotLicensedException` on denial; never calls `ILicenseGuard.EnsureFeature` (wrong boundary — throws `MInternalException`)
- `PdfEnterpriseServiceExtensions.cs`: `AddPdfEnterprise` registers `TryAddSingleton<IFeatureGate, LicenseFeatureGate>()` — `AlwaysAllowFeatureGate` is never registered in this extension (T-16-01 mitigation)
- `LicenseFeatureGateTests.cs`: 4 tests (LIC-01 throw on unlicensed, pass on licensed, DI binding asserts `LicenseFeatureGate` not `AlwaysAllowFeatureGate`, `LicenseTier.Licensed` + pdf.* FeatureToCapability registry coverage)

## Success Criteria Verification

- [x] LicenseFeatureGate created and registered — `AddPdfEnterprise` binds it via `TryAddSingleton`
- [x] pdf.designer/registry/canary in all 4 registries: `Capabilities` consts, `CapabilityKeys` HashSet, `FeatureToCapability` dict, `BlocksAllEnterpriseCapabilities`
- [x] `dotnet test --filter LicenseFeatureGate` exits 0 with 4 passing tests
- [x] Full `dotnet test --no-build` suite: 0 failed (confirmed two consecutive clean runs)
- [x] `git status src/Muonroi.Pdf/` shows no changes (SC5 inviolable boundary intact)
- [x] Both projects build 0 errors/warnings

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] MSTD0001 analyzer blocks `throw new FeatureNotLicensedException`**
- **Found during:** Task 2 GREEN build
- **Issue:** Roslyn MSTD0001 analyzer (Muonroi.CodeStandards) forbids `throw new X` where X does not inherit from `MException` in `Muonroi.*` non-test namespaces. `FeatureNotLicensedException : InvalidOperationException` triggers the error.
- **Fix:** Added `#pragma warning disable MSTD0001` / `#pragma warning restore MSTD0001` around the throw statement in `LicenseFeatureGate.cs`, with a comment explaining the intentional design: callers must catch without taking a `Muonroi.Governance` dependency. Confirmed the existing test `DenyAll_EnsureFeatureOrThrow_IsInvalidOperationException` (which asserts catchability as `InvalidOperationException`) continues to pass.
- **Alternative considered:** Changing `FeatureNotLicensedException` to inherit from `MException` — rejected because an existing test asserts `InvalidOperationException` catchability and `MException : Exception` (not `InvalidOperationException`), which would require changing the established boundary contract.
- **Files modified:** `src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs`
- **Commits:** `805dac46`

## Version Bump Decision

`Muonroi.Governance.Abstractions` received additive public-surface additions (new constants, new HashSet entries, new dict entries — no removals, no breaking changes). Per `VERSION_GOVERNANCE.md` and RESEARCH Q3 RESOLVED: the entire building-block ecosystem ships under a single coordinated `1.0.0-alpha.NN` suffix governed in `Directory.Build.props`. This plan's changes will be included in the next alpha cut (alpha.15 → alpha.16) coordinated across all building-block packages. No per-package `Version=` attribute was added (CPM NU1011 prohibition respected).

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes introduced. All changes are in-process DI binding and in-memory capability registry. T-16-01 (DI bypass), T-16-02 (RSA tampering), and T-16-03 (fail-closed gap) mitigated as designed.

## Known Stubs

None. `LicenseFeatureGate` is fully wired to `ILicenseGuard.HasFeature`. The `AlwaysAllowFeatureGate` remains available as a direct-construction test double but is not registered in production DI.

## Self-Check: PASSED
