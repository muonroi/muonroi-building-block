# Phase 8.9 — Fidelity Primitives + Pagination + Inline Flow (PLAN, REVISED)

> **Date:** 2026-05-28 (revised post-research)
> **Branch:** `phase/08.9-fidelity` (off develop)
> **Predecessor:** Phase 8.8 closed (commit `72bb7e9` on develop)
> **Goal:** 18/18 **single-page** visual gate + table grid visible + label-value inline flow

## Inputs

- `RESEARCH-G7.md` — root cause `BoxTreeBuilder.cs:133` default `?? "block"` for empty `display`. SMALL scope.
- `RESEARCH-G8-PAGINATION.md` — root cause body `height:148mm`=419.527pt triggers PaginationEngine break. Fix ~10 LOC.
- `RESEARCH-G3-G4-G5.md` — G3 affects 10/17 templates; **G4 + G5 affect ZERO templates** (no `<input>` element in 17 HTML files).
- `RESEARCH-TD9-HARNESS.md` (output retained in research agent reply) — recommend Option B (page-count assertion), ~38 LOC, BEFORE G8.

## Scope revision (vs original split)

| ID | Original | Revised | Reason |
|----|----------|---------|--------|
| G3 (table border-collapse) | 8.9 | **8.9 KEEP** | 10/17 templates affected — biggest visual gap |
| G4 (`<input>` checkbox glyph) | 8.9 | **DEFER 8.11** | 0 templates use; pre-emptive without demand |
| G5 (`<input>` text underline) | 8.9 | **DEFER 8.11** | 0 templates use |
| G7 (inline display default) | 8.9 | **8.9 KEEP** | Affects label-value across many templates |
| G8 (HSLA_E pagination) | 8.9 | **8.9 KEEP** | Blocks single-page render of HSLA_E |
| TD9 (page-count assertion) | 8.9 | **8.9 KEEP** | Masking bug; regression guard |

**Net scope:** TD9 + G8 + G7 + G3. Estimated **~158 LOC** total (was ~290).

## Wave structure (4 atomic commits, sequential)

### Wave 8.9a — TD9 page count assertion (~38 LOC, regression guard FIRST)
- **Files:** `tests/Muonroi.Pdf.Tests/Golden/RealTemplateBaselineTests.cs`
- **What:** New `[Theory]` `RealTemplate_ExpectedPageCount` with `{ template → count }` dict. HSLA_E initially = 2 (current), all others = 1. Will flip HSLA_E → 1 after Wave 8.9b.
- **Commit:** `test(08.9): page count assertion for real templates (wave 8.9a / TD9)`
- **Risk:** LOW.

### Wave 8.9b — G8 body height pagination clamp (~10 LOC)
- **Files:** `src/Muonroi.Pdf/Internal/Layout/PaginationEngine.cs` OR `BlockLayoutEngine.cs` body dispatch
- **What:** When `IsBodyRoot && box.Height >= pageHeightPt`, treat body height as "non-paginating" — body rect kept for backgrounds/borders but pagination doesn't break for it. Confirm exact site per `RESEARCH-G8-PAGINATION.md`.
- **Update:** flip Wave 8.9a's `expectedPageCount[HSLA_E] = 2 → 1`.
- **Commit:** `fix(08.9): clamp body explicit height for pagination (wave 8.9b / G8)`
- **Risk:** LOW. Touches one branch in pagination logic.

### Wave 8.9c — G7 UA-inline display default (~30 LOC)
- **Files:** `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs:133`
- **What:** Switch on `display` when value is null/empty/unset, look up tag name in UA-inline list (`span`, `label`, `strong`, `em`, `b`, `i`, `u`, `a`, `code`, `kbd`, `mark`, `small`, `sub`, `sup`, `time`, `cite`, `abbr`, `q`, `var`, `samp`, `dfn`) → return InlineBox. Otherwise BlockBox.
- **Tests:** New `BoxTreeBuilderTests` cases for each UA-inline tag.
- **Commit:** `fix(08.9): UA-inline element display default — span/label/strong/etc (wave 8.9c / G7)`
- **Risk:** LOW. Local change in box construction.

### Wave 8.9d — G3 table border-collapse cell grid (~80 LOC)
- **Files:** `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs`, possibly `TableLayoutEngine.cs`
- **What:** When TableCellBox PositionedElement has `BorderTop/Right/Bottom/Left > 0`, emit PDF stroke commands at cell rect edges. Honor `border-collapse:collapse` — draw each shared edge once (pick inner/outer per CSS 2.1 §17.6.2 priority). Color from cell.BorderColor (test corpus = teal `#008080`).
- **Goldens:** 10 templates will re-baseline (BNTT, CAPR_E, CHNG_E, CRCD_E, CSLA_E, HANG_E, HANG_F, HBL, HSLA_E, NHAR_E).
- **Commit:** `fix(08.9): draw table cell borders with border-collapse support (wave 8.9d / G3)`
- **Risk:** MED. Touches PDF writer + may affect existing block-element border path.

## Out of scope (deferred)

- **G4 (`<input type=checkbox>`)**, **G5 (`<input type=text>`)** → 8.11. No template uses; revisit when real demand.
- **G6 vertical-align edge cases** → 8.11.
- **C2 logo image audit across all 18 templates** — post-8.9d sanity pass.
- Other items per `.planning/GAPS-AND-DEBT.md` already assigned.

## Success criteria

- **SC1:** HSLA_E renders on **page 1** (1-page PDF).
- **SC2:** 10 templates with `border-collapse:collapse` show visible teal grid lines.
- **SC3:** Label-value pairs (CAPR_E "Mã lô: LO12345", HSLA_E equivalents) on **same row**.
- **SC4:** `RealTemplate_ExpectedPageCount` theory passes for all 18 templates.
- **SC5:** `dotnet test` all green (335 baseline + new G7 tests + new TD9 theory).
- **SC6:** No regression on previously-working visuals (HSLA_F, HBND_F, HBL form structure).

## Execution order

1. Branch `phase/08.9-fidelity` off `develop`.
2. Wave 8.9a (TD9) → Wave 8.9b (G8) → Wave 8.9c (G7) → Wave 8.9d (G3). Sequential.
3. After each wave: `dotnet test`; commit only if green.
4. Close-out: VERIFICATION.md + update GAPS-AND-DEBT.md (mark G3/G7/G8/TD9 as FIXED) + update ROADMAP.
5. Merge `phase/08.9-fidelity` → develop with `--no-ff`.

## References

- `RESEARCH-G3-G4-G5.md`, `RESEARCH-G7.md`, `RESEARCH-G8-PAGINATION.md`
- `../GAPS-AND-DEBT.md`
- CSS 2.1 §17.6 border-collapse https://www.w3.org/TR/CSS21/tables.html#borders
- CSS 2.1 §9.2.4 display values https://www.w3.org/TR/CSS21/visuren.html#display-prop
- HTML5 §15.3 UA stylesheet inline elements
