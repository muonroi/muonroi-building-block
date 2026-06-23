---
phase: 18-flexbox-layout-engine
plan: 02
subsystem: pdf-layout
tags: [flexbox, box-tree, css, layout-engine, FLEX-05]
requires: [18-01]
provides:
  - "FlexContainerBox box type with resolved container flex props"
  - "BoxNode flex-item props (FlexGrow/FlexShrink/FlexBasisRaw/Order/AlignSelf)"
  - "Gated display:flex/inline-flex -> FlexContainerBox mapping (AllowModernLayout)"
  - "AllowModernLayout threaded MPdfService -> LayoutAsync -> RunLayout -> Build"
affects:
  - "18-03 (FlexLayoutEngine consumes the typed flex box tree + resolved props)"
tech-stack:
  added: []
  patterns:
    - "Gated box-type mapping via `when _allowModernLayout` switch guards (degrade-preserving)"
    - "CSS shorthand expansion (flex / flex-flow / gap) reusing existing ParseLength"
key-files:
  created:
    - src/Muonroi.Pdf/Internal/Layout/Boxes/FlexContainerBox.cs
    - tests/Muonroi.Pdf.Tests/Layout/FlexBoxTreeTests.cs
  modified:
    - src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs
    - src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs
    - src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs
    - src/Muonroi.Pdf/Internal/Service/MPdfService.cs
    - tests/Muonroi.Pdf.Tests/Service/AllocationProbe.cs
decisions:
  - "flex:<number> single-number form expands to FlexGrow=number, FlexShrink=1, FlexBasisRaw=\"0%\" (literal locked to \"0%\" per CSS spec)"
  - "RenderColumnInto (running header/footer) passes allowModernLayout:false — first-cut deferral, flex in @page running content degrades to block"
  - "FlexContainerBox children built via NormalizeChildren (same collect/wrap path as BlockBox)"
metrics:
  duration: ~8 min
  completed: 2026-06-21
  tasks: 2
  files: 7
---

# Phase 18 Plan 02: Flexbox Box Tree Summary

Added the `FlexContainerBox` box type plus nullable flex-item props on `BoxNode`, and wired `BoxTreeBuilder` to map `display:flex`/`inline-flex` to `FlexContainerBox` ONLY when `AllowModernLayout` is on (else the existing `BlockBox` degrade path stays byte-identical). Threaded the flag `MPdfService → LayoutEngine.LayoutAsync → RunLayout → BoxTreeBuilder.Build`, mirroring how `_configs.Limits` already flows. No layout algorithm — this plan produces a fully-typed, fully-resolved flex box tree for Plan 03 to position.

## What Was Built

### Task 1 — FlexContainerBox + BoxNode flex-item props (commit `3ea502df`)

`FlexContainerBox : BoxNode` (sealed, namespace `Muonroi.Pdf.Internal.Layout.Boxes`) — resolved container props with CSS-default values:

| Prop | Type | Default |
|------|------|---------|
| `FlexDirection` | `string` | `"row"` |
| `FlexWrap` | `string` | `"nowrap"` |
| `JustifyContent` | `string` | `"flex-start"` |
| `AlignItems` | `string` | `"stretch"` |
| `AlignContent` | `string` | `"stretch"` |
| `RowGap` | `float` | `0f` (points) |
| `ColumnGap` | `float` | `0f` (points) |
| `IsInlineFlex` | `bool` | `false` |

`BoxNode` nullable flex-ITEM props (resolved on EVERY child so any box type can be a flex item; null = CSS initial value, zero behavioural change when unset):

| Prop | Type | null = |
|------|------|--------|
| `FlexGrow` | `float?` | CSS default 0 |
| `FlexShrink` | `float?` | CSS default 1 |
| `FlexBasisRaw` | `string?` | CSS default `auto` |
| `Order` | `int?` | CSS default 0 |
| `AlignSelf` | `string?` | CSS default `auto` (inherit container `AlignItems`) |

### Task 2 — Gated mapping + prop resolution + threaded flag (commit `4a8d61f8`, TDD)

- `BoxTreeBuilder.Build` gained `bool allowModernLayout = false`; stored in private field `_allowModernLayout`. `CreateBox` changed from `static` to instance so it reads the field.
- Display switch: added `"flex" when _allowModernLayout => new FlexContainerBox { Source = node }` and `"inline-flex" when _allowModernLayout => new FlexContainerBox { Source = node, IsInlineFlex = true }` BEFORE the `_ => new BlockBox` default. Flag off ⇒ flex hits `_` ⇒ `BlockBox` (degrade unchanged). `grid`/`inline-grid` are NOT mapped (Plan-19 scope).
- `BuildNode` recursion: added `case FlexContainerBox flexBox` that calls a new `BuildChildren(node, FlexContainerBox)` overload (same `CollectChildren` + `NormalizeChildren` as the block path) and propagates inherited text props / transform group.
- `ResolveFlexProperties` (new): container props read only for `FlexContainerBox` (incl. `flex-flow` shorthand applied first, then `flex-direction`/`flex-wrap` longhand overrides; `gap` one-or-two lengths, then `row-gap`/`column-gap` overrides). Item props read on every box: `flex` shorthand (then `flex-grow`/`flex-shrink`/`flex-basis` longhand overrides), `order`, `align-self`. All lengths reuse the existing `ParseLength`; malformed values fall back to CSS defaults and never throw (T-18-04).
- `flex` shorthand expansion (CSS Flexbox §7.1): `none`→(0,0,auto); `auto`→(1,1,auto); `initial`→(0,1,auto); `<number>`→(number,1,**"0%"**); `<g> <s>`→(g,s,"0%"); `<g> <basis>`→(g,1,basis); `<g> <s> <basis>`→(g,s,basis). The single-number basis literal is **locked to `"0%"`**.

