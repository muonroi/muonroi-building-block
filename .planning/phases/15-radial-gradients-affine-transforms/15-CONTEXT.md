# Phase 15: Radial Gradients + Affine Transforms - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Extend the two writer-level CSS features shipped in Phase 14, reusing their infrastructure:

1. **`radial-gradient(...)` backgrounds** — render as PDF radial shading (ShadingType 3),
   alongside the Phase 14 `linear-gradient` axial shading (ShadingType 2).
2. **Full 2D affine `transform`** — generalize the Phase 14 single-`rotate()` support to the
   complete affine set, composed into one CTM.

Additive only — no golden re-baseline expected; existing 17 TCIS templates must stay byte-identical.
</domain>

<decisions>
## Implementation Decisions

### Transform function set (policy gate)
- **D-01:** Allow the **full 2D affine set**: `translate`/`translateX`/`translateY`,
  `scale`/`scaleX`/`scaleY`, `rotate`, `skew`/`skewX`/`skewY`, `matrix(a,b,c,d,e,f)`, and
  **multi-function chains** (e.g. `transform: translate(..) rotate(..) scale(..)`). All functions
  compose left-to-right into a **single 2×3 affine matrix → one CTM**. Rationale: implementation is
  uniform (matrix multiply) regardless of which functions are allowed; `matrix()` already expresses
  skew/reflection, so excluding `skew()` would be arbitrary and inconsistent. PDF handles any CTM.
- **D-02:** Widen the LegacyPrintPolicy transform gate (currently `SingleRotateRegex` /
  `IsSingleRotate` — single-rotate only). New gate: accept a whitespace-separated chain where every
  token is one of the allowed affine function names with numerically-parseable args; reject any
  unknown function token (fail-loud, keep the No-Silent-Catch + clear-violation contract). The
  inline-`style=""` fallback path added in Phase 14 must apply to the widened gate too.

### Transform origin (pivot)
- **D-03:** **Box-center only** — keep the Phase 14 pivot (box center). This equals the CSS default
  `transform-origin: 50% 50%`, so the common cases (watermark, badge, rotated/scaled block) are
  correct without parsing a new property. The `transform-origin` property is **deferred** (see
  Deferred Ideas) — revisit only if a template needs a non-center origin.

### radial-gradient shape + extent subset
- **D-04:** Support **both `circle` and `ellipse`**. Ellipse is MANDATORY because the CSS default
  shape for `radial-gradient(colorA, colorB)` is **ellipse** — a circle-only impl would reject or
  mis-render the most common form. Ellipse is rendered as a **unit-circle PDF radial shading wrapped
  in an anisotropic CTM scale** (scale x≠y) so the existing ShadingType-3 path stays single-shape.
- **D-05:** Position: support **`at center` (default) + keyword positions** (top/left/center/
  right/bottom and pairs). Extent: default **farthest-corner** (the CSS default when no
  size/extent given). **Deferred:** explicit pixel/`%` sizes and the full four extent keywords
  (`closest-side`/`closest-corner`/`farthest-side`/`farthest-corner`) — implement farthest-corner
  first; add others only on template demand.
- **D-06:** Policy: narrow the LegacyPrintPolicy gradient gate (currently `linear-gradient` only,
  Phase 14) to **also allow `radial-gradient`**. `conic-gradient` and all `repeating-*` gradients
  **stay rejected**. CascadeResolver already routes the `background` shorthand for gradients into
  `background-image` (Phase 14 fix) — reuse, no change needed there.

### Claude's Discretion
- Exact CTM composition math, matrix-decomposition vs direct-multiply, the radial `/Coords`
  (two-circle) computation + ellipse CTM derivation, parser structure (mirror `LinearGradientParser`),
  and how the affine matrix is baked into the text `Tm` vs object `cm` (extend Phase 14's
  `RotMatrix`/`AppendCm` + the rotation-bake-into-Tm approach to a general 2×3 matrix).
- Whether to generalize `RotationGroup` → a shared `TransformGroup`/affine-matrix carrier, or extend
  the existing type. Planner/researcher decides from the code.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 14 infrastructure to reuse (verified this session, shipped develop commit 3dfb7842)
- `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` — `BuildAxialShadingDict` (ShadingType 2 +
  FunctionType 2/3 stitching), `RotMatrix(angleDegCss, px, py)` (φ = −angle, PDF y-up), `AppendCm`,
  inline `/Shading` dict in page `/Resources`, rotation baked into text `Tm` (not BT/ET). The
  radial work adds a ShadingType-3 sibling; the affine work generalizes `RotMatrix`.
