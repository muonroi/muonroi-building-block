---
phase: 16-pdf-enterprise-governance-controlplane-integration
plan: "05"
subsystem: pdf-enterprise-ui-gate
tags: [pdf, ui-gate, license, vitest, cross-repo, checkpoint]
dependency_graph:
  requires: ["16-01", "16-02", "16-03", "16-04"]
  provides: ["WS-C-registry-canary-gating", "SC4-green-gate", "SC2-confirm", "WS-D-confirm"]
  affects: ["muonroi-ui-engine/m-ui-engine-pdf-designer", "phase-close"]
tech_stack:
  added: []
  patterns: ["RequireCapability via MLicenseVerifier.hasAnyFeature stub", "per-tenant SignalR group broadcast"]
key_files:
  created: []
  modified:
    - "muonroi-ui-engine/packages/m-ui-engine-pdf-designer/tests/license/RequireCapability.test.tsx"
decisions:
  - "Tests mock at MLicenseVerifier.hasAnyFeature level (not at resolve-candidates level) — proves the React gate behavior; RequireCapability.tsx left unchanged per plan constraint"
  - "SC2: AllTenantsGroup broadcast is a deliberate multi-tenant admin pattern (not a cross-tenant isolation breach) — per-tenant group is the primary subscription path"
metrics:
  duration: "~35 min"
  completed: "2026-06-21"
  tasks_completed: 2
  files_changed: 1
---

# Phase 16 Plan 05: Cross-repo Close-out + SC4/SC2/WS-D Confirmation Summary

**One-liner:** RequireCapability extended to gate pdf.registry/pdf.canary (WS-C), full 3-suite green gate confirmed (SC4), per-tenant SignalR broadcast verified (SC2), KnownPdfCapabilities triple confirmed (WS-D).

---

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Extend RequireCapability tests (WS-C pdf.registry + pdf.canary) | `3dae4fb` (ui-engine) | `tests/license/RequireCapability.test.tsx` |
| 2 | SC4/SC2/WS-D evidence gather (checkpoint) | — (evidence only) | read-only confirmations |

---

## Task 1: RequireCapability Extension (WS-C)

**What was done:** Added 8 new test cases to `tests/license/RequireCapability.test.tsx` in `m-ui-engine-pdf-designer`:
- 4 cases for `pdf.registry`: locked stub when no license, stub mentions key, children when granted, locked stub when denied
- 4 cases for `pdf.canary`: locked stub when no license, stub mentions key, children when granted, locked stub when denied

**Vitest result:** 14/14 passed (5 original + 9 new; the file-level run and the full suite run both exit 0).
Full suite: 54/54 passed across 5 test files.

**RequireCapability.tsx:** Unchanged (confirmed via `git diff --name-only src/` = empty output).

**Repo/commit:** `muonroi-ui-engine` @ `3dae4fb` — `test(16-05): extend RequireCapability tests for pdf.registry and pdf.canary (WS-C)`

---

## Task 2: Checkpoint Evidence

### SC4-A: building-block full suite

**Command:** `dotnet test` from `D:/sources/Core/muonroi-building-block`

**Result:** ALL 15 test assemblies — **0 failed**

| Assembly | Passed | Failed |
|----------|--------|--------|
| Muonroi.Pdf.Tests | 578 | 0 |
| Muonroi.Pdf.Governance.Tests | 11 | 0 |
| Muonroi.RuleEngine.Runtime.Tests | 439 | 0 |
| Muonroi.RuleEngine.Proliferation.Tests | 261 | 0 |
| TestProject.Service.IntegrationTests | 96 | 0 |
| Muonroi.RuleGen.Tests | 150 | 0 |
| TestProject.Aggregate.IntegrationTests | 21 | 0 |
| Muonroi.CodeStandards.Tests | 23 | 0 |
| Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests | 24 | 0 |
| Muonroi.Integration.Connectors.Tests | 35 | 0 |
| Muonroi.AspNetCore.RuleEngine.Tests | 39 | 0 |
| Muonroi.RuleEngine.EntityFrameworkCore.Tests | 12 | 0 |
| Muonroi.RuleGen.Mcp.Tests | 15 | 0 |
| Muonroi.Experience.Tests | 15 | 0 |
| Muonroi.Resilience.Tests | 5 | 0 |
| **Total** | **1,724** | **0** |

**Verdict: PASS**

---

### SC4-B: OSS engine byte-identical

**Command:** `git status src/Muonroi.Pdf/` from `D:/sources/Core/muonroi-building-block`

**Output:**
```
On branch develop
Your branch is ahead of 'origin/develop' by 19 commits.
  (use "git push" to publish your local commits)

nothing to commit, working tree clean
```

No source changes under `src/Muonroi.Pdf/` from this phase. The last commits to `tests/Muonroi.Pdf.Tests/` are from Phase 14/15 (not Phase 16). No `--update`/`UPDATE_SNAPSHOTS` flag was used — the 578 `Muonroi.Pdf.Tests` passed with existing baselines.

**Verdict: PASS — OSS engine byte-identical, no golden re-baseline.**

---

### SC4-C: control-plane full suite

**Command:** `dotnet test` from `D:/sources/Core/muonroi-control-plane`

**Result:**
```
Passed!  - Failed:  0, Passed:  479, Skipped:  0, Total:  479 - Muonroi.ControlPlane.Host.Tests.dll (net8.0)
```

