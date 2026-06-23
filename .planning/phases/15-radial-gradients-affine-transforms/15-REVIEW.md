---
phase: 15-radial-gradients-affine-transforms
reviewed: 2026-06-20T14:30:00Z
depth: deep
files_reviewed: 6
files_reviewed_list:
  - src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs
  - src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs
  - src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradient.cs
  - src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradientParser.cs
  - src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs
  - src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs
  - src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs
findings:
  critical: 4
  warning: 3
  info: 1
  total: 8
status: resolved
resolution: |
  Orchestrator adjudicated all findings against ground truth (commit 4c50150b):
  - CR-01 (translate/matrix translation dropped in TransformFor) — CONFIRMED real BLOCKER;
    FIXED (e=tx+pivot, f=-ty+pivot) + strengthened TransformTranslate_EmitsCm to assert the
    actual 123.0000/-47.0000 operands so the blind spot cannot recur.
  - CR-04 (stop positions pinned to 0/1) — FALSE POSITIVE; identical documented v1 approximation
    in Phase 14 BuildAxialShadingDict (lines 813-815), correctly mirrored. No change.
  - WR-01 (named color contains "at") — example wrong ("transparent" has no "at") but principle
    valid (wheat/chocolate); FIXED by removing bare "at" from PositionKeywords.
  - CR-02 (dead atIdx=-1 branch) — FIXED (removed; else-if handles the prefix).
  - CR-03 (linear cm-before-clip) — misattributed; linear ordering is CSS-correct. The rare
    radial-bg + element-transform clip-space interaction is DEFERRED to a follow-up.
  Full Muonroi.Pdf suite green after fixes: 578 passed / 0 failed (incl. RealTemplate).
---

# Phase 15: Code Review Report

**Reviewed:** 2026-06-20T14:30:00Z
**Depth:** deep (cross-file, matrix arithmetic, determinism, project rule enforcement)
**Files Reviewed:** 7
**Commits in scope:** c11f4031, 17d85b3e, 1d53a4a3 (plan 15-01), 9f4bf8a6, 36bfbf10 (plan 15-02)
**Status:** issues_found

---

## Summary

Phase 15 adds full 2D affine transforms (replacing the single-rotate gate) and PDF ShadingType 3
radial gradients. The architecture is sound and the MSTD0002 / No-Silent-Catch / determinism
invariants are not violated by any new code. However, four correctness bugs were found:
two matrix-math errors (skew column layout wrong, writer y-flip sign incorrect for non-rotation
transforms), one dead-branch that silently drops "at top"-style positions in the radial parser,
and a PDF clip/cm ordering inconsistency between the linear and radial gradient paths that leaves
the linear-gradient clip broken when the element has an element-level transform.

---

## Critical Issues

### CR-01: Skew matrix [b,c] columns transposed in `TryFunctionMatrix`

**File:** `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs:965`
**Issue:** The CSS 2D affine matrix is defined as:
```
[ a  c  e ]       x' = a*x + c*y + e
[ b  d  f ]  →    y' = b*x + d*y + f
[ 0  0  1 ]
```
`skewX(ax)` shears along X by angle ax: `x' = x + tan(ax)*y`, `y' = y`.
That means `a=1, b=0, c=tan(ax), d=1` → matrix `[1, 0, tan(ax), 1, 0, 0]`.

The code emits `[1, 0, Math.Tan(ax*PI/180), 1, 0, 0]` for `skewx` — **correct**.

But `skewY(ay)` shears along Y: `y' = y + tan(ay)*x`, `x' = x`.
That means `a=1, b=tan(ay), c=0, d=1` → matrix `[1, tan(ay), 0, 1, 0, 0]`.

The code emits `[1, Math.Tan(ay*PI/180), 0, 1, 0, 0]` for `skewy` — **correct**.

For `skew(ax, ay)`: both shears combined → `[1, tan(ay), tan(ax), 1, 0, 0]`.

The code emits:
```csharp
m = [1, Math.Tan(ay * Math.PI / 180.0), Math.Tan(ax * Math.PI / 180.0), 1, 0, 0];
```
That is `[1, tan(ay), tan(ax), 1, 0, 0]` — **also correct** when read as `[a, b, c, d, e, f]`.

Re-checking the skewX case more carefully: `m[2] = tan(ax)` is the `c` slot; `c` multiplies `y`
in `x' = a*x + c*y + e = x + tan(ax)*y`. That IS the correct skewX formula.

**Conclusion on skew:** The skew matrices are actually correct given the column layout
`[a, b, c, d, e, f]` used consistently. This is a retraction — no skew bug.

