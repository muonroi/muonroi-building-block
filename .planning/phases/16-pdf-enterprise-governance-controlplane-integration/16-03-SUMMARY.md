---
phase: 16-pdf-enterprise-governance-controlplane-integration
plan: "03"
subsystem: compliance-evidence-pack
tags: [compliance, control-plane, audit, IMControlPlaneStore, D-03]
dependency_graph:
  requires:
    - IRuleSetAuditStore (already registered by AddMRuleEngineWithPostgres in control-plane)
    - MControlPlaneRegistry / IMControlPlaneStore (Muonroi.Governance.Enterprise)
    - MComplianceExportService (consumes IEnumerable<IMControlPlaneStore>)
  provides:
    - PdfAuditControlPlaneStore : IMControlPlaneStore (control-plane Services/Compliance/)
    - DI registration in control-plane Program.cs
  affects:
    - MComplianceExportService evidence pack (will now include pdf.template.* events)
tech_stack:
  added:
    - Muonroi.Governance.Enterprise ProjectReference added to Muonroi.ControlPlane.Host.csproj
  patterns:
    - sync-over-async bridge via GetAwaiter().GetResult() (documented, intentional)
    - IMControlPlaneStore adapter pattern (read-only, no-op Save)
    - ILogger<T> (Microsoft) per control-plane convention (not IMLog)
    - No Silent Catch — logger?.LogError on query failure
key_files:
  created:
    - muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/Compliance/PdfAuditControlPlaneStore.cs
    - muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/PdfAuditControlPlaneStoreTests.cs
  modified:
    - muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Program.cs
    - muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Muonroi.ControlPlane.Host.csproj
decisions:
  - "Sync bridge: GetAwaiter().GetResult() chosen over background IHostedService cache — low pdf.template.* event volume (< 100 per tenant); documented with XML-doc comment in the adapter"
  - "AddSingleton (not TryAdd) for IMControlPlaneStore adapter — MComplianceExportService aggregates IEnumerable<IMControlPlaneStore>, so each registration must appear individually"
  - "Added Muonroi.Governance.Enterprise ProjectReference to control-plane Host.csproj — required for IMControlPlaneStore + MControlPlaneRegistry types, not previously referenced"
metrics:
  duration: "~25 minutes"
  completed: 2026-06-21
  tasks: 2
  files: 4
---

# Phase 16 Plan 03: PdfAuditControlPlaneStore (D-03 Compliance Evidence Pack) Summary

**One-liner:** IMControlPlaneStore adapter bridging IRuleSetAuditStore pdf.template.* events into the MComplianceExportService evidence chain via GetAwaiter().GetResult() sync bridge, registered as AddSingleton in control-plane Program.cs.

## Tasks Completed

| Task | Name | Commit (control-plane) | Status |
|------|------|------------------------|--------|
| 1 (RED) | Failing tests for PdfAuditControlPlaneStore | `4e16004` | Done |
| 1 (GREEN) | PdfAuditControlPlaneStore implementation | `740d929` | Done |
| 2 | Register adapter in DI (Program.cs) | `6a26224` | Done |

## What Was Built

`PdfAuditControlPlaneStore : IMControlPlaneStore` — a sealed class in the new
`Muonroi.ControlPlane.Host.Services.Compliance/` directory. It:

- Calls `IRuleSetAuditStore.QueryAsync(null, 1, 1000)` via `GetAwaiter().GetResult()` sync bridge
- Filters `entry.Action.StartsWith("pdf.template.", OrdinalIgnoreCase)` — exactly the 6 constants
  from `PdfTemplateAuditActions` (created/updated/submitted/approved/rejected/activated)
- Orders by `TimestampUtc` ascending
- Maps each entry to `MControlPlaneAuditRecord` (AuditId=Id, EventType=Action, EntityType="pdf-template",
  EntityId=WorkflowName, Actor=entry.Actor??"control-plane", DataHash=ContentHash??"", signature fields with ""-coalesce)
- Catches all exceptions, calls `logger?.LogError(ex, "[PdfAuditControlPlaneStore] Failed to query audit store.")`
  and returns an empty `MControlPlaneRegistry` — no propagation (T-16-10 mitigation)
- `Save(registry)` is a documented no-op (read-only adapter)
- Registered via `builder.Services.AddSingleton<IMControlPlaneStore, PdfAuditControlPlaneStore>()`
  in Program.cs immediately after `PdfTemplateRegistryService` block

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Muonroi.Governance.Enterprise ProjectReference to Host.csproj**
- **Found during:** Task 1 (GREEN) — build failed because `IMControlPlaneStore` and `MControlPlaneRegistry` types live in `Muonroi.Governance.Enterprise`, which was not referenced by `Muonroi.ControlPlane.Host.csproj`
- **Issue:** The plan specified `using Muonroi.Governance.ControlPlane;` but the project had no reference to the assembly containing that namespace
- **Fix:** Added `<ProjectReference Include="...\Muonroi.Governance.Enterprise\Muonroi.Governance.Enterprise.csproj" />` to `Muonroi.ControlPlane.Host.csproj` with explanatory comment
- **Files modified:** `src/Host/Muonroi.ControlPlane.Host/Muonroi.ControlPlane.Host.csproj`
- **Commit:** `740d929`

## Test Results

- `PdfAuditControlPlaneStoreTests`: 7 tests — all green
  - Filter: non-pdf actions excluded, exactly 2 pdf.template.* included from 3-entry store
  - Field mapping: all 10 fields verified for pdf.template.approved entry
  - Error resilience: QueryAsync throw → no exception propagates, empty registry returned
  - Empty audit trail on failure
  - Ordering: entries returned in TimestampUtc ascending order
  - Save no-op: no exception thrown
  - Null Actor: defaults to "control-plane"
- Full suite: **476 pass, 0 fail, 0 skip** (net8.0)

## TDD Gate Compliance

| Gate | Commit | Status |
|------|--------|--------|
| RED — test commit | `4e16004` | Passed (build failed as expected — class missing) |
| GREEN — impl commit | `740d929` | Passed (7 tests green) |
| REFACTOR | None needed | N/A |

## Security Notes (from threat model)

- **T-16-08 (cross-tenant leakage):** Adapter adds no new tenant filter; `IRuleSetAuditStore.QueryAsync` enforces existing tenant scoping. No widening.
- **T-16-09 (tampering):** `ContentHash` and `Signature` fields preserved verbatim from `RuleSetAuditEntry` in the mapping.
- **T-16-10 (DoS via sync bridge failure):** `Load()` try/catch logs and returns empty registry on any exception — evidence export degrades gracefully.

## Known Stubs

None — the adapter is fully wired and returns real audit data from `IRuleSetAuditStore`.

## Self-Check

- [x] `PdfAuditControlPlaneStore.cs` exists at `D:/sources/Core/muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/Compliance/PdfAuditControlPlaneStore.cs`
- [x] `PdfAuditControlPlaneStoreTests.cs` exists at `D:/sources/Core/muonroi-control-plane/tests/Muonroi.ControlPlane.Host.Tests/PdfAuditControlPlaneStoreTests.cs`
- [x] Commits `4e16004`, `740d929`, `6a26224` verified in control-plane `git log --oneline -6`
- [x] `dotnet test --filter PdfAuditControlPlane` → 7 passed
- [x] Full suite `dotnet test` → 476 passed, 0 failed

## Self-Check: PASSED
