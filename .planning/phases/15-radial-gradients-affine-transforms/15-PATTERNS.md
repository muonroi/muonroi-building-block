# Phase 15: Radial Gradients + Affine Transforms — Pattern Map

**Mapped:** 2026-06-20
**Files analyzed:** 8 new/modified files
**Analogs found:** 8 / 8 (all from Phase 14 infrastructure)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradient.cs` | model | transform | `LinearGradient.cs` | exact |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradientParser.cs` | utility | transform | `LinearGradientParser.cs` | exact |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs` (modify → TransformGroup) | model | transform | itself (Phase 14) | exact |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` (modify) | model | transform | itself (Phase 14) | exact |
| `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` (modify) | service | transform | itself (Phase 14) | exact |
| `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` (modify) | service | request-response | itself (Phase 14) | exact |
| `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` (modify) | middleware | request-response | itself (Phase 14) | exact |
| `tests/Muonroi.Pdf.Tests/Policy/GradientTransformPolicyTests.cs` (modify + extend) | test | request-response | itself (Phase 14) | exact |
| `tests/Muonroi.Pdf.Tests/Service/GradientShadingRenderTests.cs` (modify + extend) | test | request-response | itself (Phase 14) | exact |

---

## Pattern Assignments

### `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradient.cs` (model, new file)

**Analog:** `src/Muonroi.Pdf/Internal/Layout/Boxes/LinearGradient.cs`

**Full analog** (lines 1–19):
```csharp
namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// A parsed CSS <c>linear-gradient(...)</c> background (Phase 14). Rendered by the writer as a PDF
/// axial shading (ShadingType 2). <see cref="AngleDegrees"/> follows CSS convention: ...
/// </summary>
internal sealed class LinearGradient
{
    /// <summary>CSS gradient angle in degrees (0 = to top, 90 = to right, 180 = to bottom).</summary>
    public float AngleDegrees { get; init; } = 180f;

    /// <summary>Ordered color stops (at least two when the gradient is renderable).</summary>
    public IReadOnlyList<GradientStop> Stops { get; init; } = System.Array.Empty<GradientStop>();
}

/// <summary>A single gradient color stop. <see cref="Position"/> is a 0..1 fraction, or null (auto).</summary>
internal readonly record struct GradientStop(string Color, float? Position);
```

**What to copy:** Namespace, `internal sealed class` pattern, `init`-only properties, `Array.Empty<GradientStop>()` default, XML doc style.

**What to change for `RadialGradient`:**
- Replace `AngleDegrees` with `Shape` (string, default `"ellipse"`), `PositionX` (float, default 0.5f), `PositionY` (float, default 0.5f).
- `GradientStop` is shared (already declared in `LinearGradient.cs` line 18) — do NOT re-declare it; add a `using` or put `RadialGradient` in the same file or namespace without redeclaring the record.
- MSTD0002: no `!` operator; all properties are value types here so no null concern.

---

### `src/Muonroi.Pdf/Internal/Layout/Boxes/RadialGradientParser.cs` (utility, new file)

**Analog:** `src/Muonroi.Pdf/Internal/Layout/Boxes/LinearGradientParser.cs` (lines 1–190)

**Imports pattern** (lines 1–4):
```csharp
using System.Collections.Generic;
using System.Globalization;

namespace Muonroi.Pdf.Internal.Layout.Boxes;
```

**Class declaration pattern** (line 13):
```csharp
internal static class LinearGradientParser
```
Copy verbatim: `internal static class RadialGradientParser`

**TryParse shell pattern** (lines 15–57):
```csharp
public static bool TryParse(string css, out LinearGradient gradient)
{
    gradient = new LinearGradient();
    if (string.IsNullOrWhiteSpace(css))
        return false;

    int open = css.IndexOf("linear-gradient(", System.StringComparison.OrdinalIgnoreCase);
    if (open < 0)
        return false;

    int argsStart = open + "linear-gradient(".Length;
    int argsEnd = MatchParen(css, argsStart - 1);
    if (argsEnd < 0)
        return false;

    string args = css.Substring(argsStart, argsEnd - argsStart);
    List<string> parts = SplitTopLevel(args, ',');
    if (parts.Count == 0)
        return false;
    // ... direction/stop parsing
    gradient = new LinearGradient { AngleDegrees = angle, Stops = stops };
    return true;
}
```
Replace `"linear-gradient("` → `"radial-gradient("`, first-part parsing detects shape/position keywords instead of angle, remaining parts are color stops using the same `ParseStop` helper.

