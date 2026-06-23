---
phase: 18-flexbox-layout-engine
plan: 01
subsystem: pdf-governance
tags: [policy, flexbox, opt-in-flag, css-gate]
requires: []
provides:
  - "PdfPolicySettings.AllowModernLayout opt-in flag (default false)"
  - "LegacyPrintPolicy flex acceptance gated on AllowModernLayout (grid stays blocked)"
affects:
  - "src/Muonroi.Pdf.Abstractions/PdfConfigs.cs"
  - "src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs"
tech-stack:
  added: []
  patterns: ["opt-in feature flag bound from PdfConfigs:Policy", "scoped gate (flex only, grid untouched)"]
key-files:
  created:
    - "tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyAllowModernLayoutTests.cs"
  modified:
    - "src/Muonroi.Pdf.Abstractions/PdfConfigs.cs"
    - "src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs"
decisions:
  - "D-02: zero breaking change — flag default false keeps flex byte-for-byte identical to pre-Phase-18 behaviour"
  - "Grid block + grid sub-property branch left untouched so AllowModernLayout cannot relax grid (T-18-01 mitigation)"
  - "DefaultStrictPolicy not modified — it ignores the flag and always blocks flex (T-18-02 mitigation)"
metrics:
  duration: ~10m
  completed: 2026-06-21
  tasks: 2
  files: 3
---

# Phase 18 Plan 01: AllowModernLayout Gate Summary

Added the opt-in `PdfPolicySettings.AllowModernLayout` flag (default false) and gated `LegacyPrintPolicy` flex acceptance on it: when on, all flex display + flex sub-property violations are suppressed (flex is accepted for real Flexbox rendering); grid stays blocked exactly as today; `DefaultStrictPolicy` is untouched.

## What Was Built

### Task 1 — flag + gate (commit `e84b138e`)

**`src/Muonroi.Pdf.Abstractions/PdfConfigs.cs:74-83`** — new flag on `PdfPolicySettings`:
```csharp
public bool AllowModernLayout { get; init; } = false;
```
Bound automatically from `PdfConfigs:Policy:AllowModernLayout` via the existing `PdfConfigs.Policy` (`PdfConfigs.cs:107`). (FLEX-01)

**`src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs`** — gating threaded end-to-end:
- `LegacyPrintPolicy.cs:55` — new `private readonly bool _allowModernLayout;` field.
- `LegacyPrintPolicy.cs:60` parameterless ctor → `this(softDegrade: false, allowModernLayout: false)`.
- `LegacyPrintPolicy.cs:66-69` DI ctor → reads `options?.Value?.Policy?.AllowModernLayout ?? false`.
- `LegacyPrintPolicy.cs:72-78` private ctor `(bool softDegrade, bool allowModernLayout = false)` sets both fields.
- `LegacyPrintPolicy.cs:107` `ValidateAsync` passes the flag into `CheckCssFeatures(..., _softDegrade, _allowModernLayout)`.
- `LegacyPrintPolicy.cs:135` `CheckCssFeatures` signature gains `bool allowModernLayout`.
- **The actual gate** — flex display block (`LegacyPrintPolicy.cs:250-264`):
  ```csharp
  // FLEX-02 / FLEX-04: when allowModernLayout is on, flex is ACCEPTED ...
  // Grid (below) stays blocked even when AllowModernLayout=true.
  if ((display is "flex" or "inline-flex") && !allowModernLayout)
  {
      if (softDegrade) { ... soft-degrade.display.flex ...; softDegradeFlexTriggered = true; }
      else             { ... forbidden.display.flex ... }
  }
  ```
- Flex sub-property warning gate (`LegacyPrintPolicy.cs:307-309`): `if (!flexSubPropSeen && !allowModernLayout)`.
- Grid display block (`LegacyPrintPolicy.cs:266-275`) and grid sub-property branch are **unchanged** — grid stays blocked (FLEX-04). `softDegradeFlexTriggered` can no longer fire for flex when the flag is on, so the flex telemetry counter is naturally suppressed; no extra change needed.

### Task 2 — policy unit tests (commit `6ef497f2`)

**`tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyAllowModernLayoutTests.cs`** — 5 facts:
1. `FlexWithSubProps_FlagOn_Accepted_NoFlexViolation` (FLEX-02) — `display:flex;flex-direction:row;gap:10px` with flag on → `Accepted=true`, no `forbidden.display.flex` / `soft-degrade.display.flex` / `soft-degrade.flex-subproperty`.
2. `Grid_FlagOn_StrictBase_StillForbidden` (FLEX-04) — `display:grid` + flag on, strict base → `Accepted=false`, contains `forbidden.display.grid`.
3. `Grid_FlagOn_SoftDegrade_StillSoftDegradeWarning` (FLEX-04) — `display:grid` + flag on + soft-degrade → contains `soft-degrade.display.grid` (Warning).
4. `Flex_FlagOff_StrictDefault_StillForbidden_BothPolicies` (FLEX-03) — `display:flex`, default policy → `forbidden.display.flex`; `DefaultStrictPolicy` also still blocks (ignores flag).
5. `FlexWithSubProp_FlagOff_SoftDegrade_StillWarnsAndDegrades` (FLEX-03) — `display:flex;flex-grow:1` + soft-degrade, flag off → `soft-degrade.display.flex` + `soft-degrade.flex-subproperty` Warnings, `Accepted=true`.

## Test Results (evidence)

- `dotnet build src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj -c Debug` → Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test tests/Muonroi.Pdf.Tests/...csproj -c Debug --filter "...AllowModernLayoutTests|...LegacyPrintPolicyTests"` → **Passed! Failed: 0, Passed: 13, Skipped: 0** (5 new + 8 existing `LegacyPrintPolicyTests`).
- `dotnet test tests/Muonroi.Pdf.Governance.Tests/...csproj -c Debug` → **Passed! Failed: 0, Passed: 11, Skipped: 0**.
- `git diff --stat HEAD -- tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyTests.cs` → empty (existing test file unmodified; FLEX-03 byte-for-byte invariant held).

## Deviations from Plan

None - plan executed exactly as written.

## TDD Gate Compliance

Task 2 is `tdd="true"`. Per plan, Task 1's gate landed first (commit `e84b138e`, `feat`), then the tests (commit `6ef497f2`, `test`) landed GREEN. The standard RED→GREEN ordering is inverted by design here because the plan sequences the implementation task before the test task; the gate's correctness is still proven by the 5 new facts plus the unchanged 8 pre-existing facts.

## Self-Check: PASSED

- FOUND: src/Muonroi.Pdf.Abstractions/PdfConfigs.cs (AllowModernLayout present)
- FOUND: src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs (allowModernLayout gate present)
- FOUND: tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyAllowModernLayoutTests.cs
- FOUND commit: e84b138e (feat 18-01 flag + gate)
- FOUND commit: 6ef497f2 (test 18-01)
