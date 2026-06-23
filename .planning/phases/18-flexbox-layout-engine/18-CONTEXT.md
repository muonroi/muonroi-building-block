# Phase 18: Flexbox Layout Engine (OSS `Muonroi.Pdf`) — Context

**Gathered:** 2026-06-21
**Status:** Ready for planning
**Source:** Autonomous discuss (decisions locked by orchestrator per explicit user grant — "Đi vào automous đóng gap flex/grid cho tôi nhé"). User confirmed two scope forks via AskUserQuestion: (1) **Flexbox now, Grid deferred to Phase 19**; (2) **new opt-in `AllowModernLayout` flag, strict-by-default preserved, zero breaking change**. Evidence-backed by 3 parallel codebase maps (layout engine, CSS/policy pipeline, test infra) run 2026-06-21.

<domain>
## Phase Boundary

Close the **single largest remaining render gap** in the OSS PDF engine: `display:flex` is currently NOT rendered — it is either hard-blocked (`forbidden.display.flex`) or, in soft-degrade mode, silently collapsed to `display:block` with sub-properties dropped. This phase implements a **real CSS Flexbox layout algorithm** inside `Muonroi.Pdf` (OSS, Apache-2.0), gated behind a new opt-in policy flag so the strict-by-default charter is untouched.

**This phase changes the OSS engine.** Unlike Phases 16/17 (SC5: zero OSS change, byte-identical goldens), Phase 18 deliberately extends `Muonroi.Pdf`. The safety invariant flips from "OSS byte-identical" to **"existing 82 golden baselines stay byte-identical; only NEW flex goldens are added"** — proving the default (block/inline/table) path is unchanged while flex is added under an opt-in flag.

**Verified current state (evidence, 2026-06-21):**
- **Box creation seam** — `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs:196-207`: `switch(effectiveDisplay)` maps display→box type. `flex`/`inline-flex`/`grid`/`inline-grid` fall through to the `_ => new BlockBox` default (degraded).
- **Layout dispatch seam** — `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs:368-483`: `switch(child)` on box type (`HrBox`/`BlockBox`/`AnonymousBox`/`InlineBox`/`ReplacedBox`/`TableBox`/default). A new `case FlexContainerBox` is added here.
- **Box model base** — `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs`: holds Width/Height/Min/Max, Margin/Padding/Border (per-side floats, points), Display, Position, FloatValue, Children, Source. New `FlexContainerBox : BoxNode` + per-item flex props.
- **Computed style** — `IComputedStyle.GetValue(string)` over a `Dictionary<string,string>` (kebab-case keys). ZERO flex props read in layout today. `CascadeResolver.cs` resolves length units; `CssRuleSet.SupplementalProperties` (lines 294-302) recovers CSS3 props AngleSharp drops — flex props may need adding here.
- **Policy gate** — `LegacyPrintPolicy` (DEFAULT, `PdfServiceCollectionExtensions.cs:87` `TryAddSingleton`): `flex`/`inline-flex` → `forbidden.display.flex` (Error) or `soft-degrade.display.flex` (Warning when `SoftDegradeUnknownDisplay=true`), lines 247-256; `FlexGridSubProperties` HashSet (lines 32-41) lists the 24 flex/grid longhands dropped in soft-degrade. `DefaultStrictPolicy.cs:155-158` always hard-blocks.
- **Pipeline order** — `MPdfService.cs:95-100` policy validation runs BEFORE `LayoutEngine.LayoutAsync` (line 104). So the box tree only builds flex when policy ACCEPTS it.
- **Config** — `PdfConfigs.PdfPolicySettings` (`src/Muonroi.Pdf.Abstractions/PdfConfigs.cs:58-72`) holds `SoftDegradeUnknownDisplay` (default false). New `AllowModernLayout` lands here.
- **Golden infra** — `tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs`: structural snapshot (decompress FlateDecode + drop xref), 82 baselines in `TestResources/Golden/`, re-baseline via `MUONROI_UPDATE_SNAPSHOTS=1`. Unit layout tests assert `PositionedElement.Position` X/Y/W/H with `.Should().BeApproximately(.., 0.1f)`.
- **Assemblies (all OSS, building-block)** — layout → `Muonroi.Pdf`; policy + cascade → `Muonroi.Pdf.Governance`; config + `IComputedStyle` → `Muonroi.Pdf.Abstractions`. `Muonroi.Pdf` → references `Muonroi.Pdf.Governance`. **Single-repo phase** (no cross-repo coordination).

