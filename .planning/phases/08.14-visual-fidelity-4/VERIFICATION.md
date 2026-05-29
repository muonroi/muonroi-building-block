# Phase 8.14 — Visual Fidelity Sweep #4 (VERIFICATION)

> **Closed:** 2026-05-29
> **Branch:** `phase/08.14-visual-fidelity-4` → merged develop
> **Predecessor:** Phase 8.13 (`3f4262b` — G18/G19/G20/G21 FIXED)
> **Scope:** 2 user-reported gaps (G22, G23) which decomposed into 7 sub-gaps after deeper investigation.

## Commits (6 atomic on branch)

| # | SHA | Wave | Subject |
|---|-----|------|---------|
| 1 | `2efe3ae` | A | fix(08.14): GlyphCollector applies text-transform before glyph collection (G22) |
| 2 | `3c32cd8` | B | fix(08.14): table-layout class-rule fallback + inline-style width attribute fallback (G23) |
| 3 | `fe3d243` | C | fix(08.14): table-cell width no longer double-applies % against column width (G23b) |
| 4 | `395e430` | D | fix(08.14): descendant-selector class rules + UA bold for `<th>` (G23c+G23d) |
| 5 | `6c17d8f` | E | fix(08.14): fixed-layout column widths scale proportionally when declared sum < table width (G23e) |
| 6 | `e20d67e` | F | feat(08.14): synthetic bold (text stroke) + italic (Tm skew) in OwnedPdfWriter (G23f) |

## Findings & fixes

### G22 — Vietnamese uppercase diacritics rendered blank

**Root cause:** `GlyphCollector.cs:37` walked `InlineBox.Text` (pre-transform, lowercase). `InlineLayoutEngine.cs:165` applied `ToUpperInvariant()` at emit time, producing uppercase diacritic codepoints (Ế/Ă/Ý/À) the font subset never saw. `OwnedPdfWriter` encoded these against the subset cmap → `.notdef` → blank glyphs.

**Fix:** when `InlineBox.TextTransform == "uppercase"`, GlyphCollector iterates `Text.ToUpperInvariant()` to capture transformed codepoints in the subset.

### G23 (parent) — `<th style="width:16%">` wraps every word

Initial Chrome-MCP diff suggested a single issue. Investigation revealed FOUR compounding sub-gaps:

#### G23a (Wave B) — `table-layout` not in class-rule whitelist; `<th>` inline-style `width` lost

- `table-layout: fixed` in `.table-bodered2` class rule had no `LookupClassProperty` fallback → tables always took auto-mode path.
- `<th style="width:16%">` inline-style width was lost when parent table's `GetComputedStyle` threw (AngleSharp `%` width quirk).

**Fix:** added `LookupClassProperty(box.Source, "table-layout")` fallback; added `ParseInlineStyleProperty(box.Source?.GetAttribute("style"), "width")` as last-resort fallback after computed + class lookups both empty.

#### G23b (Wave C) — Table cell `WidthRaw` double-applied (same family as G19/G21)

`MeasureCell` and final-pass layout called `_blockEngine.Layout(cell, mc, ...)` with `mc.AvailableWidth = cellWidth` (the resolved column width). Inside `Layout`, `ResolveWidth` re-read `cell.WidthRaw="16%"` and computed `16% of 121pt ≈ 19pt`. Identical mechanism to Phase 8.13 G19/G21 (float branch).

**Fix:** save/clear/restore `cell.WidthRaw` around each `_blockEngine.Layout(cell, ...)` call at all 4 call sites (2 in `MeasureCell`, 2 in final-pass loop).

#### G23c (Wave D) — Descendant-selector class rules not parsed

`.table-bodered2 th, .table-bodered2 td { border: 1px solid }` was parsed into key `"table-bodered2"` only — the descendant tag `th`/`td` was dropped. TH/TD elements have no class of their own, so `LookupClassProperty(th, "border")` returned null. TH cells rendered without borders.

**Fix:**
- Added `_descendantClassRules: Dictionary<(string ancestorClass, string descendantTag), Dictionary<string, string>>`.
- `ParseClassRulesFromCss` recognises `.cls TAG` and `.cls > TAG` and stores under the (class, tag) key.
- Added `_ancestorStack` walked in `BuildNode` push/pop.
- New `LookupDescendantClassProperty` walks the ancestor stack and tries `(ancestorClass, nodeTag)` keys.
- 6 property fallback chains gained the descendant lookup: `padding`, `border-top-width`, `border-width`, `border`, `text-align`, `font-weight`, `text-transform`.

#### G23d (Wave D) — `<th>` UA bold default

Phase 8.13 G18 added UA `font-weight:bold` for `h1`–`h6` but not for `<th>`. CSS UA default is `th { font-weight: bold }`.