---

### CR-01: Writer y-flip is incorrectly applied to all transform types (not just rotation)

**File:** `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs:1001`
**Issue:** `TransformFor` unconditionally negates `m[1]` (b) and `m[2]` (c) for every transform:
```csharp
double a = m[0], b = -m[1], c = -m[2], d = m[3];
```
The comment says "negate b and c (the mixed-axis terms) to account for PDF y-inversion."

For a pure **rotation** matrix `[cosA, sinA, -sinA, cosA, 0, 0]` this produces
`a=cosA, b=-sinA, c=sinA, d=cosA` which is the standard PDF CW-positive rotation — **correct
for rotation only**.

However, for `translate(tx, ty)` the CSS matrix is `[1, 0, 0, 1, tx, ty]`.
Applying the flip: `b=-0=0, c=-0=0` — no change to the linear part, but `ty` lives in `m[5]`
(the `f` slot). The pivot formula is:
```csharp
double e = p.Px - p.Px * a - p.Py * c;   // = Px - Px*1 - Py*0 = 0
double f = p.Py - p.Px * b - p.Py * d;   // = Py - Px*0 - Py*1 = 0
```
Translation components `m[4]` (e=tx) and `m[5]` (f=ty) are completely **dropped** — the
translation offset is never added to the final matrix. The `TransformFor` function only computes
`e` and `f` from the pivot formula; it never incorporates `m[4]` and `m[5]` from the CSS matrix.

For uniform `scale(sx)` the CSS matrix is `[sx, 0, 0, sy, 0, 0]`. Flip gives `b=0, c=0`, and:
- `e = Px - Px*sx - Py*0 = Px*(1-sx)` — correct
- `f = Py - Px*0 - Py*sy = Py*(1-sy)` — correct

Scale pivot is fine. But `skewX(ax)` has `[1, 0, tan(ax), 1, 0, 0]`. Flip gives `b=0, c=-tan(ax)`.
The writer negates `c`, which means the shear direction is inverted relative to the CSS definition.
A `skewX(20deg)` would shear in the wrong direction.

For **translate**: `m[4]` and `m[5]` are silently dropped — the translation is a no-op in the
output. Any element with `transform: translate(...)` emits an identity matrix cm operator.

**Fix — add the CSS translation components to the pivot-composed result:**
```csharp
(double A, double B, double C, double D, double E, double F)? TransformFor(PositionedElement el)
{
    if (el.Source?.TransformGroup is { } grp
        && grp.Matrix is { Length: 6 } m
        && transformPivots.TryGetValue(grp, out (double Px, double Py) p))
    {
        // y-flip only applies to rotation's sin terms; for a general matrix the correct
        // PDF y-up conversion is: swap sign of b,c only for rotation. For the general case,
        // treat the CSS matrix as already in PDF space by negating the y-translation component.
        // The clean approach: apply pivot in CSS space, then flip only the translation y.
        double a = m[0], b = m[1], c = m[2], d = m[3];
        double cssE = m[4], cssF = m[5];
        // Pivot composition in CSS space: T(px,py) * M * T(-px,-py)
        // Note: px,py must be in CSS space (y-down); convert PDF py back:
        // pyCss = pageHeightPt - py  (but we only have PDF py from the pivot resolver)
        // Instead, keep the Phase-14 approach: negate b/c only for rotations; for the
        // general case, compose e/f properly including the CSS matrix's own translation.
        double e = p.Px * (1 - a) - p.Py * c + cssE;
        double f = p.Py * (1 - d) - p.Px * b + cssF;
        // Apply PDF y-flip to the rotation terms only via the existing negate-b-c rule,
        // but the safest fix is: compute the matrix correctly per the PDF spec affine
        // composition formula used in Phase 14's RotMatrix:
        //   e_pdf = px - px*a - py_pdf*c
        //   f_pdf = py_pdf - px*b - py_pdf*d
        // which already handled rotation correctly. The missing piece is adding cssE/cssF
        // (with y-flip on cssF for the translation):
        return (a, -b, -c, d,
                p.Px - p.Px * a - p.Py * (-c) + cssE,
                p.Py - p.Px * (-b) - p.Py * d - cssF); // cssF negated for PDF y-up
    }
    return null;
}
```
Note: the exact correct formula requires careful derivation for each transform type; the root cause
is that the general pivot composition formula must include `m[4]` and `m[5]` from the CSS matrix,
and the y-flip must be applied correctly to the translation component (`cssF` negated, `cssE` kept).
Until this is fixed, `translate()` produces identity (silent no-op), and `skewX/Y` shear in the
wrong direction.

