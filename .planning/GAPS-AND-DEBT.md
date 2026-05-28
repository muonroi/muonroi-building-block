# Gaps & Tech Debt — Muonroi.Pdf (cross-phase)

> Updated: 2026-05-28 after Phase 8.11 close (Image Rendering Sweep).
> Purpose: prevent silent accumulation of unresolved debt. Every gap/debt
> item has a source phase, current status, and assigned next phase.

See also: `.planning/ROADMAP.md` for phase timeline.

---

## Visual fidelity gaps (G-series)

| ID | Gap | Source | Status | Owner phase |
|----|-----|--------|--------|-------------|
| G1 | Float child text/HR/block X origin | 8.7 | FIXED 8.8 (a5448de) | — |
| G2 | Image/table in float X origin | 8.7 | FIXED 8.8 (a5448de) | — |
| G3 | Table `border-collapse:collapse` grid lines not drawn | 8.7 | FIXED 8.9 (`2ca4830`) | — |
| G4 | `<input type=checkbox>` / `<input type=radio>` glyph render | 8.7 | DEFERRED (0 templates use; revisit on demand) | 8.11 |
| G5 | `<input type=text>` border-bottom underline | 8.7 | DEFERRED (0 templates use) | 8.11 |
| G6 | `vertical-align` edge cases (multi-line cell, mixed inline) | 8.7 | OPEN (rare) | 8.11 |
| G7 | `<span>`/`<label>` inline default — empty display string | 8.7 | FIXED 8.9 (`0542d76`) | — |
| G7b | Block element with mixed text + inline element children — text node dropped by `CollectChildren`; siblings not batched in dispatch | 8.9 (discovered post-G7) | FIXED 8.9 (`df229b8`) | — |
| G8 | HSLA_E content on page 2, page 1 empty (body `height:148mm`) | 8.8 | FIXED 8.9 (`0b5ca9b`) | — |
| G9 | Image inside float renders as colored placeholder (HBND_F top-left red rect) — root cause: abs-pos `<img>` inside `overflow:hidden` div fell back to page (0,0) because containing-block gate matched only `position:relative` | 8.9 (discovered) | FIXED 8.11 (`5663bae`) — extended `isContainingBlock` to cover overflow:hidden + TableCellBox.ContainingBlockRect propagation | — |

---

## Tech debt (TD-series)

| ID | Debt | Source | Risk | Owner phase |
|----|------|--------|------|-------------|
| TD1 | `HslaERootCauseDiagnostic.cs` committed without `[Skip]` — runs on every CI build | 8.8 | LOW (fast) but pollutes signal | 8.9 — add `[Skip]` OR repurpose as permanent assertion |
| TD2 | Cursor-based float positioning (`LeftFloatRight` etc.) — fragile for nested BFC, `position:absolute` | 8.7 | MED (foundation for 8.11) | FIXED 8.10 (`289a11f`) — ExcludedShapes |
| TD3 | Float does not consistently establish its own BFC — `bfcRoot = isRoot \|\| IsBfcRoot(box)` in `BlockLayoutEngine.Layout` does not detect float boxes | 8.7 | LOW for legacy print templates | FIXED 8.11 (`df143e9`) — `IsBfcRoot` now returns true for `FloatValue != null && != "none"` |
| TD4 | Float context propagation across nested containing blocks not fully verified | 8.7 | MED | FIXED 8.10 (`807e050`) — shared Exclusions ref |
| TD5 | Right-float symmetric fix via `LeftFloatRight + ctx.AvailableWidth` math — works but coupled to cursor model | 8.7 | LOW | FIXED 8.10 (`2d61007`) — solver handles both sides uniformly |
| TD6 | `ContentOriginX > 0f` ad-hoc fallback check — fragile (`0` is technically valid). Should use `ContentOriginX.HasValue` or a sentinel | 8.8 | LOW | 8.9 or 8.10 |
| TD7 | `CellContext.AvailableWidth` compound rounding (RESEARCH-LAYOUT.md Bug 9) — never fully fixed in 8.7 | 8.7 | LOW | 8.12+ (8.11 attempt reverted — `MathF.Round(w,2)` broke 10 table goldens despite visual equivalence; needs golden re-baseline as dedicated cleanup) |
| TD8 | PNG decoder edge case for 1×1 PNG (12-byte IDAT) — `InvalidDataException`; worked around in test fixtures; engine path not hardened | 8.7 | LOW (test fixture only) | 8.9 |
| TD9 | `VisualRegressionTests` / `RealTemplateBaselineTests` rasterize page 1 only — multi-page templates can have page 1 visually empty without any test failing (uncovered G8) | 8.8 | HIGH (masking real bugs) | FIXED 8.9 (`e95db78`) — page count assertion added |
| TD10 | RESEARCH-LAYOUT.md Bug 7 (table cell content X) — fix landed in 8.7 wave 8a but symmetric `BlockBox` blockX origin still uses `LeftFloatRight \|\| PageMarginLeftPt`, not `ContentOriginX` | 8.7 | LOW | FIXED 8.10 (`2d61007`) — `AvailableWidthAtY(startY, 0f, cb, exclusions).StartX` |

---

## Charter / scope items pending

| ID | Item | Status | Owner phase |
|----|------|--------|-------------|
| C1 | 18/18 visual gate | DONE 8.9 — all 18 templates render on page 1 with table grid + inline label-value | — |
| C2 | Logo data-URI PNG render audit across all 18 templates | PARTIAL — G9 root cause closed (HBND_F + HSLA_F abs-pos position); full visual audit across 18 templates deferred | 8.12+ |
| C3 | Document v1 Legacy Print-HTML Profile public spec | Not started | After 8.11 stabilization |
| C4 | Failure mode: "unsupported: \<feature\>" error path for out-of-profile CSS | Not implemented (silent mis-render) | 8.11 or charter sub-phase |

---

## Research artifacts inventory

| File | Source phase | Status |
|------|-------------|--------|
| `.planning/phase-08.7/RESEARCH-LAYOUT.md` | 8.7 | RELEVANT — Bug 1–9 references active |
| `.planning/phase-08.7/RESEARCH-OSS-REFS.md` | 8.7 | RELEVANT — feeds 8.10 ExcludedShapes |
| `.planning/phase-08.8/RESEARCH-HSLA-E.md` | 8.8 | CLOSED (G1+G2 fixed) |
| `.planning/phase-08.9/` | 8.9 | READY for 8.9 execution |
| `.planning/phase-08.10/PLAN.md` | 8.10 | READY |

---

## How to use this file

- **Closing a phase**: update Status column for any items resolved; set fixed commit SHA.
- **Discovering a new gap/debt**: append to the relevant table; assign an owner phase.
- **Starting a phase**: filter table by `Owner phase = this-phase` for the work backlog.
- **Rule**: never leave an item as `OPEN` without an owner phase. Use 8.11 as catch-all only
  if scope is genuinely unclear.
