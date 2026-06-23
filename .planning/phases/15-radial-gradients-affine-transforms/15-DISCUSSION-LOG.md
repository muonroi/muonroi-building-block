# Phase 15: Radial Gradients + Affine Transforms - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-20
**Phase:** 15-radial-gradients-affine-transforms
**Areas discussed:** Transform function set, Transform origin, radial-gradient shape/extent subset

---

## Transform function set (policy gate)

| Option | Description | Selected |
|--------|-------------|----------|
| Full affine + matrix + skew | translate/scale/rotate/skew/matrix + multi-function chains → one CTM | ✓ |
| Affine, no matrix/skew | translate/scale/rotate only; reject matrix() + skew() | |
| Affine + matrix, no skew | translate/scale/rotate + matrix(), reject named skew() | |

**User's choice:** Full affine + matrix + skew
**Notes:** Implementation is uniform (matrix multiply) regardless of allowed set; matrix() already
expresses skew/reflection, so excluding skew() is arbitrary. PDF handles any CTM. Gate widens from
single-rotate to a whitespace-separated chain of known affine function tokens (fail-loud on unknown).

---

## Transform origin (pivot)

| Option | Description | Selected |
|--------|-------------|----------|
| Box-center only (= CSS default) | Keep Phase 14 pivot; no transform-origin parsing | ✓ |
| Support transform-origin | Parse keywords + length/% | |

**User's choice:** Box-center only
**Notes:** CSS default transform-origin is 50% 50% = box center, so common cases (watermark/badge)
are correct with no new property parsing. transform-origin deferred to a later phase on demand.

---

## radial-gradient shape + extent subset

| Option | Description | Selected |
|--------|-------------|----------|
| circle + ellipse, center/keyword pos, farthest-corner | ellipse via CTM scale on unit-circle shading; defer explicit sizes + full extent keywords | ✓ |
| circle-only (at center, farthest-corner) | minimal; would mis-render the ellipse-default form | |
| Full CSS radial | all shapes/extents/sizes/positions | |

**User's choice:** circle + ellipse, center/keyword position, farthest-corner default
**Notes:** Ellipse is mandatory — CSS default shape for `radial-gradient(a,b)` is ellipse; circle-only
would reject/mis-render the most common form. Ellipse = unit-circle ShadingType-3 + anisotropic CTM
scale. Explicit sizes + the four extent keywords deferred to template demand. conic/repeating stay
rejected.

---

## Claude's Discretion

- CTM composition math + matrix decomposition vs direct multiply.
- Radial `/Coords` (two-circle) + ellipse CTM derivation.
- Parser structure (mirror LinearGradientParser); whether to generalize RotationGroup → a shared
  affine carrier or extend it.

## Deferred Ideas

- transform-origin property (non-center origins).
- Explicit radial sizes + full four extent keywords.
- conic-gradient / repeating-* gradients (stay rejected).
- Flexbox 1D → Phase 16. CSS grid + JavaScript → permanently out of scope.
