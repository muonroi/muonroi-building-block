# Phase 19: CSS Grid Layout Engine (OSS `Muonroi.Pdf`) — Context

**Gathered:** 2026-06-21
**Status:** Ready for planning
**Source:** Autonomous (user: "ok" → run Phase 19 after Phase 18 flexbox shipped). Phase 18 deliberately deferred CSS Grid to this phase under the SAME `AllowModernLayout` opt-in flag. This phase is the direct sibling of Phase 18 — identical architecture, grid-specific algorithm.

<domain>
## Phase Boundary

Close the **second half** of the flex/grid render gap: implement a real CSS Grid layout algorithm in the OSS `Muonroi.Pdf` engine. Today (post-Phase-18) `display:grid`/`inline-grid` is still hard-blocked (`forbidden.display.grid`) or soft-degraded to block with grid sub-props silently dropped. This phase unlocks grid rendering behind the EXISTING opt-in `PdfPolicySettings.AllowModernLayout` flag (already added in Phase 18) — when the flag is on, BOTH flex (Phase 18) and grid (this phase) render.

**This extends the OSS engine** (like Phase 18). Safety invariant identical: **existing golden baselines stay byte-identical; only NEW grid goldens are added** — proving the default path and the already-shipped flex path are unperturbed.

**Verified current state (evidence, 2026-06-21, post-Phase-18):**
- **Policy gate** — `LegacyPrintPolicy.cs:264-273`: `display:grid`/`inline-grid` → `forbidden.display.grid` (Error) or `soft-degrade.display.grid` (Warning when `SoftDegradeUnknownDisplay=true`). This branch is NOT gated on `AllowModernLayout` (Phase 18 left grid blocked). Flex branch at `:253` IS gated (`&& !allowModernLayout`) — mirror that for grid.
- **Grid sub-props** — `LegacyPrintPolicy.cs:284-323`: in soft-degrade mode, grid sub-props (`FlexGridSubProperties` HashSet) emit one `soft-degrade.grid-subproperty` warning and are dropped. The grid branch (`:294-305`) does NOT have the `&& !allowModernLayout` guard that the flex branch got at `:310` — add it so grid sub-props are NOT dropped when the flag is on.
- **`FlexGridSubProperties`** (`LegacyPrintPolicy.cs:32-41`) already lists the grid longhands: `grid-template-columns/rows/areas/template`, `grid-column*`, `grid-row*`, `grid-area`, `grid-auto-columns/rows/flow`, `grid`. (`gap`/`row-gap`/`column-gap` shared with flex — already resolved by Phase 18.)
- **Box creation seam** — `BoxTreeBuilder.cs` display→box switch: `grid`/`inline-grid` currently fall through to `BlockBox` default. Phase 18 added `flex`/`inline-flex`→`FlexContainerBox` gated on the threaded `allowModernLayout`. Add `grid`/`inline-grid`→`GridContainerBox` the same way.
- **Layout dispatch seam** — `BlockLayoutEngine.DispatchLayout` has `case FlexContainerBox` (Phase 18) mirroring the `TableBox` case. Add `case GridContainerBox` → `GridEngine.Layout(...)`.
- **Engine wiring** — `LayoutEngine` ctor sets `_blockEngine.FlexEngine` post-construction (cycle-break). Add `_blockEngine.GridEngine` the same way. The `allowModernLayout` flag is ALREADY threaded `MPdfService` → `LayoutEngine.LayoutAsync` → `RunLayout` → `BoxTreeBuilder.Build` (Phase 18) — grid reuses it, no new threading.
- **Existing assets from Phase 18 to mirror** — `Boxes/FlexContainerBox.cs` (container-box pattern), `FlexLayoutEngine.cs` (non-block engine: measure → size → place → recurse via `_blockEngine.Layout` → emit `PositionedElement`s), `BoxNode` flex-item props (add grid-item props the same way: nullable = CSS initial).
- **Golden infra** — `GoldenPdf.VerifyAsync(..., bool allowModernLayout)` overload ALREADY exists (Phase 18) — reuse it for grid goldens (render with the flag on). `GoldenCorpus.FlexLayout` is a standalone group OUTSIDE `AllCases`; `ByName` does `AllCases.Concat(FlexLayout)`. Add `GridLayout` the SAME way (standalone, out of `AllCases`, extend `ByName` to `.Concat(FlexLayout).Concat(GridLayout)`).
- **Assemblies (all OSS, building-block)** — layout → `Muonroi.Pdf`; policy → `Muonroi.Pdf.Governance`; config → `Muonroi.Pdf.Abstractions`. Single-repo phase.

