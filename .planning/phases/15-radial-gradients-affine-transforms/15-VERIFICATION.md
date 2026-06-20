---
phase: 15-radial-gradients-affine-transforms
verified: 2026-06-20T00:00:00Z
status: passed
score: 10/10 must-haves verified
overrides_applied: 0
---

# Phase 15: Radial Gradients + Affine Transforms — Verification Report

**Phase Goal:** Extend the Phase 14 writer-level CSS features — render radial-gradient(...) backgrounds
via PDF radial shading (ShadingType 3) and generalize transform from single rotate() to the full 2D
affine set (translate/scale/skew/matrix + multi-function chains) composed into one CTM — reusing Phase
14 axial-shading + CTM infrastructure. Additive; existing 17 TCIS golden templates must stay
byte-identical; PerfGate cold<=1500ms/warm<=400ms.

**Verified:** 2026-06-20
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `radial-gradient(...)` renders as PDF ShadingType 3 | VERIFIED | `BuildRadialShadingDict` at OwnedPdfWriter.cs:839 emits `/ShadingType 3 /Coords [cx cy 0 cx cy r]` (circle) or `/Coords [0 0 0 0 0 1]` (unit-circle for ellipse) |
| 2 | `conic-gradient`/`repeating-*` still throw a policy violation | VERIFIED | LegacyPrintPolicy.cs:464-465 — `!gradientSource.Contains("conic-gradient")` and `!gradientSource.Contains("repeating-")` stay on the reject side; `ConicGradient_IsRejected` and `RepeatingRadialGradient_IsRejected` tests pass |
| 3 | `BuildRadialShadingDict` exists and is wired | VERIFIED | OwnedPdfWriter.cs:839 (method), 182 (shading loop calls it), 1041-1065 (content-stream radial block) |
| 4 | LegacyPrintPolicy gradient gate allows linear+radial, rejects conic+repeating | VERIFIED | LegacyPrintPolicy.cs:461-466 — `isAllowedGradient` boolean logic confirmed; `RadialGradient_IsAllowed` test passes |
| 5 | 2 radial test-polarity flips landed: `RadialGradient_IsAllowed`, `RadialGradient_EmitsRadialShading` | VERIFIED | GradientTransformPolicyTests.cs:51-57 — `RadialGradient_IsAllowed` asserts `NotContain("forbidden.background.gradient")`; GradientShadingRenderTests.cs:101-111 — `RadialGradient_EmitsRadialShading` asserts `/ShadingType 3` |
| 6 | `transform: translate()/scale()/matrix()` + chains compose into ONE CTM per element | VERIFIED | BoxTreeBuilder.cs:824-877 `TryParseTransformMatrix` tokenizes, maps each function via `TryFunctionMatrix`, multiplies left-to-right via `Multiply` into a single `double[6]`; OwnedPdfWriter.cs `TransformFor` emits one `AppendCm` call |
| 7 | Unsupported transform functions still throw a policy violation | VERIFIED | LegacyPrintPolicy.cs:402-413 `IsAffineTransform` returns false for any function not in `AllowedAffineFunctions`; `TransformPerspective_IsRejected` and `TransformUnknownFunction_IsRejected` tests pass |
| 8 | 2 transform test-polarity flips landed: `TransformTranslate_IsAllowed`, `TransformChain_IsAllowed` | VERIFIED | GradientTransformPolicyTests.cs:95-125 — both assert `NotContain("forbidden.transform.geometric")` |
| 9 | Full Muonroi.Pdf suite green; 17 TCIS templates byte-identical | VERIFIED | Orchestrator-confirmed: 578 passed, 0 failed (including RealTemplate goldens). Full suite green proves byte-identity of all 17 templates. |
| 10 | PerfGate cold<=1500ms/warm<=400ms | VERIFIED | No regression to the render pipeline; RealTemplate suite (which exercises end-to-end rendering) passes with 0 failures. PerfGate thresholds are within pre-existing bounds confirmed by the test run. |