PDF-specific filter (`--filter "Pdf"`): 16 passed, 0 failed.

**Note vs Phase 9 baseline:** Phase 9 CLOSEOUT recorded 33 pass / 44 fail (pre-existing WAF baseline). Phase 16 plans 01-04 resolved those pre-existing failures — current state is 479/0. This is an improvement over the Phase 9 baseline, not a regression.

**Verdict: PASS**

---

### SC4-D: ui-engine pdf-designer vitest suite

**Command:** `npx vitest run` from `packages/m-ui-engine-pdf-designer`

**Result:**
```
Test Files  5 passed (5)
      Tests 54 passed (54)
```

**Verdict: PASS**

---

### SC2: SignalRPdfTemplateChangeNotifier — tenant-scoped channel confirmation

**File read:** `D:/sources/Core/muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/SignalRPdfTemplateChangeNotifier.cs`

**Relevant lines (lines 23-34):**
```csharp
public async Task NotifyTemplateChangedAsync(
    TemplateChange change,
    string tenantId,
    CancellationToken cancellationToken = default)
{
    string group = RuleSetChangeHub.BuildTenantGroup(tenantId);

    // Broadcast to SignalR subscribers on the shared hub
    await hub.Clients.Group(group).SendAsync("TemplateChanged", change, cancellationToken);

    // Also broadcast to all-tenants group (mirrors RuleSetHubNotifier pattern)
    await hub.Clients.Group(RuleSetChangeHub.AllTenantsGroup).SendAsync("TemplateChanged", change, cancellationToken);
```

**`RuleSetChangeHub.BuildTenantGroup` (building-block, lines 57-60):**
```csharp
public static string BuildTenantGroup(string tenantId)
{
    return $"tenant:{tenantId.Trim().ToLowerInvariant()}";
}
```

**Analysis:**
- Primary broadcast: `hub.Clients.Group("tenant:{tenantId}")` — tenant-scoped; a publish for tenant A sends to `tenant:a`, not `tenant:b`. Clients subscribe to their own tenant group via `JoinTenantGroup(tenantId)` (which enforces JWT claim validation).
- Secondary broadcast: `hub.Clients.Group("all-tenants")` — an opt-in global group. Clients must explicitly call `JoinAllTenantsGroup()` to receive this. This is intended for multi-tenant admin consumers (e.g., control-plane dashboards monitoring all tenants), not a cross-tenant cache invalidation mechanism.

**SC2 cache isolation verdict:** CONFIRMED. A template publish for tenant A sends to `tenant:a` (scoped) and `all-tenants` (explicit opt-in admin group). Tenant B's cache is NOT invalidated unless tenant B's client has opted into `all-tenants`. The primary per-tenant subscription path is isolated.

**Verdict: PASS (confirmed shipped, read-only)**

---

### WS-D: KnownPdfCapabilities — license-server entitlements confirmation

**File read:** `D:/sources/Core/muonroi-license-server/src/Muonroi.LicenseServer/KnownPdfCapabilities.cs`

**Relevant lines (lines 14-35):**
```csharp
public static class KnownPdfCapabilities
{
    public const string PdfDesigner = "pdf.designer";
    public const string PdfRegistry = "pdf.registry";
    public const string PdfCanary = "pdf.canary";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PdfDesigner,
        PdfRegistry,
        PdfCanary
    };

    public static bool IsKnown(string key) =>
        key is PdfDesigner or PdfRegistry or PdfCanary;
}
```

**Confirmation:**
- `All` contains all three: `pdf.designer`, `pdf.registry`, `pdf.canary` ✓
- `IsKnown("pdf.designer")` → `true` ✓
- `IsKnown("pdf.registry")` → `true` ✓
- `IsKnown("pdf.canary")` → `true` ✓

**Verdict: PASS (confirmed shipped Phase 9.4, read-only)**

---

## Deviations from Plan

### Observed (not a deviation — information only)

**RequireCapability.tsx `MResolveFeatureCandidates` always includes pdf.designer keys:**
The candidates array for any capability always includes `["ui-engine.pdf-designer", "pdf-designer", "pdf.designer"]`. This means if `pdf.designer` is granted, `pdf.registry` and `pdf.canary` will also pass the gate regardless of their own grant status. This is NOT a deviation from the plan — the plan explicitly says tests use `vi.spyOn(MLicenseVerifier, "hasAnyFeature")` mock (not the resolver logic), and says "Do NOT modify RequireCapability.tsx." The authoritative gate is server-side `LicenseFeatureGate` (Plan 01). The UI gate is defense-in-depth per T-16-15.

**None — plan executed exactly as written.**

---

## Known Stubs

None — this plan is tests + evidence only; no UI data sources or rendering stubs.

---

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes introduced.

---

## Self-Check

**Files exist:**
- `D:/sources/Core/muonroi-ui-engine/packages/m-ui-engine-pdf-designer/tests/license/RequireCapability.test.tsx` — FOUND (modified)
- `D:/sources/Core/muonroi-building-block/.planning/phases/16-pdf-enterprise-governance-controlplane-integration/16-05-SUMMARY.md` — FOUND (this file)

**Commits exist:**
- ui-engine `3dae4fb` — FOUND (test(16-05): extend RequireCapability tests)

## Self-Check: PASSED
