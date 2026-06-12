# Phase 8.8 — Float Child Rendering (PLAN)

> **Date:** 2026-05-28
> **Branch:** `phase/08.8-float-child-rendering` (to be created off `develop`)
> **Predecessor:** Phase 8.7 closed at 94% visual gate (commit `93b04c4` on develop)
> **Goal:** 18/18 real-template visual gate + logo image visible in float children (HSLA_E)

## Inputs (research already done)

- `RESEARCH-HSLA-E.md` — confirmed root cause: float-child X origin uses `ctx.PageMarginLeftPt`
  instead of `floatX + paddingLeft` (Fix A2 pattern not applied to floats in Wave 8c).

## Scope

Two gap items — same root cause (`ContentOriginX` not propagated into float-child dispatch):

- **G1 — Text / HR / block inside float uses correct X origin**
  `BlockLayoutEngine.DispatchLayout`: when recursing into a float child, build a derived
  `LayoutContext` with `ContentOriginX = floatX + floatBlock.PaddingLeft + floatBlock.BorderLeft`.
  Reset float accumulators in that child context (float establishes its own BFC per CSS 2.1 §9.4.1).
  Apply symmetrically to left + right float branches.
  Also audit `HrBox` dispatch site: replace hardcoded `ctx.PageMarginLeftPt` with
  `xOrigin = ctx.ContentOriginX > 0f ? ctx.ContentOriginX : ctx.PageMarginLeftPt`.

- **G2 — Image inside float renders at correct X origin**
  Audit `ImageBox` dispatch site(s) for the same `ctx.PageMarginLeftPt` hardcode pattern.
  Fix any instance that does not respect `ctx.ContentOriginX`. Verify HSLA_F logo image is
  visible and positioned correctly inside its float column.

## Out of scope

- Table border-collapse grid lines (G3) → Phase 8.9
- Checkbox / radio glyphs (G4) → Phase 8.9
- Form field underline (G5) → Phase 8.9
- ExcludedShapes / float algorithm refactor → Phase 8.10
- Vertical-align edge, nested BFC stacks, position:absolute × float, page-break-inside floats,
  shrink-to-fit auto float → Phase 8.11

## Wave structure

Single wave — 1 commit.

**Files to change:**
- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs`
  — float child context with `ContentOriginX` (G1)
  — `HrBox` dispatch X origin fix (G1)
  — `ImageBox` dispatch X origin fix if hardcoded (G2)

**Estimated LOC:** ~30.

**Commit:** `fix(08.8): float-child ContentOriginX propagation — G1 text/HR + G2 image (wave 8.8a)`

## Success criteria

- **SC1:** HSLA_E renders 3-column header (logo / title-block / barcode) + customer section + table + footer. Non-empty content, no blank page.
- **SC2:** HSLA_F logo image (float column) visible and correctly positioned — no X=0 offset bleed.
- **SC3:** All 17 previously-passing templates remain visually unchanged (regression).
- **SC4:** `dotnet test` all green (target ≥7026 unit + 18 real-template baseline tests).
- **SC5:** Goldens re-baselined where HSLA_E was previously empty; ≤3 other goldens shift (only those that exercise floats with HRs or images inside).

## References

- `RESEARCH-HSLA-E.md` (root cause + fix)
- `../phase-08.7/RESEARCH-LAYOUT.md` (Phase 8.7 layout bug catalog)
- CSS 2.1 §9.5 https://www.w3.org/TR/CSS21/visuren.html#floats
- CSS 2.1 §9.4.1 BFC https://www.w3.org/TR/CSS21/visuren.html#block-formatting
