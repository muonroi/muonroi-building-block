# Phase 8.12b — Real-Template Visual Sweep (VERIFICATION)

> **Closed:** 2026-05-28
> **Branch:** `phase/08.12b-harness-logo` → merged to develop
> **Predecessor:** Phase 8.12 (`0354883` — lineHeight=0f latent fix)
> **Scope:** Engine bugs surfaced by Chrome-MCP visual diff of _E templates after 8.12 close-out.

## Commits (4 atomic on branch)

| # | SHA | Subject |
|---|-----|---------|
| 1 | `43d1661` | fix(08.12b): UA fallback for table structure elements + ReplacedBox honors CSS height (G14) |
| 2 | `41d0a97` | test(08.12b): loosen G9 image-Y threshold to >20pt (post-G14 cell at ~33pt) |
| 3 | `58205d4` | fix(08.12b): configure AngleSharp render device + ReplacedBox CSS height priority (G15+G16+G17) |
| 4 | `1a628c7` | fix(08.12b): float epsilon — 3rd float fits same row when widths sum to 100% (G15b) |

## Findings & fixes

### G14 — `_E` template tables silently skip rendering

**Root cause:** AngleSharp's `GetComputedStyle` throws `ArgumentException` for `%` widths when no render device is configured. The cascade engine caught the throw and returned `AngleSharpComputedStyle.Empty`, leaving `display` blank for `<table>` / `<tbody>` / `<tr>` / `<td>`. `BoxTreeBuilder.CreateBox` fell through to `BlockBox` instead of `TableBox` / `TableRowGroupBox` / `TableRowBox` / `TableCellBox`. Result: `CollectRows` found 0 rows → table height = 0 → silent disappearance. Affected all 6 `_E` templates (CHNG_E, CAPR_E, CRCD_E, CSLA_E, HANG_E, NHAR_E).

**Fix (in `BoxTreeBuilder.CreateBox`):** when `rawDisplay` is empty, fall back to HTML5 UA stylesheet display:
```csharp
"table" → "table", "tbody" → "table-row-group", "tr" → "table-row",
"td"/"th" → "table-cell", "caption" → "table-caption", etc.
```
Secondary fix (G16, included in same commit): swap `ReplacedBox` height priority so explicit CSS `height` beats intrinsic `NaturalHeight` (4×4 stub PNG decoding to 3pt was overriding `height:100px`).

### G15 + G17 — Engine-wide AngleSharp throw eats `width` / `float` / `text-align` / `border` for % widths

**Root cause (shared with G14):** the same `ArgumentException` swallow returned `Empty` for ALL `_E` template elements that used `%` widths, not just tables. Every `.w-20.float-left` lost its `float`, every `.w-30` lost its `width`, every `<th style="width:16%">` lost its width.

**Fix #1 — `AngleSharpStyledNode.cs`:** catch `ArgumentException` from `GetComputedStyle` and fall back to `el.GetStyle()` (inline-only, never throws). Inline-style declarations are preserved for ALL elements with `style="..."`.

**Fix #2 — `BoxTreeBuilder.ExtractClassRules()` + `LookupClassProperty`:** parse `<style>` blocks once, build `Dictionary<string, Dictionary<string, string>>` keyed by className, look up `width` / `float` / `text-align` / `border` for any element with a `class` attribute when the inline-style fallback doesn't cover it. ~80 LOC.

### G15b — 3rd float drops to next row when w-20 + w-50 + w-30 = 100% exactly

**Root cause:** float-rounding accumulates in `cb.Width * 0.20f + cb.Width * 0.50f` versus `cb.Width * 0.30f`. When the third float's `boxWidth` is `cb.Width * 0.30f`, the strict `availableWidth >= boxWidth` check fails by sub-pt. Geometrically valid (sum ≤ 100% by spec) but arithmetically off by ~0.005pt.

**Fix (`FloatPlacementSolver.AvoidCollisions`):** permit 0.5pt tolerance: `availableWidth >= boxWidth - 0.5f`. Conservative; only matters when exact-fit floats split a 100% row. Tightened HSLA_E from 3 → 2 pages (expected page count updated).

### G16 — Inline `<img style="height:Npx">` not honored when image has intrinsic height