**Helpers to copy verbatim** (lines 59–189) — copy ALL private helpers:
- `ParseStop` (lines 59–80): identical, parses color + optional `%` position.
- `ParsePositionFraction` (lines 82–90): identical.
- `MatchParen` (lines 155–169): identical — finds matching `)`.
- `SplitTopLevel` (lines 171–189): identical — top-level comma split, rgb()-safe.

**Helpers NOT to copy:** `IsDirection`, `ParseAngle`, `ParseToSide`, `Normalize`, `TryUnit`, `EndsWithUnit` — these are linear-gradient-specific. Replace with radial-gradient keyword parsing (`IsGradientDefinitionPart`, `ParseShapeAndPosition`).

**Error handling:** return `false` on any malformed input — never throw (lines 15–18 pattern, same as linear parser).

---

### `src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs` (modify → TransformGroup)

**Current file** (lines 1–13) — full content to replace:
```csharp
namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Phase 14: a shared rotation context for a <c>transform: rotate()</c> block and all of its
/// descendant boxes, so the block and its text rotate as a rigid group about a single pivot. ...
/// Grouping is by reference identity.
/// </summary>
internal sealed class RotationGroup
{
    /// <summary>CSS rotation in degrees (clockwise; 45 = quarter-turn clockwise on screen).</summary>
    public float AngleDegrees { get; init; }
}
```

**Pattern to apply:** Rename class `RotationGroup` → `TransformGroup`. Replace `float AngleDegrees` with `double[] Matrix { get; init; }` (length-6 affine `[a,b,c,d,e,f]`, pivot-composed at parse time, in layout/CSS coordinates). Update XML doc to say "Phase 15: shared affine transform context". Keep `internal sealed class`, keep grouping-by-reference-identity semantics.

**MSTD0002:** `Matrix` is `double[]` (reference type) — all access in callers must use `?.` patterns or null-check before indexing. Do not use `!`.

---

### `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` (modify)

**Analog:** itself — current Phase 14 gradient/transform carrier fields (lines 54–62):
```csharp
/// <summary>Parsed CSS linear-gradient background (Phase 14). Null = no gradient.</summary>
public LinearGradient? BackgroundGradient { get; set; }

/// <summary>CSS transform:rotate() angle in degrees on this box (Phase 14). 0 = no rotation.
/// Only the origin block carries a non-zero value; descendants share the <see cref="RotationGroup"/>.</summary>
public float RotationDegrees { get; set; }

/// <summary>Shared rotation context (Phase 14) for a rotated block and its descendants. Null = none.</summary>
public RotationGroup? RotationGroup { get; set; }
```

**What to add/change:**
- Add after `BackgroundGradient`: `public RadialGradient? BackgroundRadialGradient { get; set; }` (parallel property, least-disruptive approach from RESEARCH.md Q6 Option 3).
- Rename `RotationGroup? RotationGroup` → `TransformGroup? TransformGroup` (same reference-identity grouping semantics).
- Replace `float RotationDegrees` → `bool HasTransform` (bool sentinel — cleaner than a float sentinel; replaces the "RotationDegrees != 0f" origin-box detection used in `OwnedPdfWriter.cs:907`).
- Keep all other fields (`BackgroundColor`, `BackgroundImageSrc`, etc.) unchanged.

**MSTD0002:** `RadialGradient?` and `TransformGroup?` — use `?.` in all callers.

---

### `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` (modify)

**Analog:** itself — Phase 14 gradient and transform parse seams.

**Gradient parse pattern to extend** (lines 316–332):
```csharp
// linear-gradient background (Phase 14): from background-image or the background shorthand.
string? gradientSource = bgImage;
if (string.IsNullOrEmpty(gradientSource)
    || !gradientSource.Contains("linear-gradient", StringComparison.OrdinalIgnoreCase))
{
    string? bgShorthand = style.GetValue("background");
    if (!string.IsNullOrEmpty(bgShorthand)
        && bgShorthand.Contains("linear-gradient", StringComparison.OrdinalIgnoreCase))
        gradientSource = bgShorthand;
}
if (!string.IsNullOrEmpty(gradientSource)
    && gradientSource.Contains("linear-gradient", StringComparison.OrdinalIgnoreCase)
    && LinearGradientParser.TryParse(gradientSource, out LinearGradient grad))
{
    box.BackgroundGradient = grad;
}
```