**Hard boundary:** Grid is opt-in via the existing flag. With `AllowModernLayout=false` (default) grid behaves EXACTLY as today (strict-block or soft-degrade-to-block). No existing golden baseline may change a byte. Flex behaviour (Phase 18) is also untouched.
</domain>

<decisions>
## Implementation Decisions

### D-01 — Scope: a solid, useful CSS Grid subset (close the gap, defer the exotica)
IN scope:
- **Track definitions:** `grid-template-columns` / `grid-template-rows` with track sizes `<length>` (px/pt), `%`, `fr` (flexible), `auto`, `minmax(min, max)`, and `repeat(<integer>, <track-list>)`.
- **Gaps:** `gap` / `row-gap` / `column-gap` (+ legacy `grid-gap`/`grid-row-gap`/`grid-column-gap` aliases). Reuse Phase 18's gap resolution where possible.
- **Explicit item placement:** `grid-column` / `grid-row` (and `-start`/`-end`): line numbers (1-based, negative from end), `span N`.
- **Named areas:** `grid-template-areas` + `grid-area` (named placement; `grid-area` also accepts the row/col shorthand).
- **Auto-placement:** items without explicit placement flow into the grid per `grid-auto-flow: row | column`; implicit tracks sized by `grid-auto-rows` / `grid-auto-columns`.
- **Box & content alignment:** `justify-items` / `align-items` / `justify-self` / `align-self` (item within cell) and `justify-content` / `align-content` (track group within container).

OUT of scope (defer, document):
- `subgrid`; `repeat(auto-fill | auto-fit, ...)`; `grid-auto-flow: dense` (use sparse packing); masonry; baseline alignment (approximate as `start`, like Phase 18 flex); percentage track/gap resolved against an indefinite container (treat indefinite as content/auto); splitting a grid container across a page boundary (atomic for pagination, first cut — same as Phase 18 flex).

### D-02 — Contract: reuse the EXISTING `AllowModernLayout` flag (no new flag)
Gate grid acceptance in `LegacyPrintPolicy` on `AllowModernLayout` (mirror the Phase 18 flex gate exactly):
| `AllowModernLayout` | `SoftDegradeUnknownDisplay` | grid behaviour |
|---|---|---|
| **true** | (any) | `LegacyPrintPolicy` ACCEPTS grid (no violation for grid display/sub-props); engine renders real Grid. |
| false | true | UNCHANGED: Warning `soft-degrade.display.grid`, degrade to block, grid sub-props dropped. |
| false | false (DEFAULT) | UNCHANGED: Error `forbidden.display.grid`, render aborts. |
- `DefaultStrictPolicy` is **unchanged** (always hard-blocks grid).
- No new config key. No existing policy test may change its expectation; ADD grid accept-path tests. Specifically: the Phase 18 test that proves `display:grid` is STILL blocked with the flag on MUST be UPDATED (grid is now accepted with the flag on) — this is the one expected, deliberate test change this phase, and it is the inverse of Phase 18's FLEX-04 guard.

### D-03 — New `GridContainerBox` + `GridLayoutEngine` in `Muonroi.Pdf`
- `GridContainerBox : BoxNode` (in `Internal/Layout/Boxes/`, mirror `FlexContainerBox`) carrying resolved grid container props (template columns/rows as parsed track lists, auto-flow, auto-rows/cols, gaps, justify/align-items/content, template-areas grid). Grid-item props on `BoxNode` (nullable): `GridColumnRaw`, `GridRowRaw`, `GridAreaRaw`, `JustifySelf`, `AlignSelf` (AlignSelf already added in Phase 18 — reuse).
- `GridLayoutEngine` (mirror `FlexLayoutEngine`/`TableLayoutEngine`): `Layout(GridContainerBox, context, output, pageIndex) → height`. Recurses children through `_blockEngine.Layout` so nested grid/flex/block/table compose.
- `BoxTreeBuilder`: add `grid`/`inline-grid` → `GridContainerBox` ONLY when `allowModernLayout` is true (else fall through to `BlockBox`, preserving soft-degrade). Resolve grid props (parse track lists, `repeat()`, `minmax()`, `grid-template-areas` string grid, item placement shorthands).
- `BlockLayoutEngine.DispatchLayout`: add `case GridContainerBox` delegating to `GridEngine.Layout`, mirroring the `FlexContainerBox`/`TableBox` cases.
- `LayoutEngine` ctor: construct `GridLayoutEngine` and set `_blockEngine.GridEngine` (post-construction, same as Flex/Table).

