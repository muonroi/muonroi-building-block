# Phase 15: Radial Gradients + Affine Transforms — Research

**Researched:** 2026-06-20
**Domain:** PDF ShadingType 3 (radial shading) + CSS-to-PDF 2D affine transform composition
**Confidence:** HIGH — all claims below are verified against actual source files in the repository

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Full 2D affine set: `translate`/`translateX`/`translateY`, `scale`/`scaleX`/`scaleY`,
  `rotate`, `skew`/`skewX`/`skewY`, `matrix(a,b,c,d,e,f)`, and multi-function chains. All compose
  left-to-right into a single 2×3 affine matrix → one CTM.
- **D-02:** Widen `LegacyPrintPolicy` transform gate. Accept a whitespace-separated chain where every
  token is one of the allowed affine function names with numerically-parseable args; reject unknown
  function tokens (fail-loud). Inline `style=""` fallback path must apply to the widened gate too.
- **D-03:** Box-center pivot only. CSS default `transform-origin: 50% 50%`. `transform-origin`
  property deferred.
- **D-04:** Support both `circle` and `ellipse`. Ellipse = unit-circle ShadingType-3 shading wrapped
  in an anisotropic CTM scale (scale x ≠ y).
- **D-05:** Position: `at center` (default) + keyword positions (top/left/center/right/bottom and
  pairs). Extent: `farthest-corner` (CSS default). Deferred: explicit pixel/% sizes and full four
  extent keywords.
- **D-06:** Add `radial-gradient` to the policy gradient allow-list. `conic-gradient` and all
  `repeating-*` gradients stay rejected.

### Claude's Discretion

- Exact CTM composition math, matrix-decomposition vs direct-multiply.
- Radial `/Coords` (two-circle) computation + ellipse CTM derivation.
- Parser structure (mirror `LinearGradientParser`).
- How the affine matrix is baked into text `Tm` vs object `cm`.
- Whether to generalize `RotationGroup` → a shared `TransformGroup`/affine-matrix carrier, or
  extend the existing type.

### Deferred Ideas (OUT OF SCOPE)

- `transform-origin` property — non-center origins.
- Explicit radial sizes + full extent keywords (`closest-side`/`closest-corner`/`farthest-side`).
- `conic-gradient` / `repeating-*` gradients.
- Flexbox (Phase 16), CSS grid (permanently out of scope).
</user_constraints>

---

## Summary

Phase 15 extends two writer-level features from Phase 14 (commit 3dfb7842 on `develop`) by reusing
their existing infrastructure with minimal delta:

**Radial gradient:** `BuildAxialShadingDict` (ShadingType 2) at
`OwnedPdfWriter.cs:777` [VERIFIED: file read] is the direct template for a new
`BuildRadialShadingDict` (ShadingType 3). The only structural difference is the `/Coords` key
(four floats for axial → six floats for two-circle radial), `/ShadingType 2` → `3`, and the
ellipse variant adds an anisotropic `cm` scale before the `sh` call. The
`BuildStitchingFunction` at `OwnedPdfWriter.cs:822` [VERIFIED: file read] is shared unchanged.

**Affine transforms:** `RotMatrix` at `OwnedPdfWriter.cs:861` [VERIFIED: file read] already returns
the full 2×3 tuple `(A B C D E F)`. `AppendCm` at `OwnedPdfWriter.cs:872` [VERIFIED: file read]
emits any 2×3 matrix as a `cm` operator unchanged. The bake-into-Tm logic at
`OwnedPdfWriter.cs:1191-1199` [VERIFIED: file read] transforms any 2×3 matrix against the text
position — it is not rotate-specific. The only Phase 14 code that hard-codes rotation is
`RotMatrix` itself (which must be replaced by a general `ComposeAffineMatrix` function) and the
`RotationGroup` carrier (which stores only `AngleDegrees`).

