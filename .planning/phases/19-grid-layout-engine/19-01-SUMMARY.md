---
phase: 19-grid-layout-engine
plan: 01
subsystem: pdf-governance
tags: [policy, grid, css-layout, allow-modern-layout]
requires:
  - PdfPolicySettings.AllowModernLayout (existing flag, Phase 18)
provides:
  - "LegacyPrintPolicy accepts CSS grid display + grid sub-properties when AllowModernLayout=true"
affects:
  - src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs
tech_stack:
  added: []
  patterns:
    - "Gate grid acceptance on existing AllowModernLayout flag (mirror Phase 18 flex gate)"
key_files:
  created: []
  modified:
    - src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs
    - tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyAllowModernLayoutTests.cs
decisions:
  - "Reuse PdfPolicySettings.AllowModernLayout — the one flag now unlocks BOTH flex (Phase 18) and grid (Phase 19); no new config key"
metrics:
  duration_minutes: 4
  completed_date: 2026-06-21
  tasks: 2
  files_modified: 2
---

# Phase 19 Plan 01: Grid Policy Acceptance Gate Summary

Gated `LegacyPrintPolicy` CSS grid acceptance (display + grid sub-properties) on the existing `PdfPolicySettings.AllowModernLayout` flag, mirroring exactly what Phase 18 did for flex — the one flag now unlocks both flex and grid.

## What Was Built

**Task 1 — Policy gate (`LegacyPrintPolicy.cs`):** Two `&& !allowModernLayout` additions, the exact inverse of Phase 18's "grid stays blocked":

1. **Grid display branch** (`LegacyPrintPolicy.cs:264`):
   `if ((display is "grid" or "inline-grid") && !allowModernLayout)`
   Mirrors the flex gate at `:253`. With the flag on, neither `forbidden.display.grid` (strict) nor `soft-degrade.display.grid` (soft-degrade) is added, and `softDegradeGridTriggered` is never set (so the grid telemetry counter at `:340` is naturally suppressed).

2. **Grid sub-prop branch** (`LegacyPrintPolicy.cs:294`):
   `if (!gridSubPropSeen && !allowModernLayout)`
   Mirrors the flex sub-prop guard at `:310`. With the flag on, grid sub-properties are not dropped and no `soft-degrade.grid-subproperty` warning is emitted.

Comment regions updated: the old "Grid (below) stays blocked even when AllowModernLayout=true" note was removed; added `// GRID-01 / GRID-02: ... accept grid display.` and `// GRID-02: ... do not emit the "will be ignored" warning.`

The flex display gate (`:253`), flex sub-prop branch (`:310`), `DefaultStrictPolicy`, `FlexGridSubProperties`, and all transform/gradient/script/href/position logic were left untouched (T-19-01 / T-19-02 mitigations).

**Task 2 — Tests (`LegacyPrintPolicyAllowModernLayoutTests.cs`):**

Flipped the two Phase-18 grid-blocked tests:
- `Grid_FlagOn_StrictBase_StillForbidden` (@:62) → `Grid_FlagOn_StrictBase_Accepted` — now asserts `Accepted==true` + NotContain `forbidden.display.grid`.
- `Grid_FlagOn_SoftDegrade_StillSoftDegradeWarning` (@:75) → `Grid_FlagOn_SoftDegrade_Accepted_NoGridWarning` — now asserts NotContain `soft-degrade.display.grid`.

Added four new `[Fact]` tests:
- `GridWithSubProps_FlagOn_Accepted_NoGridSubpropWarning` (GRID-02) — `display:grid;grid-template-columns;gap` → accepted, no `soft-degrade.grid-subproperty`.
- `FlexAndGrid_FlagOn_BothAccepted` (GRID-02) — flag unlocks BOTH flex and grid.
- `Grid_FlagOff_StrictDefault_StillForbidden_BothPolicies` (GRID-03) — flag off: `LegacyPrintPolicy` strict + `DefaultStrictPolicy` both emit `forbidden.display.grid`.
- `GridWithSubProp_FlagOff_SoftDegrade_StillWarnsAndDegrades` (GRID-03) — flag off soft-degrade still warns `soft-degrade.display.grid` + `soft-degrade.grid-subproperty`.

Class XML-doc updated to state the flag now unlocks both flex and grid. Existing flex tests and `LegacyPrintPolicyTests.cs` untouched (SC4 — flex unaffected; no grid tests added to `LegacyPrintPolicyTests.cs`).

## Verification

- `dotnet build src/Muonroi.Pdf.Governance` — Build succeeded, 0 Warning, 0 Error.
- `dotnet test tests/Muonroi.Pdf.Tests` (per-project) — **622 passed, 0 failed**.
- `dotnet test tests/Muonroi.Pdf.Governance.Tests` (per-project) — **11 passed, 0 failed**.
- Affected-class filter (`LegacyPrintPolicyAllowModernLayoutTests|LegacyPrintPolicyTests`) — 17 passed, 0 failed.

## Success Criteria

- GRID-01: grid display + grid sub-props gated on existing `AllowModernLayout` flag (no new config key). ✓
- GRID-02: flag on → grid accepted (no violation, sub-props not dropped); Phase-18 grid-blocked tests flipped; `DefaultStrictPolicy` always-strict. ✓
- GRID-03: flag off → grid behaviour byte-for-byte unchanged (strict Error `forbidden.display.grid`; soft-degrade Warning + sub-prop drop), proven by the two new flag-off tests. ✓

## Deviations from Plan

None — plan executed exactly as written.

## Commits

- `ce04ed07` feat(19-01): gate LegacyPrintPolicy grid acceptance on AllowModernLayout
- `0f88b371` test(19-01): flip grid-blocked tests to grid-accepted + add GRID-02/03 coverage

## Self-Check: PASSED

- Files exist: `LegacyPrintPolicy.cs`, `LegacyPrintPolicyAllowModernLayoutTests.cs`, `19-01-SUMMARY.md` — all FOUND.
- Commits exist: `ce04ed07`, `0f88b371` — both FOUND in git log.
- Gate grep: `LegacyPrintPolicy.cs:264` = `(display is "grid" or "inline-grid") && !allowModernLayout`; `:295` = `!gridSubPropSeen && !allowModernLayout`. Both confirmed.