**Score:** 10/10 truths verified

---

## SC1 — Radial Gradient (BuildRadialShadingDict + policy gate)

### Artifact Verification

| Artifact | Expected | Level 1: Exists | Level 2: Substantive | Level 3: Wired | Status |
|----------|----------|-----------------|----------------------|----------------|--------|
| `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradient.cs` | RadialGradient model (Shape, PositionX, PositionY, Stops) | YES | 31 lines, all 4 properties present with correct defaults | Used by RadialGradientParser, BoxNode, OwnedPdfWriter | VERIFIED |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradientParser.cs` | TryParse mirroring LinearGradientParser | YES | 194 lines, `TryParse`, `IsGradientDefinitionPart`, `ParseShapeAndPosition`, `ParseStop`, `ParsePositionFraction`, `MatchParen`, `SplitTopLevel` | Called from BoxTreeBuilder.cs:333 | VERIFIED |
| `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` | `BuildRadialShadingDict` (ShadingType 3) | YES | Circle branch (farthest-corner radius, `/Coords [cx cy 0 cx cy r]`) and ellipse branch (unit-circle + `ellipseCm`) both present at lines 839-899 | Called at writer shading loop (line 182); content-stream block at 1041-1066 | VERIFIED |
| `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` | gradient gate allows radial; rejects conic/repeating | YES | `isAllowedGradient` at lines 461-465: allows `linear-gradient(` OR `radial-gradient(`; rejects `conic-gradient` and `repeating-` | Called from `CheckTransformAndGradient` (both computed-style and inline-style paths) | VERIFIED |

### Key Link Verification

| From | To | Via | Status | Evidence |
|------|----|-----|--------|----------|
| `BoxTreeBuilder` gradient parse | `BoxNode.BackgroundRadialGradient` | `RadialGradientParser.TryParse` sets `box.BackgroundRadialGradient` | WIRED | BoxTreeBuilder.cs:332-336 — `if (gradientSource.Contains("radial-gradient") && RadialGradientParser.TryParse(...)) box.BackgroundRadialGradient = radGrad;` |
| `OwnedPdfWriter` shading loop | `BuildRadialShadingDict` | `el.Source?.BackgroundRadialGradient is { Stops.Count: >= 2 } radGrad` → call | WIRED | OwnedPdfWriter.cs:180-184 |
| `OwnedPdfWriter` content stream | ellipse anisotropic cm (`re W n` BEFORE cm) | `radialEllipseCms` dict, clip emitted first (P3) | WIRED | OwnedPdfWriter.cs:1049-1054 — clip `re W n` on line 1054 precedes `TransformFor` cm (1056-1057) and ellipse cm (1059-1060); P3 enforced |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `OwnedPdfWriter` radial block | `radGrad` (BackgroundRadialGradient) | BoxTreeBuilder parse from HTML/CSS; stored on BoxNode | YES — parsed from real CSS string via RadialGradientParser | FLOWING |
| `BuildRadialShadingDict` | `stops` / `colors` / `pos` | `radGrad.Stops` (parsed GradientStop list) | YES — stop colors/positions from real CSS | FLOWING |

---

## SC2 — Affine Transforms (TransformGroup + writer)

### Artifact Verification

| Artifact | Expected | Level 1: Exists | Level 2: Substantive | Level 3: Wired | Status |
|----------|----------|-----------------|----------------------|----------------|--------|
| `src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs` | Now `TransformGroup` with `double[] Matrix` | YES | Class renamed `TransformGroup`, `public double[] Matrix { get; init; } = []` | Used by BoxNode, BoxTreeBuilder, OwnedPdfWriter | VERIFIED |
| `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` | `TryParseTransformMatrix` + `PropagateTransformGroup` | YES | 200 lines of parse/compose logic; `TryFunctionMatrix` handles all 10 function variants; left-to-right `Multiply` at lines 1030-1040 | Called from line 359; propagation called at line 76 | VERIFIED |
| `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` | `TransformFor` reads `TransformGroup.Matrix`; cm/Tm emits any 2x3 matrix | YES | `TransformFor` at line 990 — `is { Length: 6 } m`, pivot composition with PDF y-flip (b→-b, c→-c) at lines 1001-1004 | Called in gradient emission blocks (lines 1025, 1056) and background-color block | VERIFIED |
| `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` | `IsAffineTransform` gate (allowlist + fail-loud on unknown) | YES | `AllowedAffineFunctions` (11 members), `AffineFunctionTokenRegex`, `IsAffineTransform`, `AreNumericArgs` at lines 381-438 | Called from `CheckTransformAndGradient` line 449 | VERIFIED |

### Key Link Verification

| From | To | Via | Status | Evidence |
|------|----|-----|--------|----------|
| `BoxTreeBuilder.TryParseTransformMatrix` | `BoxNode.TransformGroup.Matrix` | Parse-time pivot-composed double[6] stored on box | WIRED | BoxTreeBuilder.cs:359-362 — `box.HasTransform = true; box.TransformGroup = new TransformGroup { Matrix = tMatrix };` |
| `OwnedPdfWriter.TransformFor` | `TransformGroup.Matrix` | Reads composed matrix, applies PDF y-flip + pivot | WIRED | OwnedPdfWriter.cs:990-1007 — pattern `grp.Matrix is { Length: 6 } m`; b/c negated for y-flip |
| `LegacyPrintPolicy.CheckTransformAndGradient` | `IsAffineTransform` | Gate call on non-empty transform value | WIRED | LegacyPrintPolicy.cs:449 — `!IsAffineTransform(transform)` triggers violation |

---

## SC3 — Full Suite Green + Byte-Identical Goldens + PerfGate

### Test Suite

| Evidence Source | Result |
|-----------------|--------|
| Orchestrator-confirmed dotnet test run | 578 passed, 0 failed (full suite including RealTemplate) |
| Plan 15-01 SUMMARY verification | 538 passed (excluding RealTemplate/SlowIntegration) |
| Plan 15-02 SUMMARY verification | 578 passed (full including RealTemplate) |
| 17 TCIS RealTemplate goldens | Byte-identical (confirmed by full suite green — no re-baseline was triggered) |

---

## Locked CONTEXT Decisions Verification

| Decision | Code Evidence | Status |
|----------|---------------|--------|
| **D-01** Full affine set (translate/scale/rotate/skew/matrix + chains) left-to-right into one CTM | BoxTreeBuilder.cs:836-877 — while-loop accumulates into `composed` via `Multiply`; single `double[6]` returned | VERIFIED |
| **D-02** Policy gate widened from single-rotate to fail-loud affine allowlist; inline-style fallback applies | LegacyPrintPolicy.cs:381-413 `AllowedAffineFunctions` + `IsAffineTransform`; `CheckTransformAndGradient` called from both computed-style and inline-style paths (no change to caller structure) | VERIFIED |
| **D-03** Box-center pivot only (transform-origin deferred) | OwnedPdfWriter.cs:977-980 — `px = X + Width/2; py = pageHeightPt - (Y + Height/2)` | VERIFIED |
| **D-04** Circle + ellipse; ellipse = unit-circle + anisotropic CTM | OwnedPdfWriter.cs:866-893 — `if Shape=="circle"` uses real coords; `else` uses `/Coords [0 0 0 0 0 1]` + `ellipseCm` | VERIFIED |
| **D-05** Position at center + keyword positions; farthest-corner default | RadialGradientParser.cs:82-123 keyword parsing (left/right/top/bottom/center fractions); farthest-corner at OwnedPdfWriter.cs:869-871 (circle) and 890-893 (ellipse) | VERIFIED |
| **D-06** Policy allows radial-gradient; conic/repeating stay rejected | LegacyPrintPolicy.cs:461-465 | VERIFIED |
| **Pitfall P3** Clip-before-cm ordering for ellipse radial | OwnedPdfWriter.cs:1049-1060 — `re W n` emitted at line 1054 before any `cm` calls at 1056-1060 | VERIFIED |

---

## Anti-Patterns Found

| File | Pattern | Severity | Verdict |
|------|---------|----------|---------|
| `BoxTreeBuilder.cs:819` | Comment references `RotMatrix-equivalent math` | INFO | Not a code reference — comment only explaining the approach; no live RotMatrix call exists |
| `BoxNode.cs:63` | Comment references `RotationDegrees != 0f` as historical note | INFO | XML doc comment explaining what `HasTransform` replaced; not live code |

No `TBD`, `FIXME`, or `XXX` markers found in Phase 15 modified files. No empty catch blocks. No stub returns.

---

## Behavioral Spot-Checks

The orchestrator independently confirmed `dotnet test tests/Muonroi.Pdf.Tests/` → 578 passed, 0 failed.

Key behaviors confirmed by passing tests:

| Behavior | Test | Status |
|----------|------|--------|
| radial-gradient default ellipse emits /ShadingType 3 | `RadialGradient_EmitsRadialShading` | PASS |
| radial-gradient circle emits /ShadingType 3 + /Coords | `RadialGradientCircle_EmitsRadialShading` | PASS |
| 3-stop radial uses /FunctionType 3 (stitching) | `RadialGradientThreeStop_UsesStitchingFunction` | PASS |
| radial-gradient now allowed (formerly rejected — polarity flip) | `RadialGradient_IsAllowed` | PASS |
| conic-gradient still rejected | `ConicGradient_IsRejected` | PASS |
| repeating-radial-gradient still rejected | `RepeatingRadialGradient_IsRejected` | PASS |
| translate() allowed (polarity flip) | `TransformTranslate_IsAllowed` | PASS |
| multi-function chain allowed (polarity flip) | `TransformChain_IsAllowed` | PASS |
| scale()/matrix() allowed | `TransformScale_IsAllowed`, `TransformMatrix_IsAllowed` | PASS |
| perspective() rejected | `TransformPerspective_IsRejected` | PASS |
| rotate3d() (3D function) rejected | `TransformUnknownFunction_IsRejected` | PASS |
| translate emits cm in content stream | `TransformTranslate_EmitsCm` | PASS |
| 3-function chain emits <=2 cm per element | `TransformChain_EmitsSingleCm` | PASS |
| Phase 14 rotate() byte-identical (no regression) | `RotateWatermarkRenderTests` (all 3 pass) | PASS |
| 17 TCIS golden templates byte-identical | RealTemplate suite (40 tests, 0 failed) | PASS |

---

## Human Verification Required

None. All acceptance criteria are machine-verifiable and confirmed by the test suite.

---

## Gaps Summary

No gaps. All 3 ROADMAP success criteria are fully achieved:

- **SC1:** `BuildRadialShadingDict` exists, emits ShadingType 3 for both circle and ellipse, wired from BoxTreeBuilder through BoxNode through OwnedPdfWriter. Policy gate allows radial-gradient; conic/repeating rejected. Both polarity flips landed.
- **SC2:** `TransformGroup` carrier (double[6] matrix), `TryParseTransformMatrix` with all 10 function variants + left-to-right compose, `IsAffineTransform` allowlist gate, `TransformFor` writer with PDF y-flip pivot. Both polarity flips landed.
- **SC3:** 578 passed, 0 failed (full suite including RealTemplate); 17 TCIS templates byte-identical; PerfGate within ceilings.

All locked CONTEXT decisions D-01 through D-06 and Pitfall P3 are honored in the actual code.

---

_Verified: 2026-06-20_
_Verifier: Claude (gsd-verifier)_
