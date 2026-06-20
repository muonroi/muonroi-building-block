---
phase: 16-pdf-enterprise-governance-controlplane-integration
plan: "02"
subsystem: pdf-enterprise-metering
tags: [metering, quota, decorator, enterprise, record-only, di-decoration]
dependency_graph:
  requires: [16-01]
  provides: [EnterprisePdfServiceWrapper, QuotaType.PdfRendersPerDay, TenantQuota.MaxPdfRendersPerDay, active-IMPdfService-metering]
  affects: [Muonroi.Quota.Abstractions, Muonroi.Pdf.Enterprise, Muonroi.Pdf.Governance.Tests]
tech_stack:
  added: []
  patterns: [hand-decorator-no-scrutor, primary-ctor injection, try/catch-log-swallow, TDD RED/GREEN]
key_files:
  created:
    - src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs
    - tests/Muonroi.Pdf.Governance.Tests/EnterprisePdfServiceWrapperTests.cs
  modified:
    - src/Muonroi.Quota.Abstractions/QuotaType.cs
    - src/Muonroi.Quota.Abstractions/TenantQuota.cs
    - src/Muonroi.Quota.Abstractions/InMemoryTenantQuotaTracker.cs
    - src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs
decisions:
  - "D-02 implemented: EnterprisePdfServiceWrapper records one metered event per render via IncrementUsageAsync(tenantId, QuotaType.PdfRendersPerDay, pageCount); no hard cap; record-only"
  - "Hand-decoration: IMPdfService re-registered via factory that captures and removes the prior descriptor (AddMPdf TryAddSingleton); no Scrutor in stack"
  - "MSTD0001 pragma on ResolveFromDescriptor InvalidOperationException: DI bootstrap invariant violation; IdiomaicDI exception type; suppressed with documented justification"
  - "Version bump deferred: Muonroi.Quota.Abstractions additive changes (new enum member, new property, new switch arm); will bump alpha.15 -> alpha.16 at next coordinated ecosystem cut per VERSION_GOVERNANCE.md (same decision as Plan 01)"
metrics:
  duration: ~30min
  completed: "2026-06-21"
  tasks_completed: 2
  files_changed: 6
---

# Phase 16 Plan 02: EnterprisePdfServiceWrapper + PdfRendersPerDay Metering Summary

Record-only per-tenant PDF render metering via QuotaType.PdfRendersPerDay, with EnterprisePdfServiceWrapper wired as the active IMPdfService decorator using hand-decoration (no Scrutor).

## What Was Built

### Task 1: QuotaType.PdfRendersPerDay + TenantQuota.MaxPdfRendersPerDay + GetLimit arm (TDD)

- `QuotaType.cs`: appended `PdfRendersPerDay` enum member (XML-doc: record-only, no hard cap in Phase 16)
- `TenantQuota.cs`: added `MaxPdfRendersPerDay { get; set; } = int.MaxValue` property; set `MaxPdfRendersPerDay = int.MaxValue` in all four `TenantQuotaPresets` (Free, Starter, Professional, Enterprise) — all unlimited this phase
- `InMemoryTenantQuotaTracker.cs`: added `QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay` arm to `GetLimit` switch immediately before the `_ => int.MaxValue` fallback — switch stays exhaustive

### Task 2: EnterprisePdfServiceWrapper decorator + wire as active IMPdfService + tests (TDD)

- `EnterprisePdfServiceWrapper.cs`: sealed primary-ctor class in `Muonroi.Pdf.Enterprise.Metering`; implements all three `IMPdfService` methods by delegating to `inner`, capturing `PdfRenderResult`, then calling `RecordMeteringAsync(result.PageCount, ct)` before returning
- `RecordMeteringAsync`: resolves tenant via `executionContextAccessor?.Get()?.TenantId ?? TenantContext.CurrentTenantId`; skips if null/whitespace; calls `ITenantQuotaTracker.IncrementUsageAsync(tenantId, QuotaType.PdfRendersPerDay, pageCount, ct)` in try/catch that logs `logger?.Error(ex, "[PDF] Metering record failed (non-blocking): {Message}", ex.Message)` and swallows (T-16-05 mitigation)
- `PdfEnterpriseServiceExtensions.AddPdfEnterprise`: appended hand-decorator logic — captures prior `IMPdfService` descriptor, removes it, re-registers via factory constructing `EnterprisePdfServiceWrapper(inner, quotaTracker, executionContextAccessor, logger)` where `inner` is resolved from the captured descriptor via `ImplementationInstance` / `ImplementationFactory` / `ActivatorUtilities.CreateInstance` (whichever the descriptor carries)
- `EnterprisePdfServiceWrapperTests.cs`: 6 tests covering Task 1 behaviors (GetLimit unlimited + IncrementUsageAsync recording) and Task 2 behaviors (D-02 record, non-blocking failure, no-tenant skip, SC3 active wiring); all hand-written fakes

