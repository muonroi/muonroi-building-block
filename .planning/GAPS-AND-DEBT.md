# Gaps & Tech Debt — Muonroi.Pdf (cross-phase)

> Updated: 2026-05-28 after Phase 8.12 close (Visual Bug Sweep — lineHeight=0f latent fix).
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
| G10 | (reserved — investigate if surfaced) | — | — | — |
| G11 | HSLA_E / CAPR_E label-value appeared vertically stacked in initial visual review | 8.12 (discovered) | CLOSED 8.12 — NOT engine bug; HSLA_E is template-structure (label/value in two separate floats with barcode between), CAPR_E is correct inline but with vertical whitespace between distinct fields | — |
| G12 | Cell content overlap in rasterized PNG (HBL, CSLA_F) | 8.12 (discovered) | CLOSED 8.12 — not visible in PDF per user review; rasterization aliasing only | — |
| G13 | HBL equipment table column misalignment in rasterized PNG | 8.12 (discovered) | CLOSED 8.12 — same as G12; rasterization artifact only | — |
| G14 | `_E` template tables silently skip rendering — AngleSharp `GetComputedStyle` throws on `%` widths → `display` blank → `<table>` boxed as `BlockBox` → `CollectRows` finds 0 rows → zero-height table | 8.12b (Chrome-MCP visual diff) | FIXED 8.12b (`43d1661`) — HTML5 UA stylesheet display fallback in `BoxTreeBuilder.CreateBox` | — |
| G15 | Engine-wide AngleSharp throw eats `width`/`float`/`text-align`/`border` for any `%` width — every `.w-XX.float-left` lost both `float` and `width` | 8.12b (same throw as G14) | FIXED 8.12b (`58205d4`) — `AngleSharpStyledNode` catches `ArgumentException` and falls back to inline `el.GetStyle()`; `BoxTreeBuilder.ExtractClassRules` walks `<style>` blocks for class-rule lookup | — |
| G15b | 3rd float drops to next row when w-20+w-50+w-30 = 100% exactly — sub-pt FP rounding fails strict `>=` check | 8.12b (post-G15 visual verification) | FIXED 8.12b (`1a628c7`) — `FloatPlacementSolver` 0.5pt epsilon tolerance | — |
| G16 | Inline `<img style="height:Npx">` not honored — intrinsic `NaturalHeight` short-circuits CSS `Height` (4×4 stub PNG NaturalHeight ≈ 3pt overrides height:100px) | 8.12b (post-G14) | FIXED 8.12b (`58205d4`) — `ReplacedBox` priority swap: CSS Height first, NaturalHeight second | — |
| G17 | Table `border-collapse:collapse` borders incomplete — `<th>`/`<td>` percent-width and border declarations lost to same AngleSharp throw as G14/G15 | 8.12b (same throw as G14/G15) | FIXED 8.12b (`58205d4`) — class-rule lookup covers `border` declarations | — |
| G18 | `<h2>` not bold + `text-transform:uppercase` not applied — `font-weight`/`text-transform` read only in InlineBox branch; class-rule whitelist gap; no block→inline propagation | 8.12b (Chrome-MCP post-G15b) | FIXED 8.13 (`34c143f`) — `BoxNode` carries `Bold`/`TextTransform` for all box types; UA h1-h6 bold; `PropagateInheritedTextProps` post-`BuildChildren`; text-runs uppercase at emit | — |
| G19 | `<p>label: <strong>value</strong></p>` wraps in `div.w-30.float-left` — `WidthRaw="X%"` double-applied: `DispatchLayout` resolves float width then inner `Layout` re-runs `ResolveWidth` against the narrowed context | 8.12b (post-G15b visual diff) | FIXED 8.13 (`1ab3b8b`) — `DispatchLayout` float branch sets `Width = floatWidth` AND clears `WidthRaw` before inner `Layout` call | — |
| G20 | `<th style="width:16%">` wraps each word — `TableLayoutEngine.ComputeAutoColumnWidths` ignored `cell.WidthRaw`; `ComputeFixedColumnWidths` skipped `Width=-1f` sentinel | 8.12b (post-G15b visual diff) | FIXED 8.13 (`a3ddb09`) — `TryParsePercent` helper + `%` honored as floor in auto-mode (`max(content, tableWidth*pct/100)`) and resolved against tableWidth in fixed-mode | — |
| G21 | `<p>Số điện thoại: 0901234567</p>` wraps 3 lines in `div.w-25.float-left.text-center` — same root cause as G19 | 8.12b (post-G15b visual diff) | FIXED 8.13 (`1ab3b8b`) — same fix as G19 | — |
| G22 | `<h2 class="text-uppercase">phiếu</h2>` Vietnamese uppercase diacritics (Ế/Ă/Ý/À) render blank — `GlyphCollector` reads pre-transform `InlineBox.Text`, subset never sees uppercase codepoints | 8.13 (Chrome-MCP visual diff post-Phase-8.13) | FIXED 8.14 (`2efe3ae`) — `GlyphCollector` applies `ToUpperInvariant()` when `TextTransform=="uppercase"` | — |
| G23 | `<th style="width:16%">` in `.table-bodered2` wraps every word — decomposed into 5 sub-causes (a/b/c/d/e) | 8.13 (Chrome-MCP post-Phase-8.13) | FIXED 8.14 — see G23a-e rows | — |
| G23a | `table-layout: fixed` from class rule lost; `<th style="width:16%">` inline-style width lost when `GetComputedStyle` throws | 8.14 (research) | FIXED 8.14 (`3c32cd8`) — `LookupClassProperty("table-layout")` fallback + `ParseInlineStyleProperty` raw-attr fallback for `width` | — |
| G23b | Table-cell `WidthRaw` double-applied — `MeasureCell` → `Layout` → `ResolveWidth` re-runs % against column width; identical mechanism to G19/G21 (float branch) | 8.14 (research) | FIXED 8.14 (`fe3d243`) — save/clear/restore `cell.WidthRaw` around each `_blockEngine.Layout(cell,...)` call | — |
| G23c | `.table-bodered2 th, .table-bodered2 td { border:1px solid }` descendant selector ignored — parser stored under key `"table-bodered2"`, TH/TD elements have no such class, lookup fails → TH borders missing | 8.14 (research) | FIXED 8.14 (`395e430`) — `_descendantClassRules` keyed `(ancestorClass, descendantTag)` + `_ancestorStack` walked in `BuildNode` + `LookupDescendantClassProperty` fallback | — |
| G23d | `<th>` not UA-bold — Phase 8.13 G18 added UA bold for `h1`-`h6` only | 8.14 (research) | FIXED 8.14 (`395e430`) — `"th"` added to UA bold switch | — |
| G23e | Fixed-layout column widths with declared sum < 100% don't scale to fill table width — CSS 2.1 §17.5.2.1 violated, 34% slack dropped | 8.14 (post-Wave-D pixel review) | FIXED 8.14 (`6c17d8f`) — proportional-scaling pass in `ComputeFixedColumnWidths` when `autoCols==0 && assigned<available` | — |
| G23f | `InlineBox.Bold=true` not visually rendered when font lacks bold variant — writer emitted identical glyphs regardless | 8.14 (post-Wave-E visual review) | FIXED 8.14 (`e20d67e`) — synthetic bold via `2 Tr` + stroke and italic via `Tm` skew `1 0 0.2 1` in `OwnedPdfWriter` | — |
| G23g | `<th>` content rendered left-aligned despite HTML5 UA `th { text-align: center }` | 8.15 (research) | FIXED 8.15 (`cf0b548`) — UA `text-align: center` added to `<th>` UA-defaults block in `BoxTreeBuilder.ResolveCssProperties` | — |
| G23h | `<td class="text-center">` content left-aligned — `TableLayoutEngine.CellContext` never copied `cell.TextAlign` into child `LayoutContext` (cascade gap at cell boundary) | 8.15 (research) | FIXED 8.15 (`cf0b548`) — `CellContext` factory accepts `TableCellBox` and seeds `TextAlign = cell?.TextAlign ?? parent.TextAlign` | — |
| G23i | (closed — false alarm) `Units.PxToPt = 0.75f` correct per CSS spec (13px → 9.75pt) | 8.15 (research) | NO-FIX 8.15 — closed during research | — |
| G23j | (closed — no corpus exposure) overline silently ignored; `font-variant`/`letter-spacing`/`word-spacing` unimplemented but used in zero templates | 8.15 (research) | DEFERRED — track if future template uses any | — |
| G24 | `<img>` without CSS width/height stretches to container width instead of using intrinsic pixel size (px→pt) | 8.16 (post-8.15 logo render audit) | FIXED 8.16 (`1bc6a09`) — `BlockLayoutEngine.ResolveWidth` adds `ReplacedBox { NaturalWidth: > 0f }` branch before auto-width fallback; `NaturalWidth/Height` seeded by `BoxTreeBuilder` from `DecodedImage × Units.PxToPt` | — |
| G25 | 3-level descendant selector `.table-bodered2 tr.no-border td { border:none }` misfiled as a flat `.table-bodered2` rule (parser `IsBareSelectorTag` rejected the `tr.no-border td` remainder) → never reached the `<td>`, so base `.cls td {border:1px}` bordered every cell incl. `no-border` rows (TCIS HBCX "Số xe VC") | TCIS preview-registration render review | FIXED — `BoxTreeBuilder.TryResolveDescendantKey` keys multi-level descendant selectors on the class NEAREST the final tag (`(no-border, td)`); nearest-ancestor walk applies it before the outer table rule. Regression test `DescendantSelector_NoBorderRow_SuppressesBaseCellBorder`. 495/495 + 1 new green | — |
| G27 | `padding` shorthand fallback (`BoxTreeBuilder.cs`) read ONLY the first token, so `padding: 2px 6px` on a `%`-width table (computed-style-throw path) applied 2px to all four sides → horizontal padding could never exceed vertical; cell text sat almost flush against the cell border (TCIS HBCX "dính mép") | TCIS HBCX review | FIXED — shorthand expanded honouring 1/2/3/4-value CSS rules. Regression test `PaddingShorthand_TwoValue_AppliesVerticalAndHorizontalSeparately`. 497/497 green | — |
| G28 | `word-break`/`overflow-wrap` declared via a descendant selector (`.table-bodered2 td { word-break: break-word }`) was NOT applied to cells whose own class differs (e.g. `<td class="text-center">`) — `ResolveWordBreakWithFallback` used own-class `LookupClassProperty` only, missing `LookupDescendantClassProperty`. Long unbreakable cell values (`ONES_EAL12133`) overflowed the column, ran past the border, and overlapped the neighbouring column (TCIS HBCX data row) | TCIS HBCX data-row review | FIXED — added descendant-selector fallback in `ResolveWordBreakWithFallback`. Regression test `WordBreak_DescendantSelector_AppliesToCellWithDifferentOwnClass`. Full suite green | — |
| G29 | `white-space` was read from computed style only and only on inline boxes (never resolved on a cell, never propagated). On `%`-width tables (computed-style-throw) `white-space: nowrap`/`pre-line` declared on a cell (class, descendant `.table-bodered2 td`, or inline) was silently lost — author could not keep an identifier (SealNo/ContainerNo) on one line | TCIS HBCX per-column control request | FIXED — `WhiteSpace` moved to `BoxNode`; `ResolveWhiteSpaceWithFallback` (computed→own-class→descendant→inline-attr) on cells; propagated to inline children in `PropagateInheritedTextProps`. Regression test `WhiteSpace_DescendantSelector_NowrapResolvesAndPropagates`; one golden (`inline-whitespace-pre-line`) re-baselined (visually verified identical) | — |
| G26 | Inline per-edge border longhands (`<td style="border-left:1px ...">`) are NOT honored on cells of a `%`-width table: when `GetComputedStyle` throws (no viewport for `%`), the border fallback (`BoxTreeBuilder.cs:305`) reads ONLY class rules, never the inline `style` attribute, so intended outer-frame edges on `tr.no-border` rows render borderless | TCIS HBCX review (alongside G25) | OPEN — workaround: express edge borders via a class rule, or move the affected rows into a non-`%`-width table. Owner: 8.11 / fidelity sweep | 8.11 |

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
| C1 | 18/18 visual gate | DONE 8.9 — all 18 templates render on page 1 with table grid + inline label-value. VERIFIED again 8.12 via PDF review (G11/G12/G13 closed as not-engine-bugs) | — |
| C1b | Author guidance: floated siblings do NOT establish inline-flow continuity (see HSLA_E vs HSLA_F) | DOCUMENTED 8.12 (VERIFICATION.md) | — |
| C2 | Logo data-URI PNG render audit across all 18 templates | COMPLETE 8.16 (`cdbb526`) — 17/17 templates render OK, no new gaps; see `.planning/phases/08.16-image-polish/AUDIT.md` | — |
| C3 | Document v1 Legacy Print-HTML Profile public spec | COMPLETE 8.16 — see `PROFILE-V1.md` at repo root (supersedes phase-internal `CAPABILITY-CONTRACT.md`) | — |
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
