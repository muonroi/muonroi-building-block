# Phase 8.13 — Visual Fidelity Sweep #3 (VERIFICATION)

> **Closed:** 2026-05-29
> **Branch:** `phase/08.13-visual-fidelity-3` → merged develop
> **Predecessor:** Phase 8.12b (`d90f8ef` — G14/G15/G15b/G16/G17 FIXED)
> **Scope:** 4 engine gaps (G18/G19/G20/G21) surfaced from Chrome-MCP visual diff of CHNG_E post-G15b.

## Commits (3 atomic on branch)

| # | SHA | Subject |
|---|-----|---------|
| 1 | `1ab3b8b` | fix(08.13): float inner-width no longer double-applies % against narrowed context (G19+G21) |
| 2 | `34c143f` | fix(08.13): h1-h6 default bold + text-transform inherits to inline children (G18) |
| 3 | `a3ddb09` | fix(08.13): table column solver honors cell width:% in auto + fixed modes (G20) |

## Findings & fixes

### G19 + G21 — Float inner-width double-application

**Root cause:** `DispatchLayout`'s float branch resolves `floatWidth = ResolveWidth(floatBlock, ctx)` correctly (e.g. `30% of 538pt ≈ 161pt`). It then calls `Layout(floatBlock, measureCtx, ...)` with `measureCtx.AvailableWidth = floatWidth`. Inside `Layout`, the first line calls `ResolveWidth(box, context)` AGAIN with `box.WidthRaw = "30%"` still set, but now against the narrowed `measureCtx.AvailableWidth = 161pt`. Result: `30% of 161pt ≈ 48pt` (i.e. `9%` of the page). Float children get 48pt available width — short lines wrap.

**Fix:** in `BlockLayoutEngine.DispatchLayout`'s float branch, after `floatWidth = ResolveWidth(floatBlock, ctx)`, set `floatBlock.Width = floatWidth` AND `floatBlock.WidthRaw = null` before calling `Layout`. The inner `ResolveWidth` hits the explicit-Width path and returns `161pt` directly, no re-percentage-application.

### G18 — `<h1>`-`<h6>` not bold + `text-transform:uppercase` not applied

**Root cause:**
1. `ResolveCssProperties` read `font-weight` and `text-transform` only inside `if (box is InlineBox inline)` — block boxes (including all `<hN>`) never set these.
2. `LookupClassProperty` class-rule whitelist excluded `text-transform`.
3. `BoxNode` had no field for these — they lived only on `InlineBox`.
4. No CSS inheritance pass propagated them from block parents to inline children.

**Fix (4 layered):**
1. Promoted `Bold`/`TextTransform` to `BoxNode` (removed `InlineBox` shadowing properties).
2. `ResolveCssProperties` reads them for ALL boxes (block + inline + replaced).
3. UA stylesheet fallback in `BoxTreeBuilder.CreateBox` sets `Bold=true` for `h1..h6` when no author `font-weight` is present.
4. New `PropagateInheritedTextProps` walks block subtree post-`BuildChildren` and copies parent `Bold`/`TextTransform` to `InlineBox` descendants that have defaults. Text runs apply `ToUpperInvariant()` at emit time when `TextTransform == "uppercase"`.

`text-transform` added to `LookupClassProperty` whitelist.

One golden regenerated: `inline-text-transform-uppercase.pdf` — the old golden captured the bug (uppercase NOT applied through block→inline propagation); new golden is correct.

### G20 — `<th style="width:16%">` ignored by column solver

**Root cause:** `cell.WidthRaw = "16%"` and `cell.Width = -1f` (the % sentinel) are correctly populated by the G15 cascade fallback. But:
- `ComputeAutoColumnWidths` only measured `ContentWidths` from text — never consulted `cell.WidthRaw` or `cell.Width`.
- `ComputeFixedColumnWidths` checked `cell.Width > 0f` which fails for the `-1f` sentinel.

Result: column width was `min-content` of the longest word, way less than declared.

