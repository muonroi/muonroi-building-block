# Phase 8.9 — Visual Fidelity Primitives (PLAN)

> **Date:** 2026-05-28
> **Branch:** `phase/08.9-visual-fidelity-primitives` (to be created off `develop` after 8.8 merges)
> **Predecessor:** Phase 8.8 (HSLA_E float-child fix; 18/18 visual gate achieved)
> **Goal:** Form-style templates structurally match reference fill PDFs — table grid lines, form
> control glyphs, and form field underlines all render correctly.

> **Note:** This phase requires its own RESEARCH.md before execution. The three waves are
> independent and can be dispatched in parallel once RESEARCH is complete.

## Inputs

- Reference fill PDFs: `C:\Users\phila\Downloads\05-27-2026-22-53-24_files_list`
- Real templates: `D:\Data\Template\Htmls\PreviewRegistion`
- Current real-template renders: `tests/Muonroi.Pdf.Tests/TestResults/visual/*.pdf`

## Goals (G3 / G4 / G5)

- **G3** — `border-collapse:collapse` table cells draw all cell boundary grid lines
- **G4** — `<input type="checkbox">` and `<input type="radio">` render as glyphs (square + X or
  checkmark when `checked`), not stray text fragments
- **G5** — `<input type="text">` and similar text inputs render with a `border-bottom` underline
  at the baseline with placeholder/value text above

---

## Wave 8.9a — Table Grid Lines (G3)

**Problem:** `border-collapse:collapse` was allowed (LegacyPrintPolicy) and `vertical-align` in
table cells was fixed in Phase 8.7 (08.7-04-PLAN.md), but cell boundary _lines_ are not drawn.
The Wave 7 fix covered background/border for block elements (stroke-not-fill) but likely did not
extend to `TableCellBox` dispatch in `OwnedPdfWriter`.

**Scope (~80 LOC):**
- `OwnedPdfWriter` — add `DrawTableCellBorder` path for `TableCellBox` items in the
  `PositionedElement` dispatch loop. Respect `border-collapse:collapse` to draw shared edges once
  (pick inner/outer priority per CSS 2.1 §17.6.2).
- `TableLayoutEngine` — verify cell boundary rects are already present in `PositionedPage`; add
  if missing.

**Tests:** Re-baseline goldens for any template using `border-collapse:collapse`. Assert grid
lines are visible in rasterized PNGs.

**Commit:** `fix(08.9): draw table cell border lines for border-collapse:collapse (wave 8.9a)`

---

## Wave 8.9b — Checkbox / Radio Glyphs (G4)

**Problem:** `<input type="checkbox">` and `<input type="radio">` currently produce stray X text
fragments (the `✗` or `×` character from the element's text content falls through inline dispatch
without a dedicated node path).

**Scope (~120 LOC):**
- `BoxTreeBuilder` — detect `<input type="checkbox">` / `<input type="radio">` and emit a new
  `InputControlBox` node instead of falling through to inline text. Carry `checked` attribute.
- `InputControlBox` — new box type with `Side` (~10pt square), `IsChecked` bool, `InputKind`
  enum (Checkbox / Radio).
- `BlockLayoutEngine` / `InlineLayoutEngine` — handle `InputControlBox` in dispatch; reserve
  10pt × 10pt block space.
- `OwnedPdfWriter` — draw the control: outer square (stroke), inner X (two diagonal lines) or ✓
  glyph for checked state; circle for radio.

**Tests:** New golden test: checkbox checked/unchecked renders correct glyph at input position.
Existing stray-X regression: assert the broken fragment no longer appears.

**Commit:** `feat(08.9): InputControlBox — checkbox/radio glyph rendering (wave 8.9b)`

---

## Wave 8.9c — Form Field Underline (G5)

**Problem:** `<input type="text">` (and similar) render invisibly or as broken inline text.
Real templates use these as underlined blank fields (a horizontal rule at baseline = print line).

**Scope (~50 LOC):**
- `BoxTreeBuilder` — detect `<input type="text">` (and `type="date"`, `type="number"` etc.) and
  emit an `InputFieldBox` node with computed width, value/placeholder text, and resolved
  `border-bottom` style.
- `InputFieldBox` — new box type; carries text, width, line style.
- `OwnedPdfWriter` — draw a horizontal line at baseline (1pt rule) + optional placeholder text
  above in light gray.

**Tests:** New golden test: text input renders with underline at correct Y. Verify no stray
inline text artifact.

**Commit:** `feat(08.9): InputFieldBox — text input border-bottom underline (wave 8.9c)`

---

## Success criteria

- **SC1:** All real templates that declare `border-collapse:collapse` show table grid lines in
  rasterized output.
- **SC2:** Checkboxes and radio buttons render as glyphs at the input position; stray `×`/`✗`
  text fragments are gone.
- **SC3:** Text input fields render with a bottom underline; no invisible/broken inline fragment.
- **SC4:** All 7026+ unit tests pass (plus new tests from this phase).
- **SC5:** No regression on previously-passing real templates — compare rasterized PNGs of
  HSLA_F, HBL, CAPR_E pre/post; zero unexpected diff.

## Out of scope

- `<select>`, `<button>`, `<textarea>` styling (basic input/checkbox/radio only for v1)
- Form submission behavior (irrelevant — print PDFs)
- `<fieldset>` / `<legend>` native styling
- Advanced CSS `:checked` pseudo-class selector (use only `checked` HTML attribute)
- Float algorithm refactor → Phase 8.10

## References

- `../phase-08.7/RESEARCH-LAYOUT.md` (Phase 8.7 layout bug catalog)
- CSS 2.1 §17.6.2 https://www.w3.org/TR/CSS21/tables.html#border-conflict-resolution
- CSS 2.1 §9.5 https://www.w3.org/TR/CSS21/visuren.html#floats
- Reference fill PDFs: `C:\Users\phila\Downloads\05-27-2026-22-53-24_files_list`
- Template audit (which of the 18 templates exercise G3/G4/G5): TBD in RESEARCH.md