**Hard boundary:** Flexbox is opt-in. With `AllowModernLayout=false` (default) every existing consumer behaves EXACTLY as today (strict-block, or soft-degrade-to-block). No existing golden baseline may change a single byte.
</domain>

<decisions>
## Implementation Decisions

### D-01 — Scope: Flexbox only; Grid deferred to Phase 19
Implement a complete single-axis CSS Flexbox layout. **CSS Grid is OUT** — it stays degrade-to-block / blocked exactly as today and becomes its own Phase 19. Flex properties in scope:
- **Container:** `flex-direction` (row | row-reverse | column | column-reverse), `flex-wrap` (nowrap | wrap | wrap-reverse), `flex-flow` shorthand, `justify-content` (flex-start | flex-end | center | space-between | space-around | space-evenly), `align-items` (flex-start | flex-end | center | stretch | baseline), `align-content` (multi-line: flex-start | flex-end | center | space-between | space-around | stretch), `gap` / `row-gap` / `column-gap`.
- **Items:** `flex-grow`, `flex-shrink`, `flex-basis`, `flex` shorthand (e.g. `flex: 1`, `flex: 1 1 auto`, `flex: 0 0 200px`), `align-self`, `order`.
- `inline-flex` is supported (treated as a flex container that participates inline / as an atomic block first cut — see D-05 discretion).

### D-02 — Contract: new opt-in `AllowModernLayout`, strict stays default (ZERO breaking change)
Add `PdfPolicySettings.AllowModernLayout` (bool, default **false**) in `Muonroi.Pdf.Abstractions/PdfConfigs.cs`. Behaviour matrix (must hold exactly):
| `AllowModernLayout` | `SoftDegradeUnknownDisplay` | flex behaviour |
|---|---|---|
| **true** | (any) | `LegacyPrintPolicy` ACCEPTS flex (no violation for flex display/sub-props); engine renders real Flexbox. |
| false | true | UNCHANGED: Warning `soft-degrade.display.flex`, degrade to block, sub-props dropped. |
| false | false (DEFAULT) | UNCHANGED: Error `forbidden.display.flex`, render aborts. |
- `DefaultStrictPolicy` is **unchanged** — it is the always-strict explicit gate; it keeps hard-blocking flex regardless of the flag.
- **Grid stays blocked even when `AllowModernLayout=true`** (grid is Phase 19): with the flag on, `display:grid` and grid-* sub-props still emit `forbidden.display.grid` (or soft-degrade per the existing matrix). Only flex is unlocked by the flag this phase.
- No existing policy test may change its expectation. NEW tests cover the `AllowModernLayout=true` accept-path.

