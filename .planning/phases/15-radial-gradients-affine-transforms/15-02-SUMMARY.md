---
phase: 15-radial-gradients-affine-transforms
plan: 02
subsystem: Muonroi.Pdf + Muonroi.Pdf.Governance
tags: [radial-gradient, pdf-shading, shadingtype-3, ellipse-ctm, policy-gate]
dependency_graph:
  requires: [15-01]
  provides: [15-02-radial-shading, 15-02-radial-render-tests]
  affects: [OwnedPdfWriter, GradientShadingRenderTests]
tech_stack:
  added: []
  patterns: [ShadingType-3 radial shading, anisotropic CTM ellipse, farthest-corner radius, clip-before-cm P3]
key_files:
  created: []
  modified:
    - src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs
    - tests/Muonroi.Pdf.Tests/Service/GradientShadingRenderTests.cs
decisions:
  - "Ellipse uses unit-circle ShadingType-3 /Coords [0 0 0 0 0 1] + anisotropic ellipseCm in content stream (D-04); circle uses /Coords [cx cy 0 cx cy r] with farthest-corner radius."
  - "Clip re W n emitted BEFORE any cm in radial shading block (Pitfall P3); this differs from linear-gradient block order and is critical for correct clip region."
  - "RadialGradient model/parser/policy gate all completed in 15-01; 15-02 scope is writer emission only."
  - "ellipseCm stored per-element in radialEllipseCms dictionary passed to BuildContentStream alongside gradientResNames."
metrics:
  duration: ~25 minutes
  completed: 2026-06-20
  tasks_completed: 3
  files_changed: 2
---

# Phase 15 Plan 02: Radial Gradient PDF Emission (ShadingType 3) Summary

PDF ShadingType 3 radial shading for `radial-gradient(...)` backgrounds — circle uses farthest-corner /Coords, ellipse uses unit-circle shading + anisotropic CTM scale, clip-before-cm ordering enforced.

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| 1: RadialGradient model + parser + policy gate | (15-01) | Already complete from Wave 1 |
| 2: BuildRadialShadingDict + writer emission | 9f4bf8a6 | ShadingType 3 dict + content stream radial block |
| 3: Radial render tests (flip + add) | 36bfbf10 | 3 new render tests; full suite 578/0 |

## What Was Built

### Task 1: (completed in 15-01 — no new work)

- `RadialGradient.cs` (model), `RadialGradientParser.cs`, `BoxNode.BackgroundRadialGradient`, `BoxTreeBuilder` radial parse, `LegacyPrintPolicy` gradient gate widened — all confirmed present and building from 15-01.

### Task 2: BuildRadialShadingDict + writer emission

`OwnedPdfWriter.cs` changes:

**`BuildRadialShadingDict(RadialGradient g, Rect rect, float pageHeightPt, out string? ellipseCm)`** added after `BuildAxialShadingDict`:
- Computes center in PDF coords: `cx = bgX + PositionX*w`, `cy = bgY + (1 - PositionY)*h` (PDF y-flip applied)
- **Circle:** farthest-corner radius = `max(dist(center, corner))` for all four corners; emits `/ShadingType 3 /Coords [cx cy 0 cx cy r]`; `ellipseCm = null`
- **Ellipse:** unit-circle `/Coords [0 0 0 0 0 1]`; `ellipseCm = "rx 0 0 ry cx cy cm"` where `rx = max(|cx-bgX|, |cx-(bgX+w)|)`, `ry = max(|cy-bgY|, |cy-(bgY+h)|)` (farthest-corner semi-axes)
- Reuses `BuildStitchingFunction` unchanged for 2-stop (FunctionType 2) and 3-stop (FunctionType 3)

**Shading loop** extended to also handle `BackgroundRadialGradient is { Stops.Count >= 2 }`; stores `ellipseCm` in `radialEllipseCms` dictionary passed to `BuildContentStream`.

**Radial content-stream emission block** (Pitfall P3 enforced):
```
q
[clip: x y w h re W n]   ← page user space FIRST
[element TransformFor cm] ← optional element transform
[ellipse cm]              ← optional anisotropic scale (ellipse only)
/ShN sh
Q
```

**Background-color guard** updated to skip when `BackgroundRadialGradient is not null` (prevents double-painting).

### Task 3: Radial render tests

Three new tests in `GradientShadingRenderTests.cs`:

- **`RadialGradient_EmitsRadialShading`**: default ellipse `radial-gradient(#fff,#000)` → asserts `/ShadingType 3`, `/Coords`, `/FunctionType 2`
- **`RadialGradientCircle_EmitsRadialShading`**: `radial-gradient(circle,#0c6b6b,#fff)` → asserts `/ShadingType 3`, `/Coords`
- **`RadialGradientThreeStop_UsesStitchingFunction`**: `radial-gradient(#f00,#0f0,#00f)` → asserts `/FunctionType 3`

## Deviations from Plan

### Auto-fixed Issues

None — plan executed exactly as written. All Task 1 artifacts were pre-built in 15-01; Task 2 and Task 3 followed the plan specification without deviation.

### Scope Notes

Task 1 in the plan's files list included `RadialGradient.cs`, `RadialGradientParser.cs`, `LinearGradientParser.cs`, and `LegacyPrintPolicy.cs` — all were already complete from 15-01. The build verified these compile correctly (0 errors, 0 MSTD0002, no CS0101). No re-work was needed. Task 2 was the core new implementation.

## Known Stubs

None. All implemented functionality is fully wired:
- Parser routes `radial-gradient(...)` to `BackgroundRadialGradient`
- Writer loop reads `BackgroundRadialGradient` and emits ShadingType 3
- Content stream emits clip + anisotropic cm + sh

## Threat Flags

None. `BuildRadialShadingDict` emits only `/Shading` + `/Function` dict entries — no `/JavaScript`, `/Launch`, or `/EmbeddedFile`. SEC-02 invariant unaffected.

## Verification Results

- `dotnet build src/Muonroi.Pdf/Muonroi.Pdf.csproj -c Debug`: 0 errors, 0 warnings (0 MSTD0002)
- `dotnet test tests/Muonroi.Pdf.Tests/ --filter "Category!=SlowIntegration&Category!=RealTemplate"`: **Passed! 541 passed, 0 failed**
- `dotnet test tests/Muonroi.Pdf.Tests/` (FULL including RealTemplate): **Passed! 578 passed, 0 failed**
- 17 TCIS RealTemplate goldens byte-identical (full suite green confirms no re-baseline)
- Radial render bytes contain `/ShadingType 3` + `/Coords`; 2-stop → `/FunctionType 2`, 3-stop → `/FunctionType 3`
- Ellipse clip ordering: `re W n` emitted before any `cm` (P3 enforced)
- Policy: `RadialGradient_IsAllowed` passes; `ConicGradient_IsRejected` and `RepeatingRadialGradient_IsRejected` pass

## Self-Check: PASSED

Files modified:
- `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs`: FOUND (contains BuildRadialShadingDict)
- `tests/Muonroi.Pdf.Tests/Service/GradientShadingRenderTests.cs`: FOUND (contains RadialGradient_EmitsRadialShading)

Commits exist:
- 9f4bf8a6: feat(15-02): BuildRadialShadingDict + radial emission in content stream (Task 2)
- 36bfbf10: feat(15-02): add radial gradient render tests (Task 3)
