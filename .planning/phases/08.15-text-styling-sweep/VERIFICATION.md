# Phase 8.15 — Text Styling Sweep (VERIFICATION)

> **Closed:** 2026-05-29
> **Branch:** `phase/08.15-text-styling-sweep` → merged develop
> **Predecessor:** Phase 8.14 (`e047f16` — G22/G23a-f FIXED)
> **Scope:** 2 alignment gaps (G23g + G23h). G23i + G23j closed during research as non-issues.

## Commits

| # | SHA | Wave | Subject |
|---|-----|------|---------|
| 1 | `cf0b548` | A | fix(08.15): UA text-align:center for th + table-cell text-align propagation (G23g+G23h) |

## Findings & fixes

### G23g — `<th>` content rendered left-aligned despite UA spec

**Root cause:** `BoxTreeBuilder.ResolveCssProperties` UA-defaults block added UA `font-weight: bold` for `<th>` in Phase 8.14 but never added UA `text-align: center`. HTML5 spec: `th { text-align: center }` is part of the UA stylesheet.

**Fix:** added UA `text-align: center` for `<th>` in UA-defaults block, behind the same fallback chain (author-level computed → class → inline → UA) so author overrides still win.

### G23h — `<td class="text-center">` content rendered left-aligned

**Root cause:** `TableLayoutEngine.CellContext` factory built a child `LayoutContext` for each cell but never copied `cell.TextAlign` into it. Inline layout downstream read `ctx.TextAlign` (= parent default "left") instead of the cell's resolved alignment. Cells with `text-align: center` cascaded correctly into the `TableCellBox` but the alignment died at the cell→content boundary.

**Fix:** `CellContext` factory signature now accepts `TableCellBox? cell = null` and seeds `TextAlign = cell?.TextAlign ?? parent.TextAlign`. All 4 call sites updated to pass the cell reference.

### G23i + G23j — closed during research

- **G23i** (`13px` font reported "too big"): `Units.PxToPt = 0.75f` is CSS-spec correct (13px → 9.75pt). No fix needed.
- **G23j** (broader font-style support): italic/oblique, font-weight numeric, underline, line-through all already supported. `overline` silently ignored but unused in corpus. `font-variant`, `letter-spacing`, `word-spacing` unimplemented but used in zero templates. Deferred — no real exposure.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | `<th>` cells UA-default center-aligned when author does not override | PASS — `cf0b548` + 3 unit tests |
| SC2 | `<td class="text-center">` content rendered centered | PASS — `cf0b548` + 3 unit tests |
| SC3 | Author-level `text-align` overrides UA default (no regression) | PASS — covered by existing alignment tests |
| SC4 | All prior 436 tests pass + new tests | PASS — 441/441 |
| SC5 | CHNG_E visually matches Chrome reference (TH row centered, TD `text-center` data row centered) | PASS — verified post-Wave-A rasterization |

## Files changed

- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — UA `text-align: center` for `<th>` (G23g)
- `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` — `CellContext` propagates cell `TextAlign` (G23h)
- `tests/Muonroi.Pdf.Tests/Layout/CellTextAlignmentTests.cs` (new, 6 tests)

## Lessons learned

- **UA stylesheet completeness is incremental.** Phase 8.13 added `h1-h6` bold; 8.14 added `th` bold; 8.15 added `th` center. The UA-defaults block accretes one rule per real-world failure. Future work: ingest the full HTML5 §15 UA stylesheet table rather than chasing single rules.
- **Cell-boundary propagation is the canonical "cascade gap" pattern.** Same family as Phase 8.13 G19/G21 (float `WidthRaw`) and Phase 8.14 G23b (cell `WidthRaw` double-application). Any value that resolves at the parent layer but is read again at the child layer must be explicitly forwarded through the context factory.

## References

- `.planning/phase-08.15/PLAN.md`
- `.planning/phase-08.15/RESEARCH-G23g.md`
- `.planning/phase-08.15/RESEARCH-G23h.md`
- `.planning/phase-08.15/RESEARCH-G23i.md`
- `.planning/phase-08.15/RESEARCH-G23j.md`
- `.planning/GAPS-AND-DEBT.md`
- HTML5 §15.3.6 — table UA stylesheet