### Exact threaded signatures

```csharp
// MPdfService.cs (both LayoutAsync call sites)
layout.LayoutAsync(styled, options, _configs.Limits, _configs.Policy.AllowModernLayout,
                   _fontResolver, _resourceResolver, _imageDecoder, cts.Token[, running])

// LayoutEngine.cs
public async Task<IPositionedPageList> LayoutAsync(
    IStyledDocument doc, PdfRenderOptions options, PdfConfigs.PdfLimits limits,
    bool allowModernLayout,                          // <-- new, after limits
    IFontResolver? fontResolver, IResourceResolver? imageResolver, IImageDecoder imageDecoder,
    CancellationToken ct, RunningContentSpec? running = null)

private PositionedPageList RunLayout(
    IStyledDocument doc, PdfRenderOptions options, int totalPages,
    IReadOnlyDictionary<string, DecodedImage>? resolvedImages = null,
    RunningContentSpec? running = null, bool allowModernLayout = false)  // <-- new, trailing

// BoxTreeBuilder.cs
public BlockBox Build(IStyledNode root,
    IReadOnlyDictionary<string, DecodedImage>? resolvedImages = null,
    bool allowModernLayout = false)                  // <-- new, trailing
```

`LayoutAsync` passes `allowModernLayout` into BOTH `engineToUse.RunLayout(...)` calls (pass 1 + pass 2). `RunLayout` passes it to `_boxTreeBuilder.Build(doc.Root, resolvedImages, allowModernLayout)`.

## Deviations from Plan

None — plan executed as written. The implementation choices the plan left to discretion were resolved as follows:

- **flex:<number> basis literal:** locked to `"0%"` exactly (the plan made this mandatory; the test asserts the literal `"0%"`).
- **MSTD compliance adjustment** (within plan scope, not a behavioural deviation): the initial container-prop assignment used the null-forgiving operator `!`, which MSTD0002 forbids. Replaced with a `OneOf(value, fallback, allowed...)` helper returning a guaranteed non-null `string` for the non-nullable container props. No `MGuard` needed (no `ArgumentNullException` path introduced).

## Phase-18 Deferral (documented)

**Flex inside @page running header/footer columns degrades to block.** `RenderColumnInto` (LayoutEngine.cs) explicitly passes `allowModernLayout: false` when building running-content box trees. Running header/footer content has no flex use-case today; this is a first-cut deferral, **not a bug**. Body content honours the configured `AllowModernLayout` flag normally.

## Verification

- `dotnet build src/Muonroi.Pdf/Muonroi.Pdf.csproj -c Debug` — clean (0 warnings, 0 errors).
- `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj --filter FlexBoxTreeTests` — **8/8 passed**.
- Full project regression `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` — **594/594 passed, 0 failed** (existing `BoxTreeBuilderTests` + all goldens/layout tests unchanged; degrade path byte-identical with the flag defaulting false).
- Grep confirms `new FlexContainerBox` in `BoxTreeBuilder.cs` and `AllowModernLayout` reaching `LayoutAsync`/`RunLayout`/`Build`.

## Success Criteria (FLEX-05) — met

- [x] `FlexContainerBox : BoxNode` exists carrying resolved container flex props.
- [x] `BoxNode` carries nullable flex-item props resolved for every child.
- [x] `BoxTreeBuilder` maps flex/inline-flex → `FlexContainerBox` ONLY when `AllowModernLayout` is on; else `BlockBox` degrade path unchanged (Test 2 proves it; 594 goldens unchanged).
- [x] `AllowModernLayout` threaded `MPdfService → LayoutAsync → RunLayout → Build`.
- [x] `flex`/`flex-flow` shorthand, `gap`/`row-gap`/`column-gap`, `flex-basis`, `order` parsed and resolved via `ParseLength`.

## Self-Check: PASSED

- Files: FlexContainerBox.cs, FlexBoxTreeTests.cs, 18-02-SUMMARY.md all present.
- Commits 3ea502df + 4a8d61f8 present in git log.
- `new FlexContainerBox` x2 in BoxTreeBuilder.cs; AllowModernLayout/allowModernLayout x7 in LayoutEngine.cs.