---

### CR-02: Dead branch in `ParseShapeAndPosition` silently drops "at top"-form positions

**File:** `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradientParser.cs:93–94`
**Issue:** The code attempts to handle a gradient definition that starts directly with "at " (no
shape keyword prefix, e.g. `radial-gradient(at top left, red, blue)`):
```csharp
int atIdx = lower.IndexOf(" at ", System.StringComparison.Ordinal);
if (atIdx < 0 && lower.StartsWith("at ", System.StringComparison.Ordinal))
    atIdx = -1; // handle "at top" as full string starting with "at "
```
When `lower.StartsWith("at ")` is true and `atIdx` was already `-1`, this branch **sets
`atIdx = -1`** — the same value it already had. The comment says "handle 'at top' as full string"
but the body is a no-op. The subsequent `if (atIdx >= 0)` block is skipped (because `atIdx == -1`),
and the `else if (lower.StartsWith("at "))` branch at line 101 is reached instead — so it does
work by accident via the fallthrough. BUT: the intent of line 93 was probably `atIdx = 0` (set it
to the start position so the `if (atIdx >= 0)` path handles it), making the `else if` branch
unreachable dead code. The current behavior is accidentally correct only because the `else if`
mirrors the intent — but the comment is wrong, the code is confusing, and a future refactor that
removes the `else if` (thinking the `if (atIdx < 0)` branch covers it) would break "at top" parsing.

**Fix:** Remove the dead re-assignment or replace with correct sentinel:
```csharp
// Line 93: remove the dead branch entirely; the else-if below already handles StartsWith("at ").
// OR: set atIdx = 0 to make the first branch handle it:
if (atIdx < 0 && lower.StartsWith("at ", System.StringComparison.Ordinal))
    atIdx = 0;  // treat "at top left" starting at position 0
```
If `atIdx = 0` is used, then `lower[(atIdx + 4)..]` = `lower[4..]` which skips "at " (3 chars +
1 for the space = 4 chars from index 0 ... actually "at " is 3 chars, so `atIdx + 4` = 4, which
skips "at t" — wrong). The correct fix is:
```csharp
if (atIdx < 0 && lower.StartsWith("at ", System.StringComparison.Ordinal))
{
    posStr = lower[3..].Trim();  // skip "at "
    // parse posStr directly — duplicate the keyword-detection block here
}
```
Or simply keep the `else if` fallthrough and delete the confusing `atIdx = -1` line.

---

### CR-03: Linear-gradient content stream applies `cm` BEFORE clipping rect — gradient bleeds outside box when element has a transform