**Fix:**
1. `ComputeAutoColumnWidths` — after `ContentWidths`, parse `cell.WidthRaw` `%` via new `TryParsePercent`. If present, set min/preferred = `Math.Max(content, tableWidth * pct / 100)`. CSS 2.1 §17.5.2 conformant — declared `%` acts as floor, never overrides longer content.
2. `ComputeFixedColumnWidths` — parallel branch: when `cell.Width <= 0f` but `WidthRaw` is `%`, resolve against `tableWidth`.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | `<h2>` renders bold + `text-uppercase` class applies | PASS — `34c143f` + 5 unit tests |
| SC2 | `<p>label: <strong>value</strong></p>` inline on single line inside `w-30.float-left` | PASS — `1ab3b8b` + 4 unit tests |
| SC3 | `<th style="width:16%">` respects declared width, content fits | PASS — `a3ddb09` + 4 unit tests |
| SC4 | All prior 388 goldens pass + 13 new unit tests | PASS — 401/401 |
| SC5 | CHNG_E visual diff vs Chrome reference: 4 prior gaps gone | PASS — visually verified post-Wave-C raster |

## Files changed (high-level)

- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` — float inner-width fix (G19+G21)
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — universal font-weight/text-transform read; UA h1-h6 bold; class-rule whitelist; `PropagateInheritedTextProps`
- `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` — added `Bold` + `TextTransform`
- `src/Muonroi.Pdf/Internal/Layout/Boxes/InlineBox.cs` — removed shadowing of inherited fields
- `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` — `TryParsePercent` + `%` honored in both auto + fixed column solvers
- `tests/Muonroi.Pdf.Tests/Layout/FloatInnerWidthDoubleApplicationTests.cs` (new, 4 tests)
- `tests/Muonroi.Pdf.Tests/Layout/HeadingBoldAndTextTransformTests.cs` (new, 5 tests)
- `tests/Muonroi.Pdf.Tests/Layout/TableCellPercentWidthTests.cs` (new, 4 tests)
- `tests/Muonroi.Pdf.Tests/TestResources/Golden/inline-text-transform-uppercase.pdf` (regenerated — old golden captured the bug)

## Out of scope (deferred)

- Other `_E` templates (CAPR_E, CRCD_E, CSLA_E, HANG_E, NHAR_E) — should now inherit all Phase 8.13 fixes; visual audit deferred to demand-driven.
- Test harness `{{logo}}` real-PNG stub (#33) — still cosmetic only.
- G6 inline `vertical-align` edge cases — still rare, demand-driven.
- TD7 cell-width rounding — still deferred (needs golden re-baseline pass).
- Full CSS inheritance pass for ALL inherited properties (color, line-height, etc.) — `Bold` and `TextTransform` only; broader inheritance is out of v1 scope.
- C4 unsupported-CSS error path — product decision pending.

## Lessons learned

- **% widths inside floats need a "resolved" gate.** The float branch resolves the float's own width against its parent context, but the inner `Layout` call must NOT re-apply the % against the just-resolved context. Pattern: when an outer pass has resolved a dimension, set the explicit value AND clear the raw string so the inner pass treats it as fully-resolved.
- **Property-read gates must be type-symmetric.** G18 happened because `font-weight` was read only for one box type. When a property is universally inherited (CSS-wise), its read must be box-type-independent. Audit other inherited properties at next visual sweep.
- **Goldens can encode bugs.** `inline-text-transform-uppercase.pdf` was a golden that captured the pre-G18 behavior. Always cross-check golden updates against a visual reference (Chrome MCP) rather than auto-accept byte-diffs.

## References

- `.planning/phase-08.13/PLAN.md`
- `.planning/phase-08.13/RESEARCH-G18.md`
- `.planning/phase-08.13/RESEARCH-G19.md`
- `.planning/phase-08.13/RESEARCH-G20.md`
- `.planning/phase-08.13/RESEARCH-G21.md`
- `.planning/GAPS-AND-DEBT.md`
- CSS 2.1 §10.3.5 — floating, non-replaced widths
- CSS 2.1 §17.5.2 — auto-table column-width algorithm