**Phase 15 extension:** Widen `gradientSource` detection to also match `"radial-gradient"`. Add a radial branch (check before or after linear — `else if` pattern):
```csharp
// Phase 15: also check radial-gradient in gradientSource resolution
if (...gradientSource.Contains("radial-gradient", ...) && RadialGradientParser.TryParse(...))
    box.BackgroundRadialGradient = radGrad;
else if (...gradientSource.Contains("linear-gradient", ...) && LinearGradientParser.TryParse(...))
    box.BackgroundGradient = grad;
```

**Transform parse pattern to replace** (lines 346–351):
```csharp
var transformVal = style.GetValue("transform");
if (!string.IsNullOrEmpty(transformVal) && TryParseRotateDegrees(transformVal, out float rotDeg) && rotDeg != 0f)
{
    box.RotationDegrees = rotDeg;
    box.RotationGroup = new RotationGroup { AngleDegrees = rotDeg };
}
```
Replace `TryParseRotateDegrees` with `TryParseTransformMatrix` returning `double[]`. Replace `RotationGroup` → `TransformGroup`. Replace `RotationDegrees = rotDeg` → `HasTransform = true`.

**Propagation pattern to rename** (lines 782–793):
```csharp
private static void PropagateRotationGroup(BoxNode node, RotationGroup group)
{
    foreach (var child in node.Children)
    {
        if (child.RotationGroup is not null)
            continue;
        child.RotationGroup = group;
        PropagateRotationGroup(child, group);
    }
}
```
Rename to `PropagateTransformGroup`, change field name to `TransformGroup`. Logic identical.

**`TryParseRotateDegrees` (lines 797–824):** Replace entirely with `TryParseTransformMatrix(string transform, out double[] matrix)`. The new function tokenizes CSS transform functions, validates each is in the allowed affine set (see LegacyPrintPolicy pattern for the function name set), and composes left-to-right. Returns `false` on any unknown function or unparseable args.

**Imports:** Add `using Muonroi.Pdf.Internal.Layout.Boxes;` already present; no new usings needed beyond existing (no external libs added).

---

### `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` (modify)

**Analog:** itself — Phase 14 shading and rotation patterns.

**Imports pattern** (lines 10–21):
```csharp
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
```
No new usings needed for Phase 15.