### D-03 — New `FlexContainerBox` + `FlexLayoutEngine` in `Muonroi.Pdf`
- New box type `FlexContainerBox : BoxNode` (in `Internal/Layout/Boxes/`) carrying resolved container flex props; flex-item props are resolved onto each child `BoxNode` (extend `BoxNode` with nullable flex-item fields, or a small per-item struct — Claude's discretion).
- New `FlexLayoutEngine` (in `Internal/Layout/`) mirroring the shape of `BlockLayoutEngine`/`TableLayoutEngine`: `Layout(FlexContainerBox, context, output, pageIndex) → height`. It positions children by emitting `PositionedElement`s and **recurses into children through the existing dispatch** so nested flex/block/inline/table all work.
- `BoxTreeBuilder.cs:196-207`: add `"flex"`/`"inline-flex"` → `FlexContainerBox` **only when `AllowModernLayout` is true**; otherwise keep falling through to `BlockBox` (preserves soft-degrade path). This requires threading `AllowModernLayout` from `MPdfService` → `LayoutEngine.LayoutAsync` → `BoxTreeBuilder` (it already threads `_configs.Limits`; add `_configs.Policy.AllowModernLayout` the same way).
- `BlockLayoutEngine.DispatchLayout` (368-483): add `case FlexContainerBox flexChild:` delegating to `FlexLayoutEngine.Layout(...)` and emitting the container `PositionedElement`, mirroring the `TableBox` case.
- `ResolveCssProperties` in `BoxTreeBuilder`: read & resolve the flex container + item props (parse `flex` shorthand, resolve `gap`/`flex-basis` lengths via existing `ParseLength`).

### D-04 — Golden safety: existing 82 baselines byte-identical; flex goldens are NEW
- Existing golden baselines in `TestResources/Golden/` MUST remain byte-identical (the default path is untouched). A run of the full `Muonroi.Pdf.Tests` golden suite with NO `MUONROI_UPDATE_SNAPSHOTS` must pass against the committed baselines.
- Add a new `FlexLayout` corpus group in `GoldenCorpus.cs` + a `FlexLayoutGoldenTests` class; generate the new flex baselines once via `MUONROI_UPDATE_SNAPSHOTS=1` and commit them. Each new golden case renders with `AllowModernLayout=true`.
- Plus unit tests asserting `PositionedElement.Position` for representative flex scenarios (row distribution, grow/shrink, justify/align, wrap, gap, column direction, nested flex) — mirror `BlockLayoutTests`/`TableLayoutTests` style.

### D-05 — Algorithm scope (spec-essential, deterministic)
Implement the CSS Flexbox resolution essentials, deterministically and unit-testably:
1. Resolve flex-basis per item (explicit basis | width/height in main axis | content size via existing measurement).
2. Determine flex lines (wrap when accumulated main size + gaps exceeds container main size; single line for nowrap).
3. Resolve flexible lengths per line: distribute free space by `flex-grow` (positive free space) / `flex-shrink × basis` (negative), with the standard frozen-item iteration and min-size (0 / content) clamping.
4. Main-axis alignment: `justify-content` (incl. space-between/around/evenly) using leftover free space + gaps.
5. Cross-axis: line cross-size = max item cross-size; `align-items`/`align-self` (stretch resolves item cross-size); `align-content` distributes multiple lines.
6. `order` reorders items for layout (visual order only).
**Deferred (document, don't implement):** true baseline alignment across mixed-font items (approximate `baseline` as `flex-start` cross-alignment first cut, documented); `flex-basis: content` intrinsic min/max-content distinctions beyond existing content measurement; `aspect-ratio`; percentage flex-basis resolved against an indefinite container; **splitting a flex container across a page boundary** — first cut treats an overflowing flex container like the table engine treats overflow (no mid-container page split; natural overflow / single-page placement), with the behaviour documented and a deferred note. Grid (all of it) → Phase 19.

### Claude's Discretion
Exact `FlexContainerBox` field set & whether flex-item props live on `BoxNode` vs a side-table; how `inline-flex` participates (atomic inline-block first cut acceptable); pagination treatment of tall flex containers (document the chosen first-cut); how `AllowModernLayout` is threaded through `LayoutAsync` (new param vs passing the whole `PdfPolicySettings`); whether `flex`/`flex-flow` shorthand expansion lives in `CascadeResolver.ExpandShorthands` or in `BoxTreeBuilder`; test case selection for the flex golden corpus.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase boundary
- `.planning/ROADMAP.md` §"Phase 18" — goal, scope, SC (added alongside this CONTEXT).

### Layout engine (where flex layout is implemented — `Muonroi.Pdf`)
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — `:196-207` display→box switch (add flex cases, gated on `AllowModernLayout`); `ResolveCssProperties` (~210-559) reads computed props (add flex prop reads).
- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` — `:368-483` `DispatchLayout` box-type switch (add `FlexContainerBox` case); `:39-196` block layout pattern to mirror; `:133` child recursion.
- `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` — closest analog: a non-block layout engine driven from `DispatchLayout`, emits `PositionedElement`s, recurses children. Mirror its structure for `FlexLayoutEngine`.
- `src/Muonroi.Pdf/Internal/Layout/InlineLayoutEngine.cs` — content measurement / line composition reference for resolving item content sizes.
- `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` + `BlockBox.cs` + `TableBox.cs` — box model + how a specialized box subclass adds props. New `FlexContainerBox` here.
- `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` — `:32-50` entry + `:251` first `BlockLayoutEngine.Layout` call; thread `AllowModernLayout` from here into `BoxTreeBuilder`.
- `src/Muonroi.Pdf/Internal/Layout/PositionedElement.cs` + `Geometry/Rect.cs` — layout output contract the engine emits.

### Policy / config (the opt-in gate — `Muonroi.Pdf.Governance` + `.Abstractions`)
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs:58-72` — `PdfPolicySettings`; add `AllowModernLayout` (default false) next to `SoftDegradeUnknownDisplay`.
- `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` — `:32-41` `FlexGridSubProperties`; `:247-256` flex display handling; `:258-267` grid handling (KEEP blocking grid); `:278-315` sub-prop detection. Gate flex (only) on `AllowModernLayout`.
- `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs:155-158` — leave unchanged (always strict).
- `src/Muonroi.Pdf/Extensions/PdfServiceCollectionExtensions.cs:87` — default policy registration (`LegacyPrintPolicy`).
- `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs` (`:312-370` ExpandShorthands, `:724-734` length props) + `CssRuleSet.cs:294-302` SupplementalProperties — where flex props/shorthands are resolved/recovered if AngleSharp drops them.

### Tests
- `tests/Muonroi.Pdf.Tests/Layout/BlockLayoutTests.cs`, `TableLayoutTests.cs`, `InlineLayoutTests.cs` — unit assertion style (`FakeStyledNode` → `BoxTreeBuilder.Build` → engine.Layout → assert `PositionedElement.Position`). Mirror for flex.
- `tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs` (`VerifyAsync`, `MUONROI_UPDATE_SNAPSHOTS`), `GoldenCorpus.cs` (add `FlexLayout` group), `Golden/BlockLayoutGoldenTests.cs` (mirror for `FlexLayoutGoldenTests`).
- `tests/Muonroi.Pdf.Tests/Policy/LegacyPrintPolicyTests.cs` (`DisplayFlex_FailsBothPolicies`), `LegacyPrintPolicySoftDegradeTests.cs` — existing flex policy expectations must stay green; ADD allow-path tests.

### Memory
- `test_flakiness_nested_build` — run **per-project** `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` (+ `Muonroi.Pdf.Governance.Tests`), NOT the full ~80-project solution.
- `ci_sdk8_vs_local_sdk10_cs1587` — validate the build against .NET 8/9, not only local .NET 10, before declaring the gate green.
- `pdf_phase15_radial_affine`, `pdf_phase14_css_gaps`, `pdf_phase13_header_footer` — prior PDF layout phases; assert operand VALUES (code review caught a green-suite bug before).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`TableLayoutEngine`** — the template for a non-block layout engine: invoked from `DispatchLayout`, computes a 2-D arrangement, emits `PositionedElement`s, recurses children. `FlexLayoutEngine` follows the same contract.
- **Content measurement** — `InlineLayoutEngine` already measures text/inline content; reuse for flex-item content-size / `flex-basis: auto`.
- **`ParseLength` + percentage resolution** — already in `BoxTreeBuilder`; reuse for `gap`, `flex-basis`, item sizes.
- **Soft-degrade plumbing** — `SoftDegradeUnknownDisplay` already threads policy settings into `LegacyPrintPolicy`; `AllowModernLayout` rides the same `Options<PdfConfigs>` channel.
- **Golden structural snapshot** — re-baseline mechanism (`MUONROI_UPDATE_SNAPSHOTS=1`) already exists; only ADD new flex cases.

### Established Patterns
- Box type per display value (`BoxTreeBuilder` switch) → layout engine per box type (`DispatchLayout` switch). Add one of each for flex.
- Strict-by-default policy charter; new capabilities arrive behind an explicit opt-in flag (mirror `SoftDegradeUnknownDisplay`).
- `PositionedElement.Position` (Rect, y-down) is the universal layout output; the writer y-flips at emit.

### Integration Points
- `MPdfService` → `LayoutEngine.LayoutAsync` → `BoxTreeBuilder` (thread `AllowModernLayout`).
- `LegacyPrintPolicy` reads `PdfPolicySettings.AllowModernLayout` to gate flex acceptance.
- `BlockLayoutEngine.DispatchLayout` → `FlexLayoutEngine` (new).
</code_context>

<specifics>
## Specific Ideas
- Keep the policy flag **flex-specific in effect this phase**: `AllowModernLayout=true` unlocks flex only; grid stays blocked (Phase 19 flips grid under the same flag). Document this clearly so Phase 19 is a small follow-on.
- `FlexLayoutEngine` must **recurse via the existing dispatch** (not reimplement block/inline/table) so nested layouts compose and existing engines are reused.
- The decisive regression guard is **existing 82 goldens byte-identical** — that single fact proves the opt-in didn't perturb the default path.
- Assert flex layout by **operand values** (item X/Y/W/H), not just "renders without throwing" (per pdf_phase15 lesson: a green suite missed a translate bug).
</specifics>

<deferred>
## Deferred Ideas
- **CSS Grid** (track sizing, `fr` units, auto-placement, `grid-template-areas`) — entire feature → **Phase 19** (same `AllowModernLayout` flag will unlock it).
- **Flex container page-splitting** — mid-container pagination; first cut keeps a flex container atomic for pagination.
- **True cross-font baseline alignment** — approximate `baseline` as `flex-start` first cut.
- **`aspect-ratio`, intrinsic min/max-content sizing subtleties, percentage flex-basis vs indefinite container** — beyond essential resolution.
- Flipping `AllowModernLayout` default to true / making flex first-class in `DefaultStrictPolicy` — revisit after soak.
</deferred>

---

*Phase: 18-flexbox-layout-engine*
*Context gathered: 2026-06-21 (autonomous)*