### D-04 — Golden safety: existing baselines byte-identical; grid goldens are NEW
- All existing baselines (default-path + the 9 Phase-18 flex baselines) MUST remain byte-identical. The structural snapshot suite passes with NO `MUONROI_UPDATE_SNAPSHOTS`.
- Add a `GridLayout` corpus group (standalone, OUTSIDE `AllCases` — same reason as flex: the flag-less `DeterminismCanaryTests`/byte-equality theory would throw `PdfPolicyException` on a grid case) + `GridLayoutGoldenTests` rendered with `AllowModernLayout=true` via the existing flag-aware `VerifyAsync` overload. Generate baselines once via `MUONROI_UPDATE_SNAPSHOTS=1`; commit. Update the regression-guard count `[Fact]` only if the default-path corpus count legitimately changes (it should NOT — grid adds 0 to `AllCases`).
- Plus unit tests asserting `PositionedElement.Position` (X/Y/W/H) by OPERAND VALUE for representative grid scenarios (fixed tracks, fr distribution, minmax, repeat, gap, explicit line placement, span, auto-placement row/column, named areas, nested grid). Assert positions, not non-throwing renders (memory `pdf_phase15_radial_affine`).

### D-05 — Algorithm scope (spec-essential, deterministic)
Implement the CSS Grid track-sizing + placement essentials, deterministically:
1. Parse explicit track lists (resolve `repeat()`, `minmax()`); count explicit columns/rows.
2. Place explicitly-positioned items (line numbers, span, named areas); build the placement grid.
3. Auto-place the rest per `grid-auto-flow` (sparse), creating implicit tracks sized by `grid-auto-rows`/`grid-auto-columns`.
4. Resolve track sizes: fixed (px/%), then `auto`/content (measure via `_blockEngine.Layout`), then distribute remaining free space across `fr` tracks proportionally; honor `minmax()` clamps and gaps.
5. Position each item into its cell rectangle (origin + accumulated track sizes + gaps), apply `justify-self`/`align-self` (and container `justify-items`/`align-items`); apply `justify-content`/`align-content` to position the whole track group when tracks don't fill the container.
6. Recurse item content via `_blockEngine.Layout`.
**Deferred (document, don't implement):** the OUT-of-scope list in D-01 (subgrid, auto-fill/fit, dense, masonry, baseline alignment, indefinite-container %, page-splitting).

### Claude's Discretion
Track-list parser representation (struct list vs records); how `grid-template-areas` maps to a name→rect index; whether grid-item placement props live on `BoxNode` vs a side-table; `inline-grid` participation (atomic block-level first cut, like Phase 18 inline-flex); exact grid golden case selection; whether `repeat()`/`minmax()` parsing lives in `BoxTreeBuilder` or a small parser helper alongside the track model.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase boundary
- `.planning/ROADMAP.md` §"Phase 19" (added alongside this CONTEXT).
- `.planning/phases/18-flexbox-layout-engine/18-CONTEXT.md` + `18-VERIFICATION.md` — the sibling phase; grid mirrors its architecture exactly. READ FIRST.

### Phase 18 assets to MIRROR (the proven pattern)
- `src/Muonroi.Pdf/Internal/Layout/Boxes/FlexContainerBox.cs` — container-box shape for `GridContainerBox`.
- `src/Muonroi.Pdf/Internal/Layout/FlexLayoutEngine.cs` — non-block engine structure: measure (max-content pass + `_blockEngine.Layout`), size, place, recurse, emit. `GridLayoutEngine` follows the same contract.
- `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` — Phase 18 added `FlexGrow/FlexShrink/FlexBasisRaw/Order/AlignSelf`; add grid-item props (`GridColumnRaw/GridRowRaw/GridAreaRaw/JustifySelf`; reuse `AlignSelf`).
- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` — `DispatchLayout` `case FlexContainerBox` (mirror for grid) + `FlexEngine` property (add `GridEngine`).
- `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` — ctor wiring of `FlexEngine` (mirror for `GridEngine`); `allowModernLayout` already threaded through `LayoutAsync`/`RunLayout`/`Build`.
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — flex→FlexContainerBox gated mapping + flex prop resolution (mirror for grid).
- `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` — closest 2-D analog (column widths, cell measurement, rowspan/colspan) — grid track sizing borrows from its measurement approach.

### Policy / config
- `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs:253` (flex gate, the EXACT pattern), `:264-273` (grid display — add `&& !allowModernLayout`), `:294-305` + `:310` (sub-prop drop — add the grid `&& !allowModernLayout` guard mirroring flex), `:32-41` (`FlexGridSubProperties` already lists grid longhands).
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — `PdfPolicySettings.AllowModernLayout` already exists (no change).
- `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` — grid block, leave unchanged.

### Tests
- `tests/Muonroi.Pdf.Tests/Layout/FlexLayoutTests.cs` + `FlexLayoutEngineSmokeTests.cs` — operand-value unit-test style to mirror for grid.
- `tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs` (`VerifyAsync(..., bool allowModernLayout)` overload — REUSE), `GoldenCorpus.cs` (`FlexLayout` standalone group + `ByName` concat pattern — mirror with `GridLayout`), `Golden/FlexLayoutGoldenTests.cs` (mirror `GridLayoutGoldenTests`), `Golden/FlexRegressionGuardTests.cs` (the count guard — update only if default corpus legitimately changes, which it must not).
- `tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyAllowModernLayoutTests.cs` — Phase 18 accept-path tests; the "grid still blocked with flag on" assertion here MUST be flipped to "grid accepted with flag on" + add grid accept-path coverage. `LegacyPrintPolicyTests.DisplayGrid_*` strict/default expectations stay unchanged.

### Memory
- `phase18_flexbox_layout` — the sibling; the **golden AllCases gotcha** (flag-gated cases must stay OUT of `AllCases` or the flag-less canary throws `PdfPolicyException`) applies identically to grid.
- `test_flakiness_nested_build` — per-project `dotnet test` (Pdf.Tests + Governance.Tests), not full solution.
- `ci_sdk8_vs_local_sdk10_cs1587` — validate net8.0 TFM, not only local .NET 10.
- `pdf_phase15_radial_affine` — assert operand VALUES (a green suite once hid a translate bug).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets (this phase is "mirror Phase 18 for grid")
- The ENTIRE opt-in plumbing (flag, threading, gated box mapping, flag-aware golden harness, standalone-corpus pattern) already exists from Phase 18 — grid reuses it verbatim.
- `TableLayoutEngine` is the closest 2-D layout analog (track/cell sizing + content measurement).
- `FlexLayoutEngine`'s max-content measurement pass (`_blockEngine.Layout` into throwaway output → max right-edge) is reusable for grid `auto`/content track sizing.

### Established Patterns
- Box type per display value (`BoxTreeBuilder` switch) → layout engine per box type (`DispatchLayout` switch), engine wired post-construction in `LayoutEngine` ctor.
- Strict-by-default; modern layout behind `AllowModernLayout`.
- Flag-gated golden cases live in a standalone corpus group OUTSIDE `AllCases`; rendered via `VerifyAsync(..., allowModernLayout:true)`.
- Existing-baselines-byte-identical is the regression proof.

### Integration Points
- `LegacyPrintPolicy` gates grid acceptance on the existing flag (mirror flex).
- `BoxTreeBuilder` maps grid→GridContainerBox when flag on.
- `BlockLayoutEngine.DispatchLayout` → `GridLayoutEngine` (new); `LayoutEngine` ctor wires `GridEngine`.
</code_context>

<specifics>
## Specific Ideas
- This phase fully closes the flex/grid gap surfaced after PDF Enterprise: with `AllowModernLayout=true`, the engine renders block/inline/table/float/abs-pos (existing) + flex (Phase 18) + grid (this phase).
- Grid track sizing is the genuinely new algorithm; placement (explicit + named areas + auto-flow) is the other substantial piece. Lean on `TableLayoutEngine` for measurement intuition and `FlexLayoutEngine` for the engine scaffold.
- Keep the `fr` distribution + `minmax()` clamp deterministic and unit-testable (operand-value assertions on resolved track sizes / item rects).
</specifics>

<deferred>
## Deferred Ideas
- `subgrid`, `repeat(auto-fill | auto-fit, ...)`, `grid-auto-flow: dense`, masonry.
- True baseline alignment (approximate as `start`).
- Percentage tracks/gaps against an indefinite container (treat as content/auto).
- Splitting a grid container across a page boundary (atomic for pagination, first cut).
- Flipping `AllowModernLayout` default to true / making flex+grid first-class in `DefaultStrictPolicy` — revisit after soak (post Phase 18+19).
</deferred>

---

*Phase: 19-grid-layout-engine*
*Context gathered: 2026-06-21 (autonomous)*