**Shading loop pattern to extend** (lines 165–179):
```csharp
// Phase 14: linear-gradient backgrounds → one inline axial shading per gradient element.
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
Phase 15 extends the `if` to also check `BackgroundRadialGradient`, calling `BuildRadialShadingDict` (which returns `(string dict, string? ellipseCm)`). The `gradientResNames` dict maps element → shading resource name, unchanged.

**`BuildAxialShadingDict` pattern to mirror** (lines 777–820) — the structural template for `BuildRadialShadingDict`:
```csharp
private static string BuildAxialShadingDict(LinearGradient g, Rect rect, float pageHeightPt)
{
    float w = rect.Width;
    float h = rect.Height;
    float bgX = rect.X;
    float bgY = pageHeightPt - rect.Y - rect.Height;   // ← PDF y-flip (MUST copy exactly)
    // ... geometry ...
    var sb = new StringBuilder();
    sb.Append("<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [");
    // ... coords ...
    sb.Append("] /Domain [0 1] /Function ");
    sb.Append(BuildStitchingFunction(colors, pos));     // ← reuse unchanged
    sb.Append(" /Extend [true true] >>");
    return sb.ToString();

    static string Num(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
}
```
`BuildRadialShadingDict` copies this structure with `/ShadingType 3`, `/Coords [cx cy 0 cx cy r]` for circle or `/Coords [0 0 0 0 0 1]` for ellipse unit-circle, and `out string? ellipseCm` for the anisotropic scale.

**`BuildStitchingFunction` (lines 822–857):** Reuse unchanged. Takes `(float R, float G, float B)[]` colors and `float[]` pos arrays — identical for radial.

**`RotMatrix` pattern to generalize** (lines 859–869):
```csharp
// Phase 14: affine matrix [a b c d e f] for a CSS rotate(angleDegCss) about pivot (px,py)
private static (double A, double B, double C, double D, double E, double F) RotMatrix(
    float angleDegCss, double px, double py)
{
    double phi = -angleDegCss * Math.PI / 180.0;   // ← negate for PDF y-up
    double a = Math.Cos(phi), b = Math.Sin(phi), c = -Math.Sin(phi), d = Math.Cos(phi);
    double e = px - px * a - py * c;
    double f = py - px * b - py * d;
    return (a, b, c, d, e, f);
}
```
Phase 15 replaces (or supplements) with `TransformFor(PositionedElement el)` that reads `el.Source?.TransformGroup?.Matrix` and returns the same tuple type. `RotMatrix` is no longer called if `TransformGroup.Matrix` already contains the pivot-composed matrix from parse time.

**`AppendCm` (lines 872–881):** Copy verbatim — already generic, takes any `(A,B,C,D,E,F)` tuple:
```csharp
private static void AppendCm(
    StringBuilder sb, (double A, double B, double C, double D, double E, double F) m)
{
    sb.Append(m.A.ToString("F6", CultureInfo.InvariantCulture)); sb.Append(' ');
    // ... A B C D with F6, E F with F4 ...
    sb.Append(m.F.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" cm");
}
```

**`RotFor` local function pattern** (lines 919–924):
```csharp
(double A, double B, double C, double D, double E, double F)? RotFor(PositionedElement el)
{
    if (el.Source?.RotationGroup is { } grp && rotationPivots.TryGetValue(grp, out (double Px, double Py) p))
        return RotMatrix(grp.AngleDegrees, p.Px, p.Py);
    return null;
}
```
Phase 15: rename to `TransformFor`, change to read `el.Source?.TransformGroup is { } grp` and return the matrix directly from `grp.Matrix` (pivot already composed at parse time). The `rotationPivots` dict becomes `_transformPivots` or is removed if pivot is stored in `TransformGroup`.

**Rotation pivot resolution pattern** (lines 901–913):
```csharp
var rotationPivots = new Dictionary<RotationGroup, (double Px, double Py)>();
foreach (PositionedElement el in page.Elements)
{
    if (el.Source is { RotationDegrees: not 0f, RotationGroup: { } originGroup })
    {
        double px = el.Position.X + el.Position.Width / 2.0;
        double py = pageHeightPt - (el.Position.Y + el.Position.Height / 2.0);
        rotationPivots[originGroup] = (px, py);
    }
}
```
Phase 15: change `RotationDegrees: not 0f` → `HasTransform: true`, `RotationGroup` → `TransformGroup`. The pivot (px, py) computation is unchanged — box center in PDF coords.

**Gradient content stream emission pattern** (lines 929–952):
```csharp
if (el.Source?.BackgroundGradient is { Stops.Count: >= 2 }
    && gradientResNames.TryGetValue(el, out string? shName))
{
    sb.AppendLine("ET");
    float gx = el.Position.X;
    float gy = pageHeightPt - el.Position.Y - el.Position.Height;
    float gw = el.Position.Width;
    float gh = el.Position.Height;
    sb.AppendLine("q");
    if (RotFor(el) is { } gRot)
        AppendCm(sb, gRot);
    sb.Append(gx...).Append(" re W n");   // clip first, then sh
    sb.AppendLine($"/{shName} sh");
    sb.AppendLine("Q");
    // ...
}
```
Phase 15: add a parallel block for `BackgroundRadialGradient`. For ellipse, insert the anisotropic `cm` AFTER the clip rect (critical ordering from RESEARCH.md Pitfall P3): `q`, clip `re W n`, element `TransformFor cm` (if any), ellipse `cm` (if ellipse), `sh`, `Q`.

**Tm-baking pattern** (lines 1186–1199) — copy verbatim, already generic:
```csharp
if (RotFor(el) is { } tRot)
{
    double ex = tRot.A * pdfXt + tRot.C * pdfYt + tRot.E;
    double fy = tRot.B * pdfXt + tRot.D * pdfYt + tRot.F;
    sb.Append(tRot.A...).Append(" Tm");
}
```
No change needed to this math — it already handles any 2×3 matrix. Only `RotFor` → `TransformFor` rename.

---

### `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` (modify)

**Analog:** itself — Phase 14 policy gate patterns.

**GetComputedStyle broad-catch pattern** (lines 195–241) — copy intact, no changes:
```csharp
catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
{
    style = null;
    // ... stylesheet scan fallback ...
}
if (style is null)
{
    // inline-style="" fallback path
    string inlineCss = element.GetAttribute("style") ?? string.Empty;
    if (inlineCss.Length > 0)
    {
        CheckTransformAndGradient(
            InlineDeclValue(inlineCss, "transform"),
            InlineDeclValue(inlineCss, "background"),
            InlineDeclValue(inlineCss, "background-image"),
            inlineSel, violations);
    }
    continue;
}
```
The new `IsAffineTransform` gate must be called in `CheckTransformAndGradient`, which is already called from both the computed-style and inline-style paths — no structural change needed here.

**`SingleRotateRegex` / `IsSingleRotate` pattern to replace** (lines 381–387):
```csharp
private static readonly System.Text.RegularExpressions.Regex SingleRotateRegex = new(
    @"^\s*rotate\(\s*-?\d*\.?\d+(deg|rad|grad|turn)?\s*\)\s*$",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase
    | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

private static bool IsSingleRotate(string transform) =>
    SingleRotateRegex.IsMatch(transform);
```
Replace with `AffineFunctionTokenRegex` (Compiled, extracts `name(args)` tokens) + `AllowedAffineFunctions` `HashSet<string>` + `IsAffineTransform(string transform)` method. Mirror the `Regex` field declaration style (`private static readonly ... Regex`).

**`CheckTransformAndGradient` violation pattern** (lines 389–421):
```csharp
private static void CheckTransformAndGradient(
    string? transform, string? background, string? backgroundImage,
    string selector, List<PolicyViolation> violations)
{
    // ...
    if (!string.IsNullOrEmpty(transform) && !IsSingleRotate(transform))
    {
        violations.Add(ViolationFor("forbidden.transform.geometric", "transform", transform, selector,
            "Only transform:rotate(<angle>) is supported; remove other transform functions"));
    }

    string gradientSource = background.Contains("gradient", ...) ? background : backgroundImage;
    if (gradientSource.Contains("gradient", ...))
    {
        bool isLinearOnly =
            gradientSource.Contains("linear-gradient(", ...)
            && !gradientSource.Contains("radial-gradient", ...)
            && !gradientSource.Contains("conic-gradient", ...)
            && !gradientSource.Contains("repeating-", ...);
        if (!isLinearOnly)
        {
            violations.Add(ViolationFor("forbidden.background.gradient", ...));
        }
    }
}
```
Phase 15 changes:
1. `!IsSingleRotate(transform)` → `!IsAffineTransform(transform)`. Update suggestion text to list allowed function names.
2. `isLinearOnly` → `isAllowedGradient`: add `|| gradientSource.Contains("radial-gradient(", ...)` to the allow-side. Update suggestion text to "Use linear-gradient or radial-gradient; other gradient functions are not supported."

**`InlineDeclValue` helper** (lines 423+) — copy verbatim, no change.

---

### `tests/Muonroi.Pdf.Tests/Policy/GradientTransformPolicyTests.cs` (modify + extend)

**Analog:** itself — existing Phase 14 test structure (lines 1–78).

**Test class/harness pattern** (lines 14–29):
```csharp
public sealed class GradientTransformPolicyTests
{
    private static async Task<IPdfDocumentContext> ParseAsync(string html) { ... }
    private static async Task<PolicyValidationResult> ValidateAsync(string css)
    {
        string html = $"<html><head><style>div{{{css}}}</style></head><body><div>x</div></body></html>";
        IPdfDocumentContext context = await ParseAsync(html);
        return await new LegacyPrintPolicy().ValidateAsync(context);
    }
```
Copy verbatim — identical harness for new tests.

**Existing tests to UPDATE (flip assertion polarity, rename):**

| Line | Current name | Current assertion | Phase 15 change |
|------|-------------|-------------------|-----------------|
| 40 | `RadialGradient_IsRejected` | `.Contain(v => v.RuleId == "forbidden.background.gradient")` | Flip to `.NotContain(...)`; rename `RadialGradient_IsAllowed` |
| 64 | `TransformTranslate_IsRejected` | `.Contain(v => v.RuleId == "forbidden.transform.geometric")` | Flip to `.NotContain(...)`; rename `TransformTranslate_IsAllowed` |
| 72 | `TransformRotateWithScale_IsRejected` | `.Contain(...)` | Flip to `.NotContain(...)`; rename `TransformChain_IsAllowed` |

**Existing test to keep unchanged:**
- `LinearGradient_IsAllowed` (line 32) — unchanged.
- `RepeatingLinearGradient_IsRejected` (line 47) — unchanged.
- `TransformRotate_IsAllowed` (line 55) — unchanged.

**New test pattern** — copy the `[Fact] public async Task ...` shape:
```csharp
[Fact]
public async Task ConicGradient_IsRejected()
{
    PolicyValidationResult result = await ValidateAsync("background:conic-gradient(red,blue);");
    result.Violations.Should().Contain(v => v.RuleId == "forbidden.background.gradient",
        because: "conic-gradient is not supported");
}
```
Add: `ConicGradient_IsRejected`, `RepeatingRadialGradient_IsRejected`, `TransformScale_IsAllowed`, `TransformMatrix_IsAllowed`, `TransformChain_IsAllowed` (multi-function chain), `TransformPerspective_IsRejected`, `TransformUnknownFunction_IsRejected`.

---

### `tests/Muonroi.Pdf.Tests/Service/GradientShadingRenderTests.cs` (modify + extend)

**Analog:** itself — Phase 14 render test structure (lines 1–64).

**RenderAsync harness pattern** (lines 22–30):
```csharp
private static async Task<byte[]> RenderAsync(string bodyInner)
{
    using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
    var svc = provider.GetRequiredService<IMPdfService>();
    string html = "<html><head>" + FontFace + "</head><body>" + bodyInner + "</body></html>";
    using var ms = new MemoryStream();
    await svc.RenderAsync(html, ms, new PdfRenderOptions { TemplateId = PdfServiceTestHarness.TemplateId }, default);
    return ms.ToArray();
}
```
Copy verbatim — identical for new tests.

**Existing test to UPDATE:**
- Line 55: `RadialGradient_IsRejectedByPolicy` — flip from `ThrowAsync<PdfPolicyException>` to successful render assertion; rename `RadialGradient_EmitsRadialShading`. Assert `text.Should().Contain("/ShadingType 3")` and `text.Should().Contain("/Coords")`.

**Structural assertion pattern for new radial tests** — mirror `LinearGradientDiv_EmitsAxialShading` (lines 33–43):
```csharp
[Fact]
public async Task LinearGradientDiv_EmitsAxialShading()
{
    byte[] bytes = await RenderAsync(
        "<div style=\"height:40px;background:linear-gradient(90deg,#0c6b6b,#ffffff);\">band</div>");
    string text = Encoding.ASCII.GetString(bytes);
    text.Should().StartWith("%PDF-1.7");
    text.Should().Contain("/ShadingType 2", because: "...");
    text.Should().Contain("/Coords", because: "...");
    text.Should().Contain("/FunctionType 2", because: "...");
}
```
New tests: `RadialGradient_EmitsRadialShading` (`/ShadingType 3`), `RadialGradientThreeStop_UsesStitchingFunction` (`/FunctionType 3`), `TransformTranslate_EmitsCm` (content stream has `cm`), `TransformChain_EmitsSingleCm` (only one `cm` for multi-function chain).

---

## Shared Patterns

### MSTD0002 — No null-forgiving `!` operator
**Source:** Project-wide rule in CLAUDE.md / CONTEXT.md
**Apply to:** All files in `Muonroi.Pdf` namespace
```csharp
// CORRECT — use ?. for nullable property chains
if (el.Source?.TransformGroup is { } grp && grp.Matrix is { Length: 6 } m)
    return (m[0], m[1], m[2], m[3], m[4], m[5]);

// FORBIDDEN — null-forgiving ! operator
var m = el.Source!.TransformGroup!.Matrix;
```

### No Silent Catch
**Source:** CLAUDE.md project rule
**Apply to:** Any try/catch added in Phase 15
```csharp
// CORRECT — log with context before returning null/false
catch (Exception ex)
{
    // [ModuleName] operation failed: {ex.Message}
    return false;
}

// FORBIDDEN — empty/silent catch
catch { return false; }
```

### Error Handling in Policy Gate (fail-loud contract)
**Source:** `LegacyPrintPolicy.cs` lines 398–402
```csharp
if (!string.IsNullOrEmpty(transform) && !IsAffineTransform(transform))
{
    violations.Add(ViolationFor("forbidden.transform.geometric", "transform", transform, selector,
        "Only affine transform functions (translate, scale, rotate, skew, matrix) are supported"));
}
```
Apply to: `LegacyPrintPolicy.CheckTransformAndGradient` — unknown function names must be reported in the violation message.

### PDF Y-Flip Convention
**Source:** `OwnedPdfWriter.cs` lines 782, 864, 910
```csharp
// Layout → PDF: y-flip for bottom-of-rect
float bgY = pageHeightPt - rect.Y - rect.Height;
// Layout → PDF: y-flip for center point
double py = pageHeightPt - (el.Position.Y + el.Position.Height / 2.0);
// CSS rotation → PDF: negate angle (CSS CW = PDF CCW)
double phi = -angleDegCss * Math.PI / 180.0;
```
Apply to: `BuildRadialShadingDict` (center coords), `TransformFor` (pivot), and anywhere layout coordinates are converted to PDF space.

### Gradient Content Stream Ordering (clip-before-cm)
**Source:** `OwnedPdfWriter.cs` lines 940–948 (Phase 14 pattern — clip then rotate)
```csharp
sb.AppendLine("q");
if (RotFor(el) is { } gRot)   // cm BEFORE clip in Phase 14 (but clip should be FIRST per P3)
    AppendCm(sb, gRot);
sb.Append(gx...).AppendLine(" re W n");   // clip
sb.AppendLine($"/{shName} sh");
sb.AppendLine("Q");
```
**CRITICAL NOTE (Pitfall P3 from RESEARCH.md):** For radial ellipse, the anisotropic `cm` must come AFTER the clip `re W n`, not before. The correct Phase 15 order is:
```
q
[clip re W n]                   ← in page user space FIRST
[element TransformFor cm]       ← element rotation/transform if any
[ellipse anisotropic cm]        ← rx 0 0 ry cx cy cm (ellipse only)
/ShN sh
Q
```
This differs from Phase 14's linear-gradient order (which puts `cm` before the clip). The linear-gradient cm is a rotation about center — clipping before or after gives same visual result because the box rect and rotation center are the same. For radial ellipse, the clip must be in page space before the scale CTM changes the coordinate system.

### `StringBuilder` Num() helper pattern
**Source:** `OwnedPdfWriter.cs` lines 819, 853–856
```csharp
static string Num(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
// F6 for matrix elements A-D, F4 for translation E-F and coordinates
```
Apply to: `BuildRadialShadingDict` and any ellipse `cm` string construction.

---

## No Analog Found

None — all Phase 15 files have close analogs in Phase 14 infrastructure.

---

## Key Pitfalls Summary for Planner

| # | File | Risk | Mitigation (from RESEARCH.md) |
|---|------|------|-------------------------------|
| P3 | OwnedPdfWriter.cs | Clip rect wrong after ellipse CTM | Emit clip `re W n` BEFORE any `cm` in radial shading block |
| P5 | BoxTreeBuilder + OwnedPdfWriter | Pivot y-flip applied twice | Either compose pivot at parse time OR resolve pivot in writer — not both |
| P6 | All Muonroi.Pdf files | MSTD0002: `!` operator triggers analyzer | Use `?.` and `is { }` patterns everywhere |
| P7 | GradientTransformPolicyTests.cs:40 | `RadialGradient_IsRejected` test will fail | Update to `RadialGradient_IsAllowed` (flip assertion) |
| P8 | GradientShadingRenderTests.cs:55 | `RadialGradient_IsRejectedByPolicy` will fail | Update to `RadialGradient_EmitsRadialShading` |

---

## Metadata

**Analog search scope:** `src/Muonroi.Pdf/`, `src/Muonroi.Pdf.Governance/`, `tests/Muonroi.Pdf.Tests/`
**Files read:** 10 source files + 2 test files
**Pattern extraction date:** 2026-06-20
**Valid until:** 2026-07-20 (stable Phase 14 infrastructure)