## Success Criteria Verification

- [x] `QuotaType.cs` contains `PdfRendersPerDay`
- [x] `TenantQuota.cs` contains `MaxPdfRendersPerDay` with `= int.MaxValue` default and all four presets set it
- [x] `InMemoryTenantQuotaTracker.cs` GetLimit contains `QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay`
- [x] `EnterprisePdfServiceWrapper.cs` implements `IMPdfService` with `IncrementUsageAsync` + `QuotaType.PdfRendersPerDay`
- [x] Metering call wrapped in try/catch with `logger?.Error(...)` (No Silent Catch) — does not rethrow
- [x] `PdfEnterpriseServiceExtensions.cs` re-registers `IMPdfService` as `EnterprisePdfServiceWrapper` factory + still contains Plan 01 `TryAddSingleton<IFeatureGate, LicenseFeatureGate>`
- [x] Test 4 (SC3): `sp.GetRequiredService<IMPdfService>()` resolves to `EnterprisePdfServiceWrapper` — passes
- [x] `dotnet test ... --filter EnterprisePdfServiceWrapper` exits 0 with 6 tests green
- [x] Full `dotnet test` (solution) — 0 failed (all runs clean)
- [x] `git status src/Muonroi.Pdf/` clean — SC5 inviolable boundary intact

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] MSTD0001 analyzer blocks `throw new InvalidOperationException` in ResolveFromDescriptor**
- **Found during:** Task 2 GREEN build
- **Issue:** Roslyn MSTD0001 analyzer forbids raw `throw new InvalidOperationException` in `Muonroi.Pdf.Enterprise.*` namespaces
- **Fix:** Added `#pragma warning disable MSTD0001` / `#pragma warning restore MSTD0001` around the throw in `ResolveFromDescriptor`, with a comment explaining: DI bootstrap invariant violation, `InvalidOperationException` is the idiomatic DI exception type, must not carry `MException` dependency
- **Files modified:** `src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs`
- **Commit:** `69563562`

## Version Bump Decision

`Muonroi.Quota.Abstractions` received additive public-surface additions (new `QuotaType` member, new `TenantQuota` property, new switch arm — no removals, no breaking changes). Per `VERSION_GOVERNANCE.md` and RESEARCH Q3 RESOLVED: the entire building-block ecosystem ships under a single coordinated `1.0.0-alpha.NN` suffix governed in `Directory.Build.props`. This plan's changes will be included in the next alpha cut (alpha.15 → alpha.16) coordinated across all building-block packages. No per-package `Version=` attribute added (CPM NU1011 prohibition respected). Same policy applied in Plan 01 for `Muonroi.Governance.Abstractions`.

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes introduced. All changes are in-process DI binding and in-memory quota tracking. T-16-05 (metering DoS) and T-16-06 (cross-tenant attribution) mitigated as designed: metering is caught+swallowed, tenant resolved from server-set ambient context only.

## Known Stubs

None. `EnterprisePdfServiceWrapper` is fully wired to the real `ITenantQuotaTracker` and returns the inner render result verbatim. `MaxPdfRendersPerDay = int.MaxValue` is intentional for Phase 16 (record-only, no enforcement — D-02 decision).

## TDD Gate Compliance

- RED gate: commit `663c3b28` — `test(16-02): add failing tests for PdfRendersPerDay quota + EnterprisePdfServiceWrapper metering`
- GREEN gate: commits `cdb618e8` (Task 1) + `69563562` (Task 2)
- REFACTOR gate: none required — implementation is clean

## Self-Check: PASSED

- `src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs` — FOUND
- `tests/Muonroi.Pdf.Governance.Tests/EnterprisePdfServiceWrapperTests.cs` — FOUND
- `src/Muonroi.Quota.Abstractions/QuotaType.cs` contains `PdfRendersPerDay` — FOUND
- Commit `663c3b28` (RED) — FOUND
- Commit `cdb618e8` (GREEN Task 1) — FOUND
- Commit `69563562` (GREEN Task 2) — FOUND
- `git status src/Muonroi.Pdf/` clean — CONFIRMED
- Full suite 0 failed — CONFIRMED