**Root cause:** `ReplacedBox` height computation:
```csharp
// BEFORE — intrinsic wins:
float h = replacedChild.NaturalHeight > 0f ? replacedChild.NaturalHeight : replacedChild.Height;
```
A 4×4 stub PNG decodes to `NaturalHeight ≈ 3pt`, short-circuiting the CSS `height:100px` (=75pt).

**Fix:** swap priority — CSS Height first, NaturalHeight second, line-height fallback last.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | `_E` template tables render with content + grid borders | PASS — `RealTemplate_CHNG_E_ContainsTableContent` added |
| SC2 | 3 floats with widths summing to 100% land on same row | PASS — visually verified CHNG_E header (logo / TÂN CẢNG / Mã lô block) |
| SC3 | Inline `<img style="height:Npx">` honors CSS height | PASS — G16 priority swap |
| SC4 | All prior 386 golden tests pass + new tests | PASS — 388/388 |
| SC5 | G9 regression guard still passes post-G14 (image at cell, not page top) | PASS — `imgY ≈ 33pt > 20pt` threshold |

## Files changed (high-level)

- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — UA display fallback + `ExtractClassRules` + `LookupClassProperty`
- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` — `ReplacedBox` height priority swap (G16)
- `src/Muonroi.Pdf/Internal/Layout/FloatPlacementSolver.cs` — 0.5pt epsilon (G15b)
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledNode.cs` — `ArgumentException` fallback to inline `GetStyle()`
- `tests/Muonroi.Pdf.Tests/Diagnostic/HbndFLogoPositionDiagnostic.cs` — threshold loosened (50→20pt)
- `tests/Muonroi.Pdf.Tests/Golden/RealTemplateBaselineTests.cs` — `HSLA_E` page count 3→2; `CHNG_E_ContainsTableContent` added

## Out of scope — surfaced for Phase 8.13

Chrome-MCP visual diff of post-G15b CHNG_E vs reference HTML revealed 4 remaining engine gaps. Documented in `.planning/GAPS-AND-DEBT.md` as G18/G19/G20/G21; tracked for Phase 8.13.

| ID | Symptom | Suspected root cause |
|----|---------|----------------------|
| G18 | `<h2>` not bold, `text-transform:uppercase` not applied to descendant `<h2>` text | UA stylesheet missing `h2 { font-weight: bold }`; class-rule lookup doesn't cover `text-transform` |
| G19 | `<p>label: <strong>value</strong></p>` wraps to 2 lines inside `div.w-30.float-left` | Float content-box width may be shrink-to-fit min-content rather than honoring class-rule `width:30%` |
| G20 | `<th style="width:16%">` wraps each word — column appears 1-char wide | Inline `%` width on `<th>` not propagating to table column-width solver |
| G21 | `<p>Số điện thoại: 0901234567</p>` wraps 3 lines inside `div.w-25.float-left.text-center` | Same family as G19 — nested float width not class-resolved |

G19/G20/G21 likely share a root cause family (% width resolution in float / table-cell contexts post-G15).

## Lessons learned

- **AngleSharp render device must be configured globally for ANY `%` value.** G14/G15/G17 all surfaced from the same swallowed `ArgumentException`. Future cascade work should treat "no render device" as a hard error, not a silent `Empty`.
- **Visual diff is the catch-net for engine-vs-template confusion** — and the catch-net for engine bugs that survive golden tests. Goldens pass when output is *stable*, not when it's *correct*. User-driven Chrome-MCP review caught 9 real gaps in 8.12+8.12b that goldens missed.
- **Intrinsic-first dimension resolution is wrong** — CSS explicit dimensions always beat content-derived dimensions. G16 was a 6-line fix masquerading as a layout bug for 3 phases.

## References

- `.planning/phase-08.12b/RESEARCH-G14.md`
- `.planning/phase-08.12b/RESEARCH-G15.md`
- `.planning/phase-08.12b/RESEARCH-G15b.md`
- `.planning/phase-08.12b/RESEARCH-G16.md`
- `.planning/phase-08.12b/RESEARCH-G17.md`
- `.planning/GAPS-AND-DEBT.md`
- CSS 2.1 §10.3.3 — block-level widths; §17.5 — visual formatting of tables