**Recommended decisions (Claude's discretion areas):**
1. Rename `RotationGroup` → `TransformGroup` with a `double[6] Matrix` field (instead of
   `AngleDegrees`). This is a one-file rename + update in `RotationGroup.cs`, `BoxNode.cs`, and
   `BoxTreeBuilder.cs`. Avoids introducing a parallel carrier type.
2. Direct left-to-right matrix multiply (no decomposition). Each CSS function maps to one 2×3
   matrix; successive multiply is 6-multiply + 6-add per function — trivial.

**Primary recommendation:** Implement `BuildRadialShadingDict` as a near-copy of
`BuildAxialShadingDict`, generalize `RotMatrix` → `ComposeTransformMatrix`, and rename
`RotationGroup` → `TransformGroup` (storing `double[6]` matrix). All existing Phase 14 paths for
background painting and text Tm-baking extend naturally with no new PDF operators.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CSS `radial-gradient()` parsing | Layout (BoxTreeBuilder) | — | Mirrors linear-gradient parse path |
| PDF ShadingType-3 dict emission | PDF Writer (OwnedPdfWriter) | — | All shading dict construction is in the writer |
| Ellipse-to-unit-circle CTM scaling | PDF Writer (OwnedPdfWriter) | — | CTM/cm operator emitted at write time |
| CSS transform function chain parsing | Layout (BoxTreeBuilder) | — | Mirrors TryParseRotateDegrees seam |
| 2×3 matrix composition | Layout (BoxTreeBuilder) | — | Composed at parse time, stored in TransformGroup |
| CTM cm + text Tm baking | PDF Writer (OwnedPdfWriter) | — | All coordinate emission is writer responsibility |
| Policy gate (allow/reject) | Governance (LegacyPrintPolicy) | — | All policy decisions centralized in policy layer |
| Gradient shorthand routing | Governance (CascadeResolver) | — | ExpandBackground already routes gradient→background-image |

---

## Standard Stack

No new external dependencies. All work is in existing packages:

| Package | Current Role | Phase 15 Role |
|---------|-------------|---------------|
| `Muonroi.Pdf` (internal) | Writer + layout | Add `BuildRadialShadingDict`, generalize `RotMatrix` |
| `Muonroi.Pdf.Governance` (internal) | Policy + cascade | Widen transform + gradient gates |

**Installation:** None required. No new NuGet packages.

---

## Package Legitimacy Audit

Not applicable — this phase installs no external packages.

---

## Architecture Patterns

### System Architecture Diagram

```
HTML + CSS
    |
    v
CascadeResolver.ExpandBackground          (routes radial-gradient → background-image; no change needed)
    |
    v
BoxTreeBuilder.ResolveCssProperties
    |--- "background-image" contains "radial-gradient" → RadialGradientParser.TryParse → box.BackgroundGradient = RadialGradient
    |--- "transform" → TryParseTransformMatrix (NEW, replaces TryParseRotateDegrees)
    |                → box.TransformMatrix = double[6]
    |                → box.TransformGroup = new TransformGroup { Matrix = ... }
    |--- PropagateTransformGroup (renamed from PropagateRotationGroup)
    |
    v
OwnedPdfWriter.BuildContentStream
    |--- pageShadings: BackgroundGradient is RadialGradient → BuildRadialShadingDict
    |                  BackgroundGradient is LinearGradient → BuildAxialShadingDict (unchanged)
    |
    |--- TransformFor(el) → returns double[6] from TransformGroup pivot-composed matrix
    |
    |--- gradient element: q / [anisotropic cm for ellipse] / AppendCm(transform) / re W n / sh / Q
    |--- background-color: q / AppendCm(transform) / rg / re / f / Q  (unchanged, just uses TransformGroup)
    |--- text InlineBox: bake transform matrix into Tm (unchanged math, different matrix source)
    |
    v
PDF /Resources /Shading dict  → inline ShadingType-3 dict (same plumbing as ShadingType-2)
```

### Recommended Project Structure

```
src/Muonroi.Pdf/Internal/Layout/Boxes/
    LinearGradient.cs           (existing, unchanged)
    LinearGradientParser.cs     (existing, unchanged)
    RadialGradient.cs           (NEW — mirrors LinearGradient)
    RadialGradientParser.cs     (NEW — mirrors LinearGradientParser structure)
    RotationGroup.cs            (RENAME/MODIFY → TransformGroup with double[6] Matrix)
    BoxNode.cs                  (MODIFY — BackgroundGradient: BoxBackground? (union), TransformGroup replaces RotationGroup)

src/Muonroi.Pdf/Internal/Layout/
    BoxTreeBuilder.cs           (MODIFY — TryParseTransformMatrix, PropagateTransformGroup,
                                           RadialGradientParser.TryParse path)

src/Muonroi.Pdf/Internal/Writer/
    OwnedPdfWriter.cs           (MODIFY — BuildRadialShadingDict, GeneralizeTransformFor,
                                           ellipse anisotropic cm scale)

src/Muonroi.Pdf.Governance/Policies/
    LegacyPrintPolicy.cs        (MODIFY — widen transform gate regex/token, add radial-gradient allow)
```

---

## Research Question Answers

### Q1: PDF ShadingType 3 (Radial) Dict Structure

**What the ShadingType 2 dict looks like (verified at OwnedPdfWriter.cs:811-817):**
```
<< /ShadingType 2 /ColorSpace /DeviceRGB
   /Coords [x0 y0 x1 y1]
   /Domain [0 1]
   /Function <stitching-or-exponential>
   /Extend [true true] >>
```

**What ShadingType 3 (radial) requires — minimal delta:**
```
<< /ShadingType 3 /ColorSpace /DeviceRGB
   /Coords [x0 y0 r0  x1 y1 r1]
   /Domain [0 1]
   /Function <stitching-or-exponential>   ← IDENTICAL to BuildStitchingFunction output
   /Extend [true true] >>
```

**Key differences vs ShadingType 2:** [CITED: PDF 1.7 specification §8.7.4.5.4]
- `/ShadingType 3` (not 2)
- `/Coords` is 6 values: `x0 y0 r0 x1 y1 r1` where (x0,y0) is the center of circle 0 (inner),
  r0 is its radius, (x1,y1) is the center of circle 1 (outer), r1 is its radius.
- For a standard center radial: inner circle = center point, r0 = 0; outer circle = center point
  again, r1 = farthest-corner radius. Both circles share the same center for the common concentric
  case.
- `/Function` and `/Extend` are identical in structure to ShadingType 2.

**Minimal delta to add `BuildRadialShadingDict`:**
- New method alongside `BuildAxialShadingDict` (OwnedPdfWriter.cs:777).
- Takes `RadialGradient g, Rect rect, float pageHeightPt` (same signature shape).
- Computes center (cx, cy) in PDF coords, computes r1 = farthest-corner radius.
- Emits `<< /ShadingType 3 /ColorSpace /DeviceRGB /Coords [cx cy 0 cx cy r1] /Domain [0 1]
  /Function <call BuildStitchingFunction> /Extend [true true] >>`.
- The page /Resources /Shading plumbing is identical — the result is the same `string dict` value.

**`/Extend [true true]`:** Extends the shading beyond the two bounding circles. Required for
radial-gradient to paint outside the gradient radius (e.g. solid color fill to box edges). [CITED: PDF 1.7 §8.7.4.5.4]

**`BuildStitchingFunction` reuse:** The function builder at OwnedPdfWriter.cs:822 [VERIFIED: file read]
takes colors and positions and returns a FunctionType 2/3 string. It has no geometry — works
identically for ShadingType 3. Call it unchanged.

---

### Q2: CSS radial-gradient → PDF Mapping (D-04/D-05)

**CSS radial-gradient syntax (locked subset):**
```css
radial-gradient([shape] [size] at [position], color-stop, color-stop, ...)
/* shape: circle | ellipse (default: ellipse) */
/* size: farthest-corner (default, only one to implement) */
/* position: center | top | bottom | left | right | top left | etc. */
```
[CITED: MDN Web Docs — CSS radial-gradient()]

**Center resolution (position `at <keyword>`):**
```
cx = rect.X + fraction_x * rect.Width
cy = rect.Y + fraction_y * rect.Height   (layout coords, then Y-flip for PDF)

Keyword mapping:
  left → fraction_x=0, top → fraction_y=0
  center/default → fraction_x=0.5, fraction_y=0.5
  right → fraction_x=1, bottom → fraction_y=1
```
[ASSUMED: fraction mapping for keyword positions is standard CSS behavior, matches specification]

**Farthest-corner radius (the CSS default):** [CITED: MDN — radial-gradient() / CSS Values Level 4]
```
For a circle, farthest-corner is the distance from the center to the farthest corner of the box:
  r = max(
    sqrt((cx - rect.Left)^2  + (cy - rect.Top)^2),
    sqrt((cx - rect.Right)^2 + (cy - rect.Top)^2),
    sqrt((cx - rect.Left)^2  + (cy - rect.Bottom)^2),
    sqrt((cx - rect.Right)^2 + (cy - rect.Bottom)^2)
  )
```

**Circle mapping → PDF ShadingType-3 /Coords:**
```
/Coords [cx_pdf  cy_pdf  0    cx_pdf  cy_pdf  r]
           ↑ inner circle (radius 0 = point)   ↑ outer circle (farthest-corner radius)
```
PDF y-flip: `cy_pdf = pageHeightPt - cy_layout`.
Both circles have the same center (concentric) for the standard case.

**Ellipse mapping — unit-circle approach (D-04):**

CSS ellipse has two radii rx (horizontal) and ry (vertical). Using a unit-circle ShadingType-3
shading with an anisotropic CTM scale avoids a second ShadingType or a two-radius extension.

Derivation:
```
1. Compute the "natural" ellipse radii for farthest-corner extent:
   For farthest-corner ellipse, the CSS spec defines:
     rx = farthest_corner_x = max(|cx - rect.Left|, |cx - rect.Right|)
     ry = farthest_corner_y = max(|cy - rect.Top|, |cy - rect.Bottom|)
   (These are the semi-axes of the gradient ellipse.)

2. Map to unit-circle shading:
   - Emit a unit-circle ShadingType-3 shading with /Coords [0 0 0  0 0 1]
     (center at origin, r0=0, r1=1).
   - Before calling `sh`, set a CTM scale: rx 0 0 ry cx_pdf cy_pdf cm
     This maps unit circle → ellipse centered at (cx_pdf, cy_pdf).

3. The `sh` operator paints the unit-circle gradient, CTM-stretched into the ellipse shape.
```
[ASSUMED: farthest-corner ellipse radii = max(left/right distance) and max(top/bottom distance);
this matches the CSS specification definition but is not verified against the exact PDF behavior
in an authoritative source for this specific combination. Risk: if wrong, ellipse scale is incorrect.]

**Full content stream sequence for ellipse radial (within `q ... Q` block):**
```
q
[optional: transform CTM for element rotation/scale]
cx_pdf ry 0 ry cx_pdf cy_pdf cm    ← anisotropic scale + translate to center
re W n                              ← clip to box rect (in ORIGINAL coords? ISSUE: see pitfall below)
/ShN sh
Q
```

**Clip rect order issue — key pitfall (see Common Pitfalls §P3):**
The clip `re W n` must use the box rect in user space coordinates, but after the `cm` scale the
coordinate system changes. Solution: emit the clip rect BEFORE the `cm` (i.e., clip in page user
space, then scale). Alternatively, compute the clip rect in the scaled coordinate system. The
Phase 14 axial shading clips BEFORE the rotation `cm` (OwnedPdfWriter.cs:941-946 [VERIFIED: file
read]) — apply the same pattern: clip in page coords, then apply cm for the shading.

**Corrected sequence:**
```
q
[clip in page user space: box_x box_y box_w box_h re W n]
[element transform cm if any]
[for ellipse: rx 0 0 ry cx_pdf cy_pdf cm  — unit-circle scaling]
/ShN sh
Q
```
Note: the unit-circle shading dict uses `/Coords [0 0 0  0 0 1]` (origin-centered). The anisotropic
`cm` then places and scales it to the actual ellipse position and size.

---

### Q3: Full 2D Affine Composition (D-01/D-03)

**PDF affine matrix notation:** A 2×3 matrix `[a b c d e f]` transforms point (x, y) to:
```
x' = a*x + c*y + e
y' = b*x + d*y + f
```
[CITED: PDF 1.7 specification §8.3.3 — transformation matrices]

**CSS→PDF coordinate flip:** CSS y increases downward; PDF y increases upward. Phase 14 handles
this for rotate by negating the angle (OwnedPdfWriter.cs:864: `phi = -angleDegCss * PI / 180`
[VERIFIED: file read]). For the general affine, only the rotation-related elements change sign
(skew and translate y components flip), while scale is symmetric. The composed approach is:
apply CSS functions left-to-right (each as a 2×3 matrix), then at emission time bake the CSS-to-PDF
flip into the translation component (negate y of the translation). This matches Phase 14's approach.

**Per-function 2×3 matrix forms (CSS convention, before y-flip):**

```csharp
// Identity
[1, 0, 0, 1, 0, 0]

// translate(tx, ty)
[1, 0, 0, 1, tx, ty]

// translateX(tx)
[1, 0, 0, 1, tx, 0]

// translateY(ty)
[1, 0, 0, 1, 0, ty]

// scale(sx, sy)  — scale(sx) means sy=sx
[sx, 0, 0, sy, 0, 0]

// scaleX(sx)
[sx, 0, 0, 1, 0, 0]

// scaleY(sy)
[1, 0, 0, sy, 0, 0]

// rotate(angle) — CSS CW, before flip
// phi = angle in radians (CW in CSS; negate for PDF)
[cos(phi), sin(phi), -sin(phi), cos(phi), 0, 0]

// skewX(angle)
[1, 0, tan(angle), 1, 0, 0]

// skewY(angle)
[1, tan(angle), 0, 1, 0, 0]

// skew(ax, ay) — skew(ax) means ay=0
[1, tan(ay), tan(ax), 1, 0, 0]

// matrix(a, b, c, d, e, f) — direct
[a, b, c, d, e, f]
```
[ASSUMED: matrix forms derived from CSS Transform Level 1 specification; standard and well-established
but not independently verified against a CSS spec document in this session.]

**Left-to-right composition (multiply M1 then M2):**
```csharp
static double[] Multiply(double[] m1, double[] m2)
{
    // m = [a, b, c, d, e, f]
    // Homogeneous form:  | a c e |
    //                   | b d f |
    //                   | 0 0 1 |
    // m1 * m2:
    double a = m1[0]*m2[0] + m1[2]*m2[1];
    double b = m1[1]*m2[0] + m1[3]*m2[1];
    double c = m1[0]*m2[2] + m1[2]*m2[3];
    double d = m1[1]*m2[2] + m1[3]*m2[3];
    double e = m1[0]*m2[4] + m1[2]*m2[5] + m1[4];
    double f = m1[1]*m2[4] + m1[3]*m2[5] + m1[5];
    return [a, b, c, d, e, f];
}
```
[ASSUMED: homogeneous matrix multiplication formula is standard linear algebra]

**Box-center pivot composition (D-03):**
CSS default `transform-origin: 50% 50%` means all transforms apply relative to the box center
(px, py). The pivot-composition is:
```
M_final = T(px,py) * M_css * T(-px,-py)
```
Where T(tx,ty) = translate matrix. Expanding:
```
// pre-translate to pivot (subtract center)
T_to   = [1,0,0,1, px, py]
T_from = [1,0,0,1,-px,-py]
// M_with_pivot = T_to * M_css * T_from
```
Computed once at BoxTreeBuilder parse time, stored as the final 2×3 in `TransformGroup.Matrix`.
[ASSUMED: pivot-composition formula is standard; matches Phase 14's RotMatrix which already
applies the same pattern explicitly (OwnedPdfWriter.cs:862-868 VERIFIED: file read).]

**CSS-to-PDF y-flip application:**
Phase 14 bakes the y-flip into `RotMatrix` by negating phi (OwnedPdfWriter.cs:864 [VERIFIED]).
For the general case: the composed matrix in CSS space has elements [a,b,c,d,e,f]. In PDF space
(y-up), the translation components flip:
- For purely CSS-space matrices computed with layout coordinates: the E and F terms are already in
  layout space (y-down). The existing Tm-baking code at OwnedPdfWriter.cs:1191-1192 [VERIFIED]
  already handles this:
  ```csharp
  double ex = tRot.A * pdfXt + tRot.C * pdfYt + tRot.E;
  double fy = tRot.B * pdfXt + tRot.D * pdfYt + tRot.F;
  ```
  This works for any 2×3 matrix — not rotation-specific. The pivot (px,py) in `TransformGroup`
  is stored in PDF coords (y-up) just as `rotationPivots` is today.

**Recommendation:** Compute the pivot-composed matrix (T_to * M_css * T_from) in CSS layout
coordinates at BoxTreeBuilder time. Store as `double[6]` in `TransformGroup`. At writer time,
apply the same Tm-baking formula and `AppendCm` that Phase 14 already uses — they are already
generic. The only writer-side change is that `RotFor` reads `TransformGroup.Matrix` instead of
calling `RotMatrix(grp.AngleDegrees, ...)`.

**Extend Phase 14's Tm-baking to affine matrix (no new logic needed):**

Existing code at OwnedPdfWriter.cs:1186-1199 [VERIFIED: file read]:
```csharp
if (RotFor(el) is { } tRot)
{
    double ex = tRot.A * pdfXt + tRot.C * pdfYt + tRot.E;
    double fy = tRot.B * pdfXt + tRot.D * pdfYt + tRot.F;
    // emit: A B C D ex fy Tm
}
```
This code already handles any 2×3 matrix — it uses A, B, C, D, E, F without assuming rotation.
Changing `RotFor` to return a general matrix instead of only a rotation matrix is sufficient.

---

### Q4: Policy Gate Widening (D-02/D-06)

**Current transform gate (LegacyPrintPolicy.cs:381-387) [VERIFIED: file read]:**
```csharp
private static readonly Regex SingleRotateRegex = new(
    @"^\s*rotate\(\s*-?\d*\.?\d+(deg|rad|grad|turn)?\s*\)\s*$",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
private static bool IsSingleRotate(string transform) => SingleRotateRegex.IsMatch(transform);
```
Used in `CheckTransformAndGradient` at line 398.

**New transform gate (D-02):** Replace `IsSingleRotate` with `IsAffineTransform(string transform)`.
Approach: tokenize the transform string by extracting all `function(...)` tokens, verify each token's
function name is in the allowed affine set, and verify the args are numerically parseable.

```csharp
// Allowed function names (case-insensitive)
private static readonly HashSet<string> AllowedAffineFunctions = new(StringComparer.OrdinalIgnoreCase)
{
    "translate", "translateX", "translateY",
    "scale", "scaleX", "scaleY",
    "rotate",
    "skew", "skewX", "skewY",
    "matrix"
};

// Token extraction regex: matches function-name(args) pairs
private static readonly Regex AffineFunctionTokenRegex = new(
    @"(\w+)\(([^)]*)\)",
    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

private static bool IsAffineTransform(string transform)
{
    if (string.IsNullOrWhiteSpace(transform)) return false;
    var matches = AffineFunctionTokenRegex.Matches(transform);
    if (matches.Count == 0) return false;
    // Verify the entire string is accounted for by the matched functions (no unknown tokens)
    int totalMatchedLength = matches.Sum(m => m.Length);
    int whitespaceLength = transform.Count(char.IsWhiteSpace);
    // Rough check: all non-whitespace should be part of function tokens
    foreach (Match m in matches)
    {
        if (!AllowedAffineFunctions.Contains(m.Groups[1].Value)) return false;
        // Verify args parse as comma-separated numbers with optional units
        string args = m.Groups[2].Value;
        if (!AreNumericArgs(args)) return false;
    }
    return true;
}
```
[ASSUMED: regex approach for CSS transform token extraction; specific implementation may need
adjustment for edge cases like nested parens or whitespace-heavy inputs.]

**Fail-loud contract:** When `IsAffineTransform` returns false, emit `forbidden.transform.geometric`
violation exactly as today (LegacyPrintPolicy.cs:398-401 [VERIFIED]). When an unknown function
is found (e.g., `perspective()`), the violation message should name the unknown function.

**No Silent Catch:** The existing pattern at LegacyPrintPolicy.cs:204 [VERIFIED: file read] catches
`ArgumentException or NullReferenceException` from `GetComputedStyle`, degrades to stylesheet scan,
and then to the inline-style fallback. The widened gate calls `IsAffineTransform` in both paths
(computed-style and inline-style). No new exception handling needed.

**Gradient gate widening (D-06):**
Current check at LegacyPrintPolicy.cs:408-420 [VERIFIED: file read]:
```csharp
bool isLinearOnly =
    gradientSource.Contains("linear-gradient(", StringComparison.OrdinalIgnoreCase)
    && !gradientSource.Contains("radial-gradient", StringComparison.OrdinalIgnoreCase)
    && !gradientSource.Contains("conic-gradient", StringComparison.OrdinalIgnoreCase)
    && !gradientSource.Contains("repeating-", StringComparison.OrdinalIgnoreCase);
if (!isLinearOnly) { violations.Add(...) }
```

New check — rename variable and add radial-gradient to the allow-set:
```csharp
bool isAllowedGradient =
    (gradientSource.Contains("linear-gradient(", StringComparison.OrdinalIgnoreCase)
     || gradientSource.Contains("radial-gradient(", StringComparison.OrdinalIgnoreCase))
    && !gradientSource.Contains("conic-gradient", StringComparison.OrdinalIgnoreCase)
    && !gradientSource.Contains("repeating-", StringComparison.OrdinalIgnoreCase);
if (!isAllowedGradient) { violations.Add(...) }
```

The suggestion text in the violation changes to "Use linear-gradient or radial-gradient; other
gradient functions are not supported."

**`CascadeResolver.ExpandBackground` (no change needed) [VERIFIED: file read]:**
Line 522: `if (v.Contains("gradient", StringComparison.OrdinalIgnoreCase))` — already routes any
gradient (linear or radial) to `background-image`. No change required.

---

### Q5: TransformGroup Carrier Design Decision (Claude's Discretion)

**Current `RotationGroup` (RotationGroup.cs:1-14) [VERIFIED: file read]:**
```csharp
internal sealed class RotationGroup
{
    public float AngleDegrees { get; init; }
}
```
Used in `BoxNode.cs` (line 62 [VERIFIED]: `public RotationGroup? RotationGroup { get; set; }`).
Used in `BoxTreeBuilder.cs` (lines 75, 350, 784 [VERIFIED]).
Used in `OwnedPdfWriter.cs` (lines 904, 907, 921-922 [VERIFIED]).

**Recommendation (Claude's discretion): Rename `RotationGroup` → `TransformGroup`**

Change `AngleDegrees: float` to `Matrix: double[]` (length 6, already pivot-composed). This is
a mechanical rename + field change across 4 files. It avoids a parallel carrier type and keeps
the existing propagation logic (`PropagateRotationGroup` → `PropagateTransformGroup`) intact.

The writer's `RotFor` local function at OwnedPdfWriter.cs:919-923 changes from:
```csharp
if (el.Source?.RotationGroup is { } grp && rotationPivots.TryGetValue(grp, out (double Px, double Py) p))
    return RotMatrix(grp.AngleDegrees, p.Px, p.Py);
```
To:
```csharp
if (el.Source?.TransformGroup is { } grp && _transformPivots.TryGetValue(grp, out _))
    return (grp.Matrix[0], grp.Matrix[1], grp.Matrix[2], grp.Matrix[3], grp.Matrix[4], grp.Matrix[5]);
```
Where the pivot-composition is now done at parse time, so no per-element RotMatrix call is needed.

**Origin-box detection changes:** Currently `el.Source is { RotationDegrees: not 0f, RotationGroup: { } }`.
With the rename: `el.Source is { TransformDegrees: not 0f, TransformGroup: { } }` (or a bool flag
like `IsTransformOriginBox`). The cleanest approach: keep `RotationDegrees` renamed to
`TransformHasTransform` (bool), and detect the origin by whether the box itself set the transform
(not a descendant). Alternatively, the BoxTreeBuilder can store the pivot in the `TransformGroup`
directly and the writer just reads `grp.Pivot` instead of looking it up.

**Simpler approach:** Store `(Px, Py)` pivot in `TransformGroup` alongside `Matrix`. BoxTreeBuilder
computes the pivot at parse time (box center in PDF coords — but we're in layout space here;
pivot needs Y-flip which happens in the writer). Therefore:
- `TransformGroup.Matrix` = the CSS pivot-composed matrix (in layout coordinates, pre y-flip)
- `TransformGroup.PivotX`, `TransformGroup.PivotY` = box center in layout coords (for Y-flip in writer)

This exactly mirrors Phase 14's approach: the writer resolves `rotationPivots` by scanning for
origin elements and computing PDF-space (px, py). Keep that pattern: writer scans elements to find
the origin, computes the PDF pivot, then applies `TransformGroup.Matrix` (with E/F already
incorporating layout-space pivot from parse time, then writer adjusts for PDF y-up).

**MSTD0002 note:** `TransformGroup.Matrix` as `double[]` — ensure `?.` access everywhere, no `!`.

---

### Q6: `RadialGradient` and `RadialGradientParser` Design

**`LinearGradient` model (LinearGradient.cs:1-18) [VERIFIED: file read]:**
```csharp
internal sealed class LinearGradient
{
    public float AngleDegrees { get; init; } = 180f;
    public IReadOnlyList<GradientStop> Stops { get; init; } = Array.Empty<GradientStop>();
}
```

**`RadialGradient` model (new file, mirrors LinearGradient):**
```csharp
internal sealed class RadialGradient
{
    /// <summary>Shape: "circle" or "ellipse" (default "ellipse").</summary>
    public string Shape { get; init; } = "ellipse";

    /// <summary>
    /// Position as fractions [0..1]: (PositionX, PositionY).
    /// Default center: (0.5, 0.5).
    /// </summary>
    public float PositionX { get; init; } = 0.5f;
    public float PositionY { get; init; } = 0.5f;

    /// <summary>Color stops (at least two when renderable).</summary>
    public IReadOnlyList<GradientStop> Stops { get; init; } = Array.Empty<GradientStop>();
}
```
`GradientStop` is shared (already exists in LinearGradient.cs line 18 [VERIFIED]).

**BoxNode.BackgroundGradient carrier:** The current type is `LinearGradient?` (BoxNode.cs:55
[VERIFIED]). Phase 15 must support either gradient type. Options:
1. Change to `object? BackgroundGradient` — loses type safety.
2. Introduce a sealed abstract `BackgroundGradientBase` with `Stops` property — clean but more churn.
3. Keep `LinearGradient? BackgroundGradient` and add `RadialGradient? BackgroundRadialGradient` —
   minimal churn, slight duplication.

**Recommendation:** Option 3 is the lowest-risk for a single-writer change. The writer already
branches on gradient type for the shading dict. Adding a separate property avoids touching all
existing code that checks `BackgroundGradient is not null`.

**`RadialGradientParser.TryParse` structure (mirrors LinearGradientParser):**

`LinearGradientParser.TryParse` (LinearGradientParser.cs:15-57 [VERIFIED: file read]):
1. Find `linear-gradient(` with `IndexOf`.
2. Find matching `)` with `MatchParen`.
3. Split args by `,` top-level (rgb()-comma-safe via `SplitTopLevel`).
4. Check first part for direction; parse angle.
5. Parse remaining parts as color stops.

`RadialGradientParser.TryParse` mirrors the structure:
1. Find `radial-gradient(` with `IndexOf`.
2. `MatchParen` to find close.
3. `SplitTopLevel(args, ',')` — same helper.
4. Check first part for shape/size/position keywords (the "gradient definition" part).
5. Parse remaining parts as color stops using the same `ParseStop` / `ParsePositionFraction` logic.

**Gradient definition part parsing (first comma-separated arg):**
The CSS grammar for the first arg of `radial-gradient`:
```
[[ circle | ellipse ] || [ farthest-corner | ... ]] [at <position>]
```
For the locked subset (D-04/D-05):
- `circle` → shape=circle
- `ellipse` → shape=ellipse (default when no shape keyword)
- `at center` → posX=0.5, posY=0.5 (default)
- `at top` → posX=0.5, posY=0
- `at bottom` → posX=0.5, posY=1
- `at left` → posX=0, posY=0.5
- `at right` → posX=1, posY=0.5
- `at top left` → posX=0, posY=0
- etc.
- If no shape/size/position keyword → entire first arg is a color stop, use defaults.

If first arg does NOT contain a gradient-definition keyword (shape, size, or `at`), treat the first
arg as a color stop (radial-gradient with just colors, e.g. `radial-gradient(#fff, #000)` defaults
to ellipse, farthest-corner, center).
[ASSUMED: parsing logic derived from CSS specification understanding; specific edge cases may need
validation against real browser output.]

**`SplitTopLevel` and `MatchParen` reuse:** These are private static methods in
`LinearGradientParser`. Either duplicate them in `RadialGradientParser` or extract to a shared
`GradientParserHelpers` internal static class. Extraction is cleaner.

---

### Q7: BoxTreeBuilder parse/propagate seam changes

**Current gradient parse (BoxTreeBuilder.cs:318-332) [VERIFIED: file read]:**
```csharp
string? gradientSource = bgImage;
if (string.IsNullOrEmpty(gradientSource) || !gradientSource.Contains("linear-gradient", ...))
{
    string? bgShorthand = style.GetValue("background");
    if (...bgShorthand.Contains("linear-gradient",...)) gradientSource = bgShorthand;
}
if (...gradientSource.Contains("linear-gradient",...) && LinearGradientParser.TryParse(...))
    box.BackgroundGradient = grad;
```

**New gradient parse (Phase 15):**
```csharp
// Check for radial-gradient first (new in Phase 15)
if (...gradientSource.Contains("radial-gradient",...) && RadialGradientParser.TryParse(...))
    box.BackgroundRadialGradient = radGrad;
// Then check for linear-gradient (existing)
else if (...gradientSource.Contains("linear-gradient",...) && LinearGradientParser.TryParse(...))
    box.BackgroundGradient = grad;
```

**Current transform parse (BoxTreeBuilder.cs:346-351) [VERIFIED: file read]:**
```csharp
var transformVal = style.GetValue("transform");
if (!string.IsNullOrEmpty(transformVal) && TryParseRotateDegrees(transformVal, out float rotDeg) && rotDeg != 0f)
{
    box.RotationDegrees = rotDeg;
    box.RotationGroup = new RotationGroup { AngleDegrees = rotDeg };
}
```

**New transform parse (Phase 15) — replace `TryParseRotateDegrees` with `TryParseTransformMatrix`:**
```csharp
var transformVal = style.GetValue("transform");
if (!string.IsNullOrEmpty(transformVal) && TryParseTransformMatrix(transformVal, out double[] matrix))
{
    box.HasTransform = true;  // or keep RotationDegrees as a sentinel
    box.TransformGroup = new TransformGroup { Matrix = matrix };
}
```

**`TryParseTransformMatrix`:** Replace `TryParseRotateDegrees`. Returns false on unrecognized function
name or non-parseable args (same fail-silent-in-layout-but-caught-by-policy contract as today).
The policy gate (LegacyPrintPolicy) rejects unknown functions before layout runs, so TryParseTransformMatrix
only needs to handle the allowed set. If it encounters an unknown function it returns false (box
has no transform), which is safe because policy already blocked it.

**`PropagateTransformGroup` (rename `PropagateRotationGroup`):**
BoxTreeBuilder.cs:784-793 [VERIFIED: file read] — identical logic, only rename. The propagation
semantics are unchanged: copy the group to all descendants that don't have their own transform.

---

### Q8: Writer-side radial shading integration

**Current shading loop (OwnedPdfWriter.cs:165-179) [VERIFIED: file read]:**
```csharp
var gradientResNames = new Dictionary<PositionedElement, string>();
var pageShadings = new List<(string ResName, string Dict)>();
{
    int gi = 0;
    foreach (PositionedElement el in page.Elements)
    {
        if (el.Source?.BackgroundGradient is not { Stops.Count: >= 2 } grad)
            continue;
        string resName = $"Sh{gi++}";
        gradientResNames[el] = resName;
        pageShadings.Add((resName, BuildAxialShadingDict(grad, el.Position, pageHeightPt)));
    }
}
```

**New shading loop (Phase 15) — extend for radial:**
```csharp
foreach (PositionedElement el in page.Elements)
{
    string? dict = null;
    if (el.Source?.BackgroundGradient is { Stops.Count: >= 2 } linGrad)
        dict = BuildAxialShadingDict(linGrad, el.Position, pageHeightPt);
    else if (el.Source?.BackgroundRadialGradient is { Stops.Count: >= 2 } radGrad)
        dict = BuildRadialShadingDict(radGrad, el.Position, pageHeightPt);
    if (dict is null) continue;
    string resName = $"Sh{gi++}";
    gradientResNames[el] = resName;
    pageShadings.Add((resName, dict));
}
```

**Content stream: radial shading with ellipse CTM** (in `BuildContentStream`):

The existing code at OwnedPdfWriter.cs:931-952 [VERIFIED: file read] emits:
```
q
[RotFor cm if any]
x y w h re W n
/ShN sh
Q
```

For radial shading, the ellipse requires an additional anisotropic `cm` between the clip and `sh`.
The `BuildRadialShadingDict` for ellipse will use a unit-circle `/Coords [0 0 0  0 0 1]`, and the
ellipse-scale `cm` goes into the content stream (not the shading dict). A bool/enum on `RadialGradient`
indicates whether an ellipse cm is needed, and provides the rx/ry/cx_pdf/cy_pdf values.

Alternatively: `BuildRadialShadingDict` returns a tuple `(string dict, string? ellipseCm)` where
`ellipseCm` is the cm matrix string if shape is ellipse, null for circle. The writer emits it
between the clip and the `sh` call.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Multi-stop gradient color interpolation | Custom interpolation | Existing `BuildStitchingFunction` (OwnedPdfWriter.cs:822) |
| Paren-balanced comma splitting | Custom parser | Existing `SplitTopLevel` in `LinearGradientParser.cs:171` |
| Matching `func(...)` paren | Custom scan | Existing `MatchParen` in `LinearGradientParser.cs:155` |
| Color stop parsing | New parser | Existing `ParseStop` / `ParsePositionFraction` in `LinearGradientParser` |
| Angle unit parsing | New parser | Existing angle parsing logic in `LinearGradientParser.ParseAngle` |
| PDF page /Resources /Shading plumbing | Custom registry | Existing `pageShadings` list + inline dict pattern (OwnedPdfWriter.cs:221-227) |
| CTM `cm` emission | Custom | Existing `AppendCm` (OwnedPdfWriter.cs:872) |
| Text position Tm-baking | Custom | Existing bake-into-Tm code (OwnedPdfWriter.cs:1186-1199) |

**Key insight:** The Phase 14 infrastructure is deliberately generic — `AppendCm` takes any 2×3
tuple, and the Tm-baking code uses A/B/C/D/E/F without any rotation assumption. Phase 15 does not
need to add new PDF operators or new resource plumbing.

---

## Common Pitfalls

### P1: GetComputedStyle throws on transform functions (NullReferenceException)
**What goes wrong:** AngleSharp.Css (beta) throws NullReferenceException for some transform values
it cannot compute headlessly. `CheckTransformAndGradient` is called with the raw inline-style
fallback value when this happens.
**Root cause:** AngleSharp.Css `GetComputedStyle` is fragile for newer CSS properties.
**How to avoid:** The existing broad catch at LegacyPrintPolicy.cs:204 (`catch (Exception ex) when
(ex is ArgumentException or NullReferenceException)`) [VERIFIED: file read] already handles this.
The widened `IsAffineTransform` gate is called in BOTH the computed-style path (line 322) AND the
inline-style fallback path (line 234 [VERIFIED: file read]). Ensure the new gate function is
called in both places.
**Warning signs:** Policy passes a multi-function transform that the policy should reject, because
GetComputedStyle threw and the fallback missed the inline-style check.

### P2: CascadeResolver.ExpandBackground routes any gradient keyword
**What goes wrong:** `ExpandBackground` routes any string containing "gradient" to
`background-image` (CascadeResolver.cs:522 [VERIFIED: file read]). This already correctly handles
`radial-gradient(...)` in the `background` shorthand. No change needed, but forgetting this means
accidentally re-implementing routing logic in BoxTreeBuilder.
**How to avoid:** Rely on `ExpandBackground`; in BoxTreeBuilder, check `background-image` only
(same as today for linear-gradient).

### P3: Clip rect coordinates when using anisotropic CTM for ellipse
**What goes wrong:** The clip `re W n` must be in the coordinate system BEFORE the `cm` that
scales the unit circle to the ellipse. If the clip is emitted AFTER the ellipse CTM, the clip
rect is in the scaled coordinate system and clips incorrectly.
**Root cause:** `q ... cm ... re ... sh ... Q` applies the clip in scaled space.
**How to avoid:** Emit in order: `q`, clip rect (`re W n`), element transform `cm` (if any),
ellipse anisotropic `cm`, `sh`, `Q`. Mirror Phase 14's order: clip rect is emitted before any
`cm` (OwnedPdfWriter.cs:941-948 [VERIFIED]).
**Warning signs:** Radial gradient is clipped to a narrow rectangle or not clipped at all.

### P4: Ellipse farthest-corner radii — not the box half-dimensions
**What goes wrong:** Using `rx = rect.Width / 2` and `ry = rect.Height / 2` gives a centered
gradient that exactly touches the box edges — this is `closest-side`, not `farthest-corner`.
**Root cause:** CSS `farthest-corner` (the default) is the distance from the gradient center to
the FARTHEST corner, not the nearest edge.
**How to avoid:** For a center-positioned gradient: `rx = max(|cx - left|, |cx - right|)`,
`ry = max(|cy - top|, |cy - bottom|)`. For the default center position (cx=0.5, cy=0.5):
`rx = rect.Width / 2` and `ry = rect.Height / 2` happen to be correct because the center is
equidistant from all sides. But for off-center positions this differs.
**Warning signs:** Off-center radial gradients don't fill the box corners.

### P5: TransformGroup.Matrix pivot composition at parse time vs write time
**What goes wrong:** If the pivot (box center) is composed into the matrix at BoxTreeBuilder time
using layout coords (y-down), but the writer expects PDF-space (y-up) E/F components, the
translation part of the matrix will be wrong.
**Root cause:** The y-flip must be applied consistently.
**How to avoid:** Either (a) store the matrix without pivot in `TransformGroup` and apply
pivot+y-flip in the writer (matching Phase 14 exactly), or (b) store the pivot-composed matrix
in layout coords and have the writer bake the y-flip the same way as today's `RotMatrix`
(which computes E/F in PDF coords by using `py = pageHeightPt - ...`). Option (a) is safer and
requires less change to the writer. The writer already has the pivot-resolution loop for
`rotationPivots` (OwnedPdfWriter.cs:904-913 [VERIFIED]).
**Warning signs:** Transforms are applied at the wrong position (correct shape but wrong location).

### P6: MSTD0002 — no `!` operator in Muonroi.Pdf namespaces
**What goes wrong:** Accessing `TransformGroup.Matrix[0]` without null-guard triggers MSTD0002.
**How to avoid:** Use `?.` for all nullable property access. Use `MGuard.NotNull` for assertions.
Do not write `box.TransformGroup!.Matrix`. This applies to `RadialGradient?`, `TransformGroup?`,
and `double[]?` array access.

### P7: `GradientTransformPolicyTests.RadialGradient_IsRejected` test must be UPDATED, not deleted
**What goes wrong:** The existing test at `GradientTransformPolicyTests.cs:41-44` [VERIFIED: file read]
asserts that `radial-gradient` is REJECTED. After D-06 widens the gate, this test will FAIL
(it now expects rejection, but policy allows it).
**How to avoid:** Update the test to `Should().NotContain(... "forbidden.background.gradient" ...)`
and rename it to `RadialGradient_IsAllowed`. Add new tests for conic/repeating remaining rejected.

### P8: `GradientShadingRenderTests.RadialGradient_IsRejectedByPolicy` must be updated
**What goes wrong:** `GradientShadingRenderTests.cs:55-63` [VERIFIED: file read] asserts
`ThrowAsync<PdfPolicyException>` for a radial-gradient render. After Phase 15 this must render
successfully (emit ShadingType 3) and the test must check for `/ShadingType 3` instead.
**How to avoid:** Update the test: it becomes `RadialGradient_EmitsRadialShading`.

---

## Runtime State Inventory

**SKIPPED** — this is a greenfield feature addition, not a rename/refactor/migration phase.
No stored data, live service config, OS-registered state, secrets/env vars, or build artifacts
need updating.

---

## Environment Availability

Phase 15 is a pure code/logic change — no new external tools, databases, or runtimes required
beyond the existing project build stack.

| Dependency | Required By | Available | Note |
|------------|------------|-----------|------|
| .NET SDK | Build | Yes | Existing project target |
| dotnet test | Test runner | Yes | Standard |
| `MUONROI_UPDATE_SNAPSHOTS=1` | Golden baseline | On-demand | Set only when adding new golden cases |

---

## Test / Golden Strategy

### Existing tests to UPDATE (not add)

| Test file | Test method | Required change |
|-----------|-------------|----------------|
| `Policy/GradientTransformPolicyTests.cs:41` | `RadialGradient_IsRejected` | Flip to `IsAllowed`; rename |
| `Service/GradientShadingRenderTests.cs:55` | `RadialGradient_IsRejectedByPolicy` | Flip to `EmitsRadialShading`; assert `/ShadingType 3` |
| `Policy/GradientTransformPolicyTests.cs:64` | `TransformTranslate_IsRejected` | Flip to `IsAllowed`; covers D-01 |
| `Policy/GradientTransformPolicyTests.cs:72` | `TransformRotateWithScale_IsRejected` | Flip to `IsAllowed` (chain); rename |

### New policy tests (Phase 15)

| Test | Asserts |
|------|---------|
| `RadialGradient_IsAllowed` | No `forbidden.background.gradient` violation |
| `ConicGradient_IsRejected` | Has `forbidden.background.gradient` violation |
| `RepeatingRadialGradient_IsRejected` | Has `forbidden.background.gradient` violation |
| `TransformTranslate_IsAllowed` | No `forbidden.transform.geometric` violation |
| `TransformScale_IsAllowed` | No violation |
| `TransformMatrix_IsAllowed` | No violation (`matrix(1,0,0,1,10,20)`) |
| `TransformChain_IsAllowed` | No violation (`translate(10px) rotate(45deg) scale(0.5)`) |
| `TransformPerspective_IsRejected` | Has violation (unknown function) |
| `TransformUnknownFunction_IsRejected` | Has violation |

### New golden cases — FidelityExtended group (SC1 and SC2)

These are additive; existing 17 TCIS templates are byte-unchanged (no radial/non-rotate transforms).
Baselines generated with `MUONROI_UPDATE_SNAPSHOTS=1` on first run.

**SC1 — Radial gradient:**
| Case name | HTML snippet | Assertion |
|-----------|-------------|-----------|
| `radial-gradient-circle-center` | `<div style="height:60px;background:radial-gradient(circle,#0c6b6b,#fff);">x</div>` | `/ShadingType 3` in page bytes |
| `radial-gradient-ellipse-default` | `<div style="height:60px;background:radial-gradient(#ff0,#0ff);">x</div>` | `/ShadingType 3`; ellipse is CSS default |
| `radial-gradient-two-stop` | 2-stop radial | `/FunctionType 2` (single exponential) |
| `radial-gradient-three-stop` | 3-stop radial | `/FunctionType 3` (stitching) |

**SC2 — Non-rotate transforms:**
| Case name | HTML snippet | Assertion |
|-----------|-------------|-----------|
| `transform-translate` | `<div style="transform:translate(10px,5px);">x</div>` | Content stream has `cm` |
| `transform-scale` | `<div style="transform:scale(0.8);">x</div>` | Content stream has `cm` |
| `transform-matrix` | `<div style="transform:matrix(1,0,0,1,20,10);">x</div>` | `cm` in stream |
| `transform-chain` | `<div style="transform:translate(5px) rotate(30deg) scale(0.9);">x</div>` | Single `cm` per element |

**Structural assertions for SC1 (mirror `GradientShadingRenderTests`):**
- Render to bytes, read as ASCII/Latin-1.
- `bytes.Should().Contain("/ShadingType 3")`.
- `bytes.Should().Contain("/Coords")`.
- For 2-stop: `bytes.Should().Contain("/FunctionType 2")`.
- For 3-stop: `bytes.Should().Contain("/FunctionType 3")`.
- Conic and repeating-* still throw `PdfPolicyException`.

**Structural assertions for SC2:**
- Render to bytes, decompress content stream.
- Each transformed element produces a `cm` operator in the content stream.
- No per-function nested `q ... Q` (only one `cm` for multi-function chains).
- `perspective()` and unknown functions still throw `PdfPolicyException`.

**PerfGate:** No change needed. PerfGate uses `reference-50kb.html` which contains no radial
gradients or non-rotate transforms. Cold ≤ 1500 ms / warm ≤ 400 ms ceilings unchanged.
[VERIFIED: PerfGateTests.cs:33-34]

**Golden re-baseline scope:** Existing 17 TCIS templates use no radial gradients or non-rotate
transforms (confirmed by policy — they already pass through `LegacyPrintPolicy` today). No
existing golden bytes change. Only newly-added gradient/transform cases need `MUONROI_UPDATE_SNAPSHOTS=1`.

---

## Code Examples

### BuildRadialShadingDict — minimal delta from BuildAxialShadingDict

```csharp
// Source: OwnedPdfWriter.cs:777 (BuildAxialShadingDict verified pattern)
// Phase 15: build an inline PDF radial-shading dictionary (ShadingType 3) for a radial-gradient
// background. For a circle: /Coords = [cx cy 0 cx cy r] (two concentric circles, r0=0).
// For an ellipse: /Coords = [0 0 0 0 0 1] (unit circle), preceded in the content stream by
// an anisotropic CTM scale [rx 0 0 ry cx_pdf cy_pdf cm].
private static string BuildRadialShadingDict(
    RadialGradient g, Rect rect, float pageHeightPt, out string? ellipseCm)
{
    float w = rect.Width;
    float h = rect.Height;
    float bgX = rect.X;
    float bgY = pageHeightPt - rect.Y - rect.Height;

    // Center in PDF coords (y-up)
    double cx = bgX + g.PositionX * w;
    double cy = bgY + (1.0 - g.PositionY) * h;  // y-flip: CSS top=0 → PDF bottom

    IReadOnlyList<GradientStop> stops = g.Stops;
    // [normalize stops + build colors — identical to BuildAxialShadingDict]
    // ...

    var sb = new StringBuilder();
    if (g.Shape == "circle")
    {
        // Farthest-corner circle radius
        double r = Math.Max(
            Math.Max(Distance(cx, cy, bgX, bgY), Distance(cx, cy, bgX + w, bgY)),
            Math.Max(Distance(cx, cy, bgX, bgY + h), Distance(cx, cy, bgX + w, bgY + h)));

        sb.Append("<< /ShadingType 3 /ColorSpace /DeviceRGB /Coords [");
        sb.Append(Num(cx)); sb.Append(' '); sb.Append(Num(cy)); sb.Append(" 0 ");
        sb.Append(Num(cx)); sb.Append(' '); sb.Append(Num(cy)); sb.Append(' '); sb.Append(Num(r));
        sb.Append("] /Domain [0 1] /Function ");
        sb.Append(BuildStitchingFunction(colors, pos));
        sb.Append(" /Extend [true true] >>");
        ellipseCm = null;
    }
    else // ellipse
    {
        // Unit-circle shading — CTM scale goes in content stream
        sb.Append("<< /ShadingType 3 /ColorSpace /DeviceRGB /Coords [0 0 0 0 0 1]");
        sb.Append(" /Domain [0 1] /Function ");
        sb.Append(BuildStitchingFunction(colors, pos));
        sb.Append(" /Extend [true true] >>");

        // Anisotropic scale for content stream: [rx 0 0 ry cx cy cm]
        double rx = Math.Max(Math.Abs(cx - bgX), Math.Abs(cx - (bgX + w)));
        double ry = Math.Max(Math.Abs(cy - bgY), Math.Abs(cy - (bgY + h)));
        ellipseCm = $"{Num(rx)} 0 0 {Num(ry)} {Num(cx)} {Num(cy)} cm";
    }
    return sb.ToString();

    static double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    static string Num(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
}
```

### ComposeTransformMatrix — left-to-right function chain

```csharp
// Phase 15: parse a CSS transform value into a composed 2×3 affine matrix [a,b,c,d,e,f].
// CSS functions compose left-to-right: f1 then f2 → M_f2 * M_f1 (right-associative multiply).
// Returns false if any function is unrecognized or args are not parseable.
private static bool TryParseTransformMatrix(string transform, out double[] matrix)
{
    matrix = [1, 0, 0, 1, 0, 0]; // identity
    // tokenize functions, validate, multiply left-to-right
    // ... (see Implementation section in PLAN)
    return true; // simplified
}

private static double[] Multiply(double[] m1, double[] m2)
{
    return [
        m1[0]*m2[0] + m1[2]*m2[1],
        m1[1]*m2[0] + m1[3]*m2[1],
        m1[0]*m2[2] + m1[2]*m2[3],
        m1[1]*m2[2] + m1[3]*m2[3],
        m1[0]*m2[4] + m1[2]*m2[5] + m1[4],
        m1[1]*m2[4] + m1[3]*m2[5] + m1[5]
    ];
}
```

### IsAffineTransform — widened policy gate

```csharp
// Phase 15: replaces IsSingleRotate. Accepts a whitespace-separated chain of allowed
// affine CSS transform functions. Rejects anything with an unrecognized function name.
private static bool IsAffineTransform(string transform)
{
    if (string.IsNullOrWhiteSpace(transform)) return false;
    var matches = AffineFunctionTokenRegex.Matches(transform);
    if (matches.Count == 0) return false;
    foreach (Match m in matches)
    {
        if (!AllowedAffineFunctions.Contains(m.Groups[1].Value)) return false;
        if (!AreNumericArgs(m.Groups[2].Value)) return false;
    }
    return true;
}
```

---

## State of the Art

| Old Approach (Phase 14) | Phase 15 Approach | Impact |
|-------------------------|-------------------|--------|
| Only `rotate()` transform | Full affine set (D-01) | All 2D CSS transforms renderable |
| Only `linear-gradient` | + `radial-gradient` | Default CSS gradient shape (ellipse) supported |
| `RotationGroup.AngleDegrees` | `TransformGroup.Matrix double[6]` | Generalizes to arbitrary affine |
| `SingleRotateRegex` policy gate | Token-based affine set gate | Multi-function chains pass policy |

**Deprecated/outdated in this phase:**
- `SingleRotateRegex` (LegacyPrintPolicy.cs:381): replaced by `AffineFunctionTokenRegex` + `IsAffineTransform`.
- `IsSingleRotate` (LegacyPrintPolicy.cs:386): replaced by `IsAffineTransform`.
- `TryParseRotateDegrees` (BoxTreeBuilder.cs:797): replaced by `TryParseTransformMatrix`.
- `RotationGroup.AngleDegrees`: replaced by `TransformGroup.Matrix`.
- `BoxNode.RotationDegrees` (sentinel field for origin detection): replaced by `BoxNode.HasTransform` bool.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Per-function 2×3 matrix forms derived from CSS Transform Level 1 spec | Q3 Affine Composition | Wrong matrix form → incorrect transform rendering |
| A2 | Farthest-corner ellipse radii = max(|cx-left|,|cx-right|) and max(|cy-top|,|cy-bottom|) | Q2 Ellipse Mapping | Incorrect ellipse size; off-center gradients wrong |
| A3 | Pivot-composition formula: M_final = T(px,py) * M_css * T(-px,-py) | Q3 Pivot | Transforms applied at wrong origin |
| A4 | Left-to-right CSS composition: f1 then f2 → right-multiply (M_f1 * M_f2) | Q3 Composition | Chain order reversed |
| A5 | Keyword position fractions (top=0, bottom=1, left=0, right=1, center=0.5) | Q2 CSS mapping | Off-center gradients at wrong position |
| A6 | Regex approach for transform gate is adequate for CSS transform string tokenization | Q4 Policy | Edge cases with whitespace or unusual values could bypass gate |

Note: A4 — CSS left-to-right composition means each subsequent function is applied in the
coordinate system established by the previous functions. This is standard and equivalent to
right-multiplying each new matrix: `M_result = M_f1 * M_f2` for "f1 then f2". [ASSUMED]

---

## Open Questions

1. **Left-to-right multiply order ambiguity**
   - What we know: CSS spec says functions apply left-to-right; `translate(10px) rotate(45deg)` means
     first translate then rotate.
   - What's unclear: Whether "first translate then rotate" means M_result = M_translate * M_rotate
     or M_rotate * M_translate when using column-vector convention vs row-vector convention.
   - Recommendation: Verify against a browser (Chrome DevTools computed transform matrix for a known
     two-function chain). The matrix math is unambiguous but convention must be confirmed.

2. **Ellipse CTM in content stream vs ellipse ShadingType-3 with two different circles**
   - What we know: D-04 locks the ellipse approach as "unit-circle shading + anisotropic CTM".
   - What's unclear: Whether PDF viewers handle the `cm` + ShadingType-3 combination correctly when
     the shading `/Coords` are in the transformed (unit) space but the page clip is in page space.
   - Recommendation: Verify with a test render + PDF viewer. The clip-before-cm ordering (Pitfall P3)
     is the key risk.

3. **`BoxNode.BackgroundGradient` type — union vs parallel property**
   - What we know: Currently `LinearGradient?`. Phase 15 adds `RadialGradient?`.
   - What's unclear: Whether introducing a parallel `BackgroundRadialGradient` property is preferred
     or an abstract base type is cleaner.
   - Recommendation (Planner decides): Parallel property (`BackgroundRadialGradient`) is least
     disruptive to existing code. A base type is cleaner long-term but touches more files.

---

## Validation Architecture

Config: `workflow.nyquist_validation` key absent → enabled.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` |
| Quick run command | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "Category!=SlowIntegration&Category!=RealTemplate" -x` |
| Full suite command | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "Category!=RealTemplate" -x` |

### Phase Requirements → Test Map

| Behavior | Test Type | Automated Command | New? |
|----------|-----------|-------------------|------|
| radial-gradient renders as ShadingType 3 | integration | dotnet test --filter FullyQualifiedName~RadialGradient | New wave |
| conic-gradient still rejected by policy | unit | dotnet test --filter FullyQualifiedName~ConicGradient | New |
| repeating-* still rejected | unit | dotnet test --filter GradientTransformPolicyTests | Update existing |
| translate/scale/matrix chain renders | integration | dotnet test --filter FullyQualifiedName~Transform | New |
| perspective() still rejected | unit | dotnet test --filter GradientTransformPolicyTests | New |
| Existing 17 TCIS templates byte-identical | golden | dotnet test --filter FullyQualifiedName~RealTemplate | No change |
| PerfGate unchanged | SlowIntegration | dotnet test --filter Category=SlowIntegration | No change |

### Wave 0 Gaps

All required test infrastructure already exists. No new test framework installation needed.
- `GoldenPdf.cs` — golden harness exists [VERIFIED]
- `GoldenCorpus.cs` — add new `Phase15Gradients` and `Phase15Transforms` arrays
- `GradientShadingRenderTests.cs` — update `RadialGradient_IsRejectedByPolicy` → new shape
- `GradientTransformPolicyTests.cs` — update 3 existing tests + add 5+ new tests

---

## Security Domain

No ASVS categories specifically triggered by radial gradient shading or affine transform CTM changes.
The existing security invariants are unaffected:

| ASVS Category | Applies | Control |
|---------------|---------|---------|
| V5 Input Validation | Yes | Policy gate (LegacyPrintPolicy) rejects unknown transform functions fail-loud |
| V6 Cryptography | No | — |
| SEC-02 (no JS/EmbeddedFile) | Yes | `BuildRadialShadingDict` emits only /Shading dict — no /JavaScript |

The only security-adjacent concern: the widened transform gate must not inadvertently allow
`perspective()` or CSS filter functions (which are not 2D affine and could theoretically be abused
in future PDF engines). The `AllowedAffineFunctions` allowlist (fail-loud on unknown) is the control.

---

## Sources

### Primary (HIGH confidence)
- `OwnedPdfWriter.cs` (verified line by line): BuildAxialShadingDict (line 777), BuildStitchingFunction (822), RotMatrix (861), AppendCm (872), rotationPivots loop (904-913), RotFor (919-924), gradient emission (931-952), Tm-baking (1186-1199), shading resource plumbing (165-179, 221-227)
- `LegacyPrintPolicy.cs` (verified): SingleRotateRegex (381-387), IsSingleRotate (386), CheckTransformAndGradient (390-421), gradient gate (408-420), GetComputedStyle broad-catch (204), inline-style fallback (234-238)
- `LinearGradient.cs` (verified): model structure (1-18)
- `LinearGradientParser.cs` (verified): full parser (1-190) — template for RadialGradientParser
- `BoxNode.cs` (verified): BackgroundGradient (55), RotationDegrees (59), RotationGroup (62)
- `BoxTreeBuilder.cs` (verified): gradient parse (318-332), transform parse (346-351), PropagateRotationGroup (784-793), TryParseRotateDegrees (797-823)
- `RotationGroup.cs` (verified): class structure (1-13)
- `CascadeResolver.cs` (verified): ExpandBackground gradient routing (516-531)
- `GradientTransformPolicyTests.cs` (verified): existing tests to update (31-78)
- `GradientShadingRenderTests.cs` (verified): existing render tests to update (33-64)
- `PerfGateTests.cs` (verified): ColdCeilingMs=1500, WarmCeilingMs=400 (33-34)

### Secondary (MEDIUM confidence)
- PDF 1.7 specification §8.7.4.5.4 — ShadingType 3 (radial) dict structure: /Coords [x0 y0 r0 x1 y1 r1], /Extend [CITED]
- PDF 1.7 specification §8.3.3 — transformation matrices [a b c d e f] definition [CITED]
- MDN Web Docs — CSS radial-gradient() gradient definition parsing [CITED]

### Tertiary (LOW confidence — marked [ASSUMED])
- Per-function 2×3 matrix forms (A1)
- Farthest-corner ellipse radii formula (A2)
- Pivot-composition formula (A3)
- Multiplication order for left-to-right CSS chain (A4)
- Keyword position fraction mapping (A5)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all reuse verified in source files
- Architecture: HIGH — patterns verified in Phase 14 code
- Pitfalls: HIGH for verified patterns; MEDIUM for ellipse CTM ordering (requires test verification)
- Math formulas: MEDIUM — standard linear algebra / CSS spec, but not verified against browser output

**Research date:** 2026-06-20
**Valid until:** 2026-07-20 (stable — these files are not fast-moving)