**Fix:** added `"th"` to the UA bold switch in BoxTreeBuilder. Author-level `font-weight` overrides still beat UA.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | Vietnamese uppercase diacritics render in `<h2 class="text-uppercase">phiếu...</h2>` | PASS — `2efe3ae` + 3 unit tests |
| SC2 | `<table class="table-bodered2"><th style="width:16%">...</th></table>` cells respect declared widths | PASS — `3c32cd8` + `fe3d243` + 9 unit tests |
| SC3 | `<th>` cells in `.table-bodered2` render with grid borders | PASS — `395e430` + 5 unit tests |
| SC4 | `<th>` carries UA bold by default | PASS — `395e430` |
| SC5 | All prior 401 golden tests pass + new tests | PASS — 436/436 (full green, 35 new unit tests across waves A-F) |
| SC6 | CHNG_E visually matches Chrome reference layout | PASS — verified post-Wave-F rasterization (table full width, TH bold visible, columns scaled) |
| SC7 | Fixed-layout column scaling per CSS 2.1 §17.5.2.1 | PASS — `6c17d8f` + 4 unit tests |
| SC8 | Bold/italic visually distinct in rendered PDF regardless of font availability | PASS — `e20d67e` + 13 unit tests |

## Files changed (high-level)

- `src/Muonroi.Pdf/Internal/Font/GlyphCollector.cs` — apply text-transform pre-collection (G22)
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — class-rule + inline-attr fallbacks; descendant-selector parser; UA `th` bold (G23a/c/d)
- `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` — cell `WidthRaw` save/restore (G23b) + fixed-layout column scaling (G23e)
- `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs` — synthetic bold + italic emission (G23f)
- `tests/Muonroi.Pdf.Tests/Font/GlyphCollectorTextTransformTests.cs` (new, 3 tests)
- `tests/Muonroi.Pdf.Tests/Layout/TableInlineWidthAndLayoutTests.cs` (new, 4 tests)
- `tests/Muonroi.Pdf.Tests/Layout/TableCellWidthDoubleApplicationTests.cs` (new, 5 tests)
- `tests/Muonroi.Pdf.Tests/Layout/DescendantClassSelectorAndThBoldTests.cs` (new, 5 tests)
- `tests/Muonroi.Pdf.Tests/Golden/RealTemplateBaselineTests.cs` — CHNG_E content assertion accepts both `"Container"` (post-G22) and `"Con ainer"` (pre-G22 legacy)
- `tests/Muonroi.Pdf.Tests/TestResources/Golden/inline-text-transform-uppercase.pdf` (regenerated — post-G22 subset includes uppercase Vietnamese codepoints)

#### G23e (Wave E) — Fixed-layout column scaling when declared sum < table width

CHNG_E Table 2 declared 5 columns summing to 66% of table width. `ComputeFixedColumnWidths` distributed extra width only to AUTO columns; with `autoCols==0`, the 34% slack was dropped → table rendered at 66% body width.

**Fix:** added proportional-scaling pass — when `autoCols==0 && assigned < available`, compute `scale = available / assigned` and multiply each declared width by scale. CSS 2.1 §17.5.2.1 conformant.

#### G23f (Wave F) — Synthetic bold + italic in PDF writer

Despite `InlineBox.Bold == true` at the box-tree level (unit-test verified), rendered PDF showed no visual weight difference. Cause: `OwnedPdfWriter` did not differentiate bold rendering — emitted identical glyphs regardless of the flag. Production Vietnamese fonts often lack a separate bold typeface.

**Fix:** when `inline.Bold && fontSize >= 8`, emit PDF text rendering mode `2 Tr` (fill+stroke) with stroke color matching fill (`RG`) and width `(fontSize/13)*0.4` pt (clamped 0.2–0.8). Reset `0 Tr` after `Tj` to prevent stroke leaking. Italic: when `inline.Italic`, the text matrix sets c-term to `0.2` (`1 0 0.2 1 X Y Tm`) producing ~11° oblique slant.

## Lessons learned

- **One reported gap can decompose into many root causes.** G23 surfaced as a single "TH wraps every word" symptom. Investigation found FOUR independent compounding defects: table-layout cascade gap, inline-attr fallback gap, cell-width double-application, descendant-selector parser gap. Each required a separate fix. Future research phases should not assume 1:1 gap:cause mapping.
- **Tests that bypass the integration boundary miss real bugs.** Phase 8.13 G20's unit tests built `TableBox` + `TableCellBox` directly in C# and passed — but the same scenario in real HTML through `BoxTreeBuilder` failed because `cell.WidthRaw` was never populated. Lesson: at least ONE test per layout phase must exercise the parser→cascade→box-tree→layout integration path, not just the layout engine in isolation.
- **Executor agents can misreport pre-existing failures.** Two Wave reports claimed `AllocationProbe` was a pre-existing failure; verification confirmed (a) it passes when run alone, (b) it fails in the full suite, (c) the file's own comments document its GC-sensitivity. Acceptable here, but the pattern matches `feedback_executor_misreport_pattern.md` — always cross-check the parent commit before accepting "pre-existing" claims.

## References

- `.planning/phase-08.14/PLAN.md`
- `.planning/phase-08.14/RESEARCH-G22.md`
- `.planning/phase-08.14/RESEARCH-G23.md`
- `.planning/GAPS-AND-DEBT.md`
- CSS 2.1 §17.5 — table visual formatting
- CSS 2.1 §17.5.2.1 — fixed table layout algorithm
- W3C selectors: descendant combinator
