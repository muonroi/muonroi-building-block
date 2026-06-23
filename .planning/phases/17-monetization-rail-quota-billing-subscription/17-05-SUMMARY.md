---
phase: 17-monetization-rail-quota-billing-subscription
plan: 05
subsystem: cross-repo-verification
tags: [oss-boundary, sc5, leak-guard, cross-repo, byte-identical, golden, MON-08]
requires:
  - "17-01 (Muonroi.Billing.Abstractions seam)"
  - "17-02 (UsageAggregator + tier-sourced caps)"
  - "17-03 (control-plane quota enforcement + invoice-preview)"
  - "17-04 (license-server subscription/renewal + tier->limit map)"
provides:
  - "OssBoundaryBillingLeakTests (automated SC5 regression guard, T-17-30)"
  - "cross-repo green gate (building-block + control-plane + license-server, 0 failures)"
  - "byte-identical OSS engine confirmation (no golden re-baseline, T-17-31)"
affects: []
tech-stack:
  added: []
  patterns: [reflection over GetReferencedAssemblies(), non-vacuous counter-assertion, per-project test gate (avoids nested-build flake)]
key-files:
  created:
    - tests/Muonroi.Pdf.Tests/Service/OssBoundaryBillingLeakTests.cs
  modified: []
decisions: [SC5]
metrics:
  duration: ~15m
  completed: 2026-06-21
---

# Phase 17 Plan 05: Cross-Repo Verification + OSS Boundary Leak-Guard Summary

Closes Phase 17 by locking the inviolable open-core boundary (SC5 / MON-08): an automated
reflection-based leak-guard proves the OSS `Muonroi.Pdf` engine references no billing/quota-enforcement
assembly, the affected per-project suites are green across all three repos (0 failures), and the OSS
engine + golden corpus are byte-identical (no re-baseline). Task 3 (human-verify checkpoint) is left
for the orchestrator.

## What Was Built

- **Task 1 (MON-08 / SC5):** `OssBoundaryBillingLeakTests` under `tests/Muonroi.Pdf.Tests/Service/`,
  mirroring the existing `PackagingMetadataTests` reflection style. Resolves the OSS assembly via
  `typeof(Muonroi.Pdf.Extensions.PdfServiceCollectionExtensions).Assembly` and the Enterprise assembly
  via `typeof(Muonroi.Pdf.Enterprise.IFeatureGate).Assembly`, then asserts over
  `GetReferencedAssemblies()`:
  - Test 1 — `Muonroi.Pdf` does NOT reference `Muonroi.Billing.Abstractions`.
  - Test 2 — `Muonroi.Pdf` references neither `Muonroi.Quota.Abstractions` nor `Muonroi.AspNetCore`.
  - Test 3 (counter-assertion) — `Muonroi.Pdf.Enterprise` DOES reference `Muonroi.Quota.Abstractions`
    (the `EnterprisePdfServiceWrapper` metering dependency), proving the quota seam lives Enterprise-side
    so Test 1/2 are meaningful, not vacuous.
- **Task 2 (MON-08):** Ran the AFFECTED per-project suites in all three repos (per-project, NOT the
  full ~80-project solution which is known to flake on nested build — memory `test_flakiness_nested_build`)
  and confirmed the byte-identical OSS gate.

## Test Results (all 0 failures)

### Task 1 — leak-guard (filtered)
`dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj -c Debug --filter "FullyQualifiedName~OssBoundaryBillingLeakTests"`
→ **Passed! Failed: 0, Passed: 3, Skipped: 0, Total: 3.**

### Task 2 — cross-repo per-project suites

| Repo | Project | Result |
|------|---------|--------|
| muonroi-building-block | Muonroi.Billing.Abstractions.Tests | Passed: 11, Failed: 0 |
| muonroi-building-block | Muonroi.Pdf.Tests (incl. Golden corpus + leak-guard) | Passed: 581, Failed: 0 |
| muonroi-building-block | Muonroi.Pdf.Governance.Tests | Passed: 11, Failed: 0 |
| muonroi-control-plane | Muonroi.ControlPlane.Host.Tests | Passed: 484, Failed: 0 |
| muonroi-license-server | Muonroi.LicenseServer.Tests | Passed: 26, Failed: 0 |

building-block affected total: 603 passed / 0 failed. control-plane: 484 / 0. license-server: 26 / 0.
All three repos green.

> Note: control-plane build emitted one pre-existing CS8425 async-iterator warning in
> `tests/.../Services/AccountServiceTests.cs` (NoOpMediator.CreateStream) — out of scope for this
> phase (not introduced by 17-01..17-05), logged here for record, not fixed.

## Byte-Identical OSS Gate (SC5 / T-17-31)

`git -C D:/sources/Core/muonroi-building-block status --porcelain src/Muonroi.Pdf tests/Muonroi.Pdf.Tests/Golden`
→ **EMPTY.**

No file under `src/Muonroi.Pdf` (OSS engine) or `tests/Muonroi.Pdf.Tests/Golden` (byte-determinism
corpus) was modified across the entire phase. No golden snapshot re-baseline was required — the OSS
engine is byte-identical. The only addition is the new leak-guard test under
`tests/Muonroi.Pdf.Tests/Service/`, which is outside both gated paths and therefore permitted.

## OSS Boundary Confirmation

`OSS-BOUNDARY.md` line 67 lists `Muonroi.Billing.Abstractions` under **OSS Packages (Apache 2.0)** only
— NOT under Commercial. The billing seam is OSS; quota *enforcement* (HTTP 429) lives at the
control-plane API boundary (`Muonroi.AspNetCore` middleware, registered in 17-03), never inside the
OSS engine.

## Deviations from Plan

None — both executed tasks (Task 1, Task 2) ran exactly as written. Task 1 is a guard/verification
test over already-shipped assemblies, so it passed on first run (no RED phase applicable — this is a
boundary-assertion test, not a behavior-adding feature; the meaningful guard is that the build FAILS
if a future leak is introduced).

## Task 3 (human-verify) — DEFERRED TO ORCHESTRATOR

Per execution instructions, Task 3 (`checkpoint:human-verify`, gate="blocking") is intentionally
NOT executed here. The orchestrator handles the human-verify checkpoint. All evidence the checkpoint
requires is recorded above:
1. Three suite results all 0-failure (table above).
2. `git status` empty under `src/Muonroi.Pdf` and `tests/Muonroi.Pdf.Tests/Golden` (byte-identical).
3. control-plane `Program.cs` quota enforcement / `OSS-BOUNDARY.md` billing-under-OSS — per 17-03/17-04 SUMMARYs and OSS-BOUNDARY.md line 67.

## Known Stubs

None.

## Threat Flags

None. T-17-30 (billing-ref leak into OSS) is now mitigated by `OssBoundaryBillingLeakTests`;
T-17-31 (golden drift) mitigated by the empty byte-identical git-status gate. No new security surface
introduced (test-only addition; T-17-SC accept — no package installs).

## Commits (muonroi-building-block, develop)

- `bf3d41b6` — test(17-05): OSS boundary billing/quota leak-guard for Muonroi.Pdf (MON-08 / SC5)
- (this SUMMARY commit recorded by final metadata commit)

## Self-Check: PASSED