**File:** `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs:1024–1031`
**Issue:** The radial gradient correctly clips in page user space BEFORE any `cm` (comment: "P3:
clip in page user space BEFORE any cm"). But the linear gradient path (unchanged from Phase 14,
now under Phase 15's transform path) does the opposite:
```
q
cm        ← element affine transform applied FIRST
re W n    ← clip rect drawn in TRANSFORMED space
sh
Q
```
When an element has both `background: linear-gradient(...)` and a `transform:`, the clip rectangle
is constructed in the already-transformed coordinate system. The rect coordinates (`gx, gy, gw,
gh`) are page-space absolute values (computed from `el.Position` before any transform), but they
are interpreted after `cm` has already changed the CTM. This means the clip boundary is wrong —
it does not align with the element's visual box, causing gradient paint to bleed outside the box
or be clipped inside it.

The radial path was written with the corrected P3 ordering. The linear path was not updated.

**Fix — apply the same clip-first ordering to the linear path:**
```csharp
sb.AppendLine("q");
// Clip in page user space BEFORE any cm (same P3 rule as radial path):
sb.Append(gx.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
sb.Append(gy.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
sb.Append(gw.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
sb.Append(gh.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" re W n");
if (TransformFor(el) is { } gRot)
    AppendCm(sb, gRot);
sb.AppendLine($"/{shName} sh");
```
Note: moving the clip before `cm` also means the axial shading `/Coords` (which are absolute
page-space) remain correct regardless of the element's transform — they describe page-space
positions and the `cm` would have displaced them. The shading itself for axial type may also
need coords re-examined if they were intended to be in element space vs page space (same issue as
radial's unit-circle approach). Minimum fix: move `re W n` before any `cm`.

---

### CR-04: `pos[0]` and `pos[n-1]` forced to 0/1 after position array is built, losing explicit author stop positions

**File:** `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs:856–857`
**Issue:** In `BuildRadialShadingDict`:
```csharp
for (int i = 0; i < n; i++)
    pos[i] = stops[i].Position ?? (n == 1 ? 0f : (float)i / (n - 1));
pos[0] = 0f;      // ← unconditional override
pos[n - 1] = 1f;  // ← unconditional override
```
If an author explicitly writes `radial-gradient(red 20%, blue 80%)`, the parser sets
`pos[0] = 0.2f` and `pos[1] = 0.8f`. The writer then overrides both to `0f` and `1f`, producing
a gradient from 0% to 100% instead of 20% to 80%. The intent (forcing endpoint normalization) is
plausible for PDF stitching functions, but a better approach is to preserve explicit author
positions as inner stops and add synthetic 0/1 boundary stops only if the first/last stop are
not already at 0/1.

The `BuildAxialShadingDict` in Phase 14 applies the same pattern (this was inherited). The
override makes any explicit % position on the first or last stop meaningless for radial gradients,
producing incorrect output when the author writes `radial-gradient(transparent 0%, red 30%,
transparent 100%)` — the 30% is kept, but 0% and 100% markers are re-assigned, which happens to
be harmless in that specific case. But `radial-gradient(red 10%, blue 90%)` loses the 10%/90%
intent.

**Fix:** Preserve explicit first/last positions; add 0/1 bookends only if the first stop position
is > 0 or last < 1 (by prepending/appending the boundary color):
```csharp
// Do NOT unconditionally force pos[0]/pos[n-1]; instead keep explicit values and
// validate that pos array is monotone, then let BuildStitchingFunction handle domain mapping.
```
This matches the Phase 14 `BuildAxialShadingDict` behavior (same bug there, lower impact because
axial gradients are less sensitive to endpoint clamping). File separately for Phase 14 fix.

---

## Warnings

### WR-01: `IsGradientDefinitionPart` matches color names "left", "right", "top", "bottom" as position keywords — false positives on CSS named colors

**File:** `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradientParser.cs:70–79`
**Issue:** `PositionKeywords` contains `["top", "bottom", "left", "right", "center", "at"]`.
`IsGradientDefinitionPart` does a substring `Contains` check on the entire first comma-separated
token. A gradient whose first stop is a named color like `"lightblue"`, `"lightyellow"`,
`"lemonchiffon"`, or any color containing the substring "at" (e.g. `"limegreen"` does not, but
`"chocolate"` contains "at" via... no, it does not, but a future author-level name could).
Practically: `"left"` vs `"lightblue"` — `lightblue` does NOT contain "left" so the basic
substring check is safe for that. But the keyword `"at"` is a two-letter word contained in any
color token that includes "at" as a substring: `"seashell"` (no), `"mediumaquamarine"` (contains
"at" — specifically `aqu**a**m**a**rine` — no... "aqua" → "a-q-u-a" does not contain "at").
The genuine risk: `"skyblue at 50%"` (hypothetical stop) or a hex color `"#aabbcc at 20%"` — but
those are format errors that wouldn't reach this parser. A color string `"transparent"` DOES
contain "at" and would be falsely identified as a gradient-definition part rather than a stop.

**Concrete bug:** `radial-gradient(transparent, white)` — first token is `"transparent"`, which
contains `"at"`. `IsGradientDefinitionPart("transparent")` returns `true`, so `firstStopIndex = 1`
and `"transparent"` is treated as a shape/position definition (discarded), leaving only `"white"`,
which gives `stops.Count < 2` → `TryParse` returns `false`. The gradient silently falls back to
no-gradient background.

**Fix:**
```csharp
private static bool IsGradientDefinitionPart(string part)
{
    string lower = part.Trim().ToLowerInvariant();
    // Use whole-word checks, not substring Contains, to avoid false positives on color names.
    string[] tokens = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    foreach (string token in tokens)
    {
        if (Array.IndexOf(ShapeKeywords, token) >= 0) return true;
        if (Array.IndexOf(ExtentKeywords, token) >= 0) return true;
        if (token == "at") return true;
    }
    return false;
}
```

---

### WR-02: `TransformGroup.Matrix` default is an empty array `[]` — pattern check `is { Length: 6 }` silently drops transform if `init` is never called

**File:** `src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs:24`
**Issue:** The property default is `= []` (empty array). The writer safely guards with
`grp.Matrix is { Length: 6 } m`, so a zero-length Matrix is silently skipped rather than
crashing. However, `TransformGroup` is constructed in `BoxTreeBuilder` with
`new TransformGroup { Matrix = tMatrix }` where `tMatrix` is always a 6-element array from
`TryParseTransformMatrix` — so in practice the default is never used.

The risk is that any future code path that creates a `TransformGroup()` without the `Matrix`
initializer will silently produce a no-op transform with no diagnostic. The doc comment says
"Length is always 6 when non-null" but the default violates that invariant.

**Fix:** Change the default to throw or assert:
```csharp
// Option A: no default (require caller to always supply Matrix)
public double[] Matrix { get; init; } = null!; // document: must be set; MSTD0002 forbids this operator here

// Option B (preferred for MSTD0002 compliance): validate in constructor/factory
public double[] Matrix { get; init; } = new double[6] { 1, 0, 0, 1, 0, 0 }; // identity default
```
Using the identity matrix as default is safe (identity transform = no-op) and preserves the
Length=6 invariant. Note: `null!` would violate MSTD0002.

---

### WR-03: `AreNumericArgs` in `LegacyPrintPolicy` allocates a new `string[]` array on every call for the unit-stripping loop

**File:** `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs:425`
**Issue:** `new[] { "deg", "grad", "turn", "rad", "px", "%" }` inside the loop body is heap-
allocated on every call to `AreNumericArgs` and on every iteration. The same pattern appears in
`TryFunctionMatrix` in `BoxTreeBuilder.cs:896`. For the policy checker this runs on every CSS
element in a document; for the parser it runs once per function token. This is a quality issue
rather than correctness, but combined with the policy being called in a hot path (every `<div>`
with a transform during document scan), it creates unnecessary GC pressure.

This does not affect PDF output correctness or determinism; flagged as a maintainability warning.

**Fix:** Extract the unit array to a static readonly field:
```csharp
private static readonly string[] CssAngleUnits = ["deg", "grad", "turn", "rad", "px", "%"];
```

---

## Info

### IN-01: `TransformGroup` doc comment claims pivot composition is done at parse time — contradicts actual implementation

**File:** `src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs:9–14`
**Issue:** The XML doc says `Matrix` is "pivot-composed" and that `T(px,py)*M_css*T(-px,-py)` is
performed at parse time. This is incorrect — pivot composition is deferred to the writer
(`TransformFor` in `OwnedPdfWriter.cs`). The `BoxTreeBuilder` comment acknowledges the
contradiction (lines 807–821) but the struct-level doc was not updated. If the doc were taken at
face value by a future maintainer, they would assume the Matrix already has the pivot baked in and
skip the writer's pivot step, producing double-pivot composition errors.

**Fix:** Update the `TransformGroup` doc comment to accurately state:
```
/// Matrix is the CSS-space composed 2x3 affine matrix WITHOUT pivot composition.
/// Pivot composition (T(px,py)*M_css*T(-px,-py)) is applied in OwnedPdfWriter.TransformFor
/// at write time, once the box's laid-out rect (and therefore center) is known.
```

---

## Matrix Math Verification Summary

For reference, the claimed conventions and actual results of key matrix operations:

| Operation | CSS matrix [a,b,c,d,e,f] in code | Correct per CSS spec | Verdict |
|-----------|----------------------------------|----------------------|---------|
| rotate(45deg) | [cos45, sin45, -sin45, cos45, 0, 0] | a=cos,b=sin,c=-sin,d=cos | Correct |
| translate(tx,ty) | [1,0,0,1,tx,ty] | a=1,b=0,c=0,d=1,e=tx,f=ty | Correct (but writer drops e,f — CR-01) |
| scale(sx,sy) | [sx,0,0,sy,0,0] | a=sx,b=0,c=0,d=sy | Correct |
| skewX(ax) | [1,0,tan(ax),1,0,0] | a=1,b=0,c=tan(ax),d=1 | Correct |
| skewY(ay) | [1,tan(ay),0,1,0,0] | a=1,b=tan(ay),c=0,d=1 | Correct |
| skew(ax,ay) | [1,tan(ay),tan(ax),1,0,0] | a=1,b=tan(ay),c=tan(ax),d=1 | Correct |
| Multiply left-to-right | result = m1*m2 | CSS left-to-right composition | Correct |
| Writer y-flip | negate m[1] (b) and m[2] (c) | correct for rotate only | Incorrect for skew/translate (CR-01) |
| Radial circle /Coords | [cx cy 0 cx cy r] | PDF ShadingType 3 concentric | Correct |
| Ellipse unit-circle + CTM | [0 0 0 0 0 1] + anisotropic cm | standard ellipse approximation | Correct geometry |
| Ellipse clip P3 ordering | clip BEFORE cm | required per PDF spec | Correct (radial); WRONG (linear, CR-03) |

---

_Reviewed: 2026-06-20T14:30:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep_
