---
phase: 16
slug: pdf-enterprise-governance-controlplane-integration
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-20
---

# Phase 16 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET building-block + control-plane), vitest (ui-engine) |
| **Config file** | none — solution-level test projects already exist |
| **Quick run command (BB gate)** | `dotnet test tests/Muonroi.Pdf.Governance.Tests/Muonroi.Pdf.Governance.Tests.csproj` |
| **Quick run command (BB engine)** | `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` |
| **Quick run command (CP)** | `dotnet test muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/Muonroi.ControlPlane.Host.Tests.csproj` |
| **Quick run command (UI)** | `npx vitest run` (in `muonroi-ui-engine/packages/m-ui-engine-pdf-designer`) |
| **Full suite command** | `dotnet test` (solution root, per repo) |
| **Estimated runtime** | ~60–120 seconds (full BB solution) |

---

## Sampling Rate

- **After every task commit:** Run the relevant project test command.
- **After every plan wave:** Run `dotnet test` (full solution) for the affected repo.
- **Before `/gsd:verify-work`:** Full suite green (0 failed — Pre-Push Test Gate).
- **Max feedback latency:** 120 seconds.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 01-T1 | 16-01 | 1 | SC1 (D-01) | T-16-03 | pdf.* fail-closed in all 4 registries | build | `dotnet build src/Muonroi.Pdf.Enterprise/...` + `src/Muonroi.Governance.Enterprise/...` | yes | ⬜ pending |
| 01-T2 | 16-01 | 1 | SC1 (D-01) | T-16-01, T-16-02 | unlicensed pdf.* throws; LicenseFeatureGate bound (not no-op) | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/... --filter LicenseFeatureGate` | Wave 0 | ⬜ pending |
| 02-T1 | 16-02 | 2 | SC3 (D-02) | — | QuotaType + unlimited default + exhaustive switch | unit/build | `dotnet build src/Muonroi.Quota.Abstractions/...` | yes | ⬜ pending |
| 02-T2 | 16-02 | 2 | SC3 (D-02) | T-16-05, T-16-06 | meters page count; throwing tracker does NOT block render | unit | `dotnet test tests/Muonroi.Pdf.Governance.Tests/... --filter EnterprisePdfServiceWrapper` | Wave 0 | ⬜ pending |
| 03-T1 | 16-03 | 1 | SC3 (D-03) | T-16-08, T-16-10 | only pdf.template.* surfaced; safe degrade on query failure | unit | `dotnet test muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/... --filter PdfAuditControlPlane` | Wave 0 | ⬜ pending |
| 03-T2 | 16-03 | 1 | SC3 (D-03) | — | adapter registered as IMControlPlaneStore | build | `dotnet build muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/...` | yes | ⬜ pending |
| 04-T1 | 16-04 | 1 | SC2 (D-04) | T-16-11, T-16-12 | sub-threshold SSIM + rolloutId triggers rollback; pure calculator otherwise | unit | `dotnet test muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/... --filter PdfCanary` | extend existing | ⬜ pending |
| 05-T1 | 16-05 | 3 | SC1 (WS-C) | T-16-14 | registry/canary gate (locked stub when ungranted) | unit | `npx vitest run tests/license/RequireCapability.test.tsx` | extend existing | ⬜ pending |
| 05-T2 | 16-05 | 3 | SC4 (+WS-D) | — | full suites green, OSS byte-identical, WS-D confirmed | human-verify | `dotnet test` (BB + CP) + `npx vitest run` (UI) | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

New/extended test files created as the first action of each owning task:

- [ ] `tests/Muonroi.Pdf.Governance.Tests/LicenseFeatureGateTests.cs` — LIC-01 unlicensed-throws / licensed-passes / DI-binds-real-gate (Plan 01 Task 2). Requires adding `Muonroi.Pdf.Enterprise` + `Muonroi.Governance.Abstractions` ProjectReferences to `Muonroi.Pdf.Governance.Tests.csproj`.
- [ ] `tests/Muonroi.Pdf.Governance.Tests/EnterprisePdfServiceWrapperTests.cs` — D-02 meter-page-count / non-blocking-failure / no-tenant-skip (Plan 02 Task 2).
- [ ] `muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/PdfAuditControlPlaneStoreTests.cs` — D-03 pdf.template.* filter / field mapping / safe degrade (Plan 03 Task 1).
- [ ] Extend `muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/PdfCanaryScoringTests.cs` — D-04 rollback-fires / no-rolloutId-no-rollback / above-threshold-no-rollback (Plan 04 Task 1).
- [ ] Extend `muonroi-ui-engine/packages/m-ui-engine-pdf-designer/tests/license/RequireCapability.test.tsx` — WS-C registry/canary gating (Plan 05 Task 1).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| OSS engine byte-identical (no golden re-baseline) | SC4 | Confirming the absence of a snapshot re-baseline + clean `git status src/Muonroi.Pdf/` is a human judgement on diff intent | Run `tests/Muonroi.Pdf.Tests/` without update flags; confirm `git status src/Muonroi.Pdf/` shows no source change from this phase (Plan 05 checkpoint). |
| WS-D entitlements confirmed shipped | SC4 / WS-D | No code change; a read-confirmation of Phase 9.4 deliverable | Read `KnownPdfCapabilities.cs`; confirm All + IsKnown cover pdf.designer/registry/canary (Plan 05 checkpoint). |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (checkpoint uses `<human-check>` for the byte-identical judgement)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planner — 2026-06-20