- `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` — `SingleRotateRegex`/`IsSingleRotate`
  (transform gate), the linear-gradient-only gradient gate, the broadened
  `ArgumentException or NullReferenceException` catch around `GetComputedStyle`, and the inline-
  `style=""` fallback (`InlineDeclValue`). Both gates widen here.
- `src/Muonroi.Pdf/Internal/Layout/Boxes/LinearGradient.cs` + `LinearGradientParser.cs` — model +
  parser to mirror for `RadialGradient`/`RadialGradientParser`.
- `src/Muonroi.Pdf/Internal/Layout/Boxes/RotationGroup.cs` + `BoxNode.cs`
  (`BackgroundGradient`/`RotationDegrees`/`RotationGroup`) — the box-model carriers to extend.
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — gradient parse + transform parse +
  `PropagateRotationGroup`/`TryParseRotateDegrees` (the parse/propagate seam to generalize).
- `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs` — `ExpandBackground` already routes
  gradients to `background-image` (Phase 14).

### Phase / planning context
- `.planning/ROADMAP.md` §"Phase 15: Radial Gradients + Affine Transforms" — goal, scope, SC.
- `.planning/phases/12-owned-css-cascade-b1/12-CONTEXT.md` — owned cascade is the layout style
  source (NOT `GetComputedStyle`); the policy gate still uses `GetComputedStyle` (Phase 14 gotcha).
- Memory: `pdf_phase14_css_gaps` (the GetComputedStyle/NRE/inline-style gotcha + CTM/shading facts).

### Docs to update at close (mirror Phase 14)
- `muonroi-docs/docs/03-guides/pdf/supported-html-css.md` — move radial-gradient + non-rotate
  transforms to supported.
- `muonroi-docs/docs/03-guides/pdf/pdf-vs-dinktopdf.md` — narrow the remaining-gaps list.
- Re-ingest `bb-docs` MCP on VPS at push time (see memory `bb_docs_mcp_reingest`: VPS-only,
  `CORE_ROOT=/opt/muonroi npm run ingest`, delete-by-filter the edited docs first).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Axial shading writer (Phase 14):** `OwnedPdfWriter.BuildAxialShadingDict` + the inline `/Shading`
  registry + `sh`-with-clip pattern. ShadingType 3 (radial) is a near-sibling — same resource-dict
  plumbing, different `/ShadingType` + `/Coords` (two circles) + the same FunctionType 2/3 stops.
- **CTM machinery (Phase 14):** `RotMatrix`/`AppendCm` + the bake-into-`Tm` approach generalize from
  a rotation matrix to any 2×3 affine with no new PDF-operator plumbing.
- **Parser shape:** `LinearGradientParser` (top-level paren-aware comma split, rgb()-comma-safe,
  `to <side>`/angle parse) is the template for `RadialGradientParser`.

### Established Patterns
- **Policy is fail-loud + GetComputedStyle-fragile:** new value checks must handle the throw and the
  inline-`style` attribute path (Phase 14 lesson). Both the transform and gradient gates live in
  `LegacyPrintPolicy` and must keep the structured-violation + suggested-alternative contract.
- **MSTD0002:** no null-forgiving `!` in Muonroi.Pdf namespaces — use `?.` / `MGuard.NotNull`.
- **Additive golden discipline:** existing 17 TCIS templates use no radial/non-rotate transforms, so
  no re-baseline expected; re-baseline only newly-added gradient/transform golden cases.

### Integration Points
- `BoxNode` gradient/transform carriers → `BoxTreeBuilder` parse/propagate → `OwnedPdfWriter`
  shading + CTM emission. Same three-layer path Phase 14 established.
</code_context>

<specifics>
## Specific Ideas

- Ellipse radial = unit-circle ShadingType-3 shading + anisotropic CTM scale (keeps one shading code
  path; CTM does the stretch). This is the key technical decision that lets D-04 reuse Phase 14.
- Affine = single composed 2×3 matrix; do NOT emit per-function nested `q…Q`/`cm` — compose first,
  emit once (mirrors Phase 14 baking rotation into one `Tm`).
</specifics>

<deferred>
## Deferred Ideas

- **`transform-origin` property** — non-center origins (keywords + length/%). Box-center (CSS
  default) covers Phase 15; revisit on template demand.
- **Explicit radial sizes + full extent keywords** — `closest-side`/`closest-corner`/
  `farthest-side` and explicit `<length>`/`%` radii. Farthest-corner (CSS default) first.
- **`conic-gradient` / `repeating-*` gradients** — stay rejected (out of scope, may never ship).
- **Flexbox (1D)** — deferred to Phase 16 (separate, larger layout-engine work).
- **CSS grid, JavaScript** — permanently out of scope (architectural choice).

</deferred>

---

*Phase: 15-radial-gradients-affine-transforms*
*Context gathered: 2026-06-20*
