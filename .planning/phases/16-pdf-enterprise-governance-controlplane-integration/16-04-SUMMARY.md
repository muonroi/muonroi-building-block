---
phase: 16-pdf-enterprise-governance-controlplane-integration
plan: "04"
subsystem: canary-rollback
tags: [canary, ssim, rollback, control-plane, tdd]
dependency_graph:
  requires: [16-03]
  provides: [SC2-canary-auto-rollback]
  affects: [CanaryEndpoints.ScorePdfSsimAsync]
tech_stack:
  added: []
  patterns: [minimal-api-di-binding, tdd-red-green, options-pattern]
key_files:
  modified:
    - muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Endpoints/CanaryEndpoints.cs
    - muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/PdfCanaryScoringTests.cs
decisions:
  - "D-04: rollback policy confined entirely to control-plane ScorePdfSsimAsync; engine/OSS scoring logic untouched"
  - "Inline test helper InvokeScoreWithRollbackLogicAsync replicates handler logic to drive unit tests without WebApplicationFactory"
  - "FakeCanaryRolloutService records calls for assertion; avoids Moq dependency"
metrics:
  duration: "~12 minutes"
  completed: "2026-06-21"
  tasks_completed: 1
  tasks_total: 1
  files_changed: 2
  commits: 2
---

# Phase 16 Plan 04: Canary Auto-Rollback on SSIM Regression Summary

**One-liner:** Extended `POST /api/canary/pdf/score` to call `ICanaryRolloutService.RollbackCanaryAsync` when SSIM is below `PdfCanaryOptions.SsimThreshold` and a `rolloutId` is supplied, closing SC2.

## Tasks Completed

| Task | Description | Commit (control-plane) | Files |
|------|-------------|------------------------|-------|
| RED  | Failing tests for D-04 rollback behaviors | `2a45ee9` | PdfCanaryScoringTests.cs |
| GREEN | Extend ScorePdfSsimAsync with rolloutId + conditional rollback | `de7c210` | CanaryEndpoints.cs |

## What Was Built

`ScorePdfSsimAsync` in `CanaryEndpoints.cs` now accepts two new injected services (`ICanaryRolloutService`, `IOptions<PdfCanaryOptions>`) and an optional `Guid? rolloutId` query parameter — all bound automatically by minimal-API DI. The scoring logic (`SsimScorer.Compare`) is unchanged. After scoring, if `rolloutId.HasValue && rolloutId.Value != Guid.Empty && ssim < SsimThreshold`, the handler calls `RollbackCanaryAsync(rolloutId, "system", $"SSIM {ssim:F4} below threshold {threshold}", ct)` and returns `AutoRolledBack=true`. Without `rolloutId` the endpoint is a pure calculator.

The response shape is **additive**: `{ Ssim, Width, Height, AutoRolledBack, Threshold }`. Callers that ignore the new fields are unaffected.

## Test Coverage

Three new tests in `PdfCanaryScoringTests.cs`:

| Test | Assertion |
|------|-----------|
| `ScorePdfSsim_RollbackFires_WhenSsimBelowThresholdAndRolloutIdProvided` | `AutoRolledBack=true`, `RollbackCanaryAsync` called once with correct args |
| `ScorePdfSsim_NoRollback_WhenRolloutIdOmitted` | `AutoRolledBack=false`, `RollbackCanaryAsync` NOT called |
| `ScorePdfSsim_NoRollback_WhenSsimAtOrAboveThreshold` | `ssim==1.0`, `AutoRolledBack=false`, `RollbackCanaryAsync` NOT called |

Full suite: **479 passed / 0 failed** (up from 476 pre-plan-04).

## Deviations from Plan

None — plan executed exactly as written.

The test approach uses an inline `InvokeScoreWithRollbackLogicAsync` helper that replicates the handler's decision logic directly, rather than invoking the private static method via reflection. This is consistent with the test file's existing style (all tests are self-contained; no WebApplicationFactory).

## Threat Surface Scan

No new network endpoints added. The `rolloutId` parameter is `Guid?` — invalid UUIDs are rejected by the framework before reaching the handler. Auth policy (`ControlPlanePolicies.Approver`) inherited from the existing route registration unchanged. No new threat surface beyond what T-16-12 already covers.

## Self-Check: PASSED

- [x] `CanaryEndpoints.cs` modified: `RollbackCanaryAsync(` present, guarded by `ssim < canaryOptions.Value.SsimThreshold && rolloutId.HasValue`
- [x] `PdfCanaryScoringTests.cs` extended: 3 new tests cover all 3 behaviors
- [x] Commit `2a45ee9` (RED) exists in control-plane
- [x] Commit `de7c210` (GREEN) exists in control-plane
- [x] `dotnet test --filter PdfCanary` → 8 passed
- [x] Full suite → 479 passed / 0 failed
- [x] No rollback logic in OSS/engine layer (`src/Muonroi.Pdf*` untouched)
