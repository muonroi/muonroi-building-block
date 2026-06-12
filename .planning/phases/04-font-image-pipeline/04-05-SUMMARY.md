# Plan 04-05 Summary: Wire Font and Image Sub-Pipelines into Layout Engine

Wired `FontPipeline`, `ImagePipeline`, `GlyphCollector`, and `TrueTypeFontSubsetter` into the layout engine via a new async overload, and extended the layout data carrier for Phase 5 consumption.

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| Task 1: PositionedPageList + BoxTreeBuilder + FontPipeline | `f030e88` | Data carrier, img box, image sizing, FontCollection return |
| Task 2: LayoutEngine.LayoutAsync | `c6f3bd8` | Full async pipeline orchestration |

## Files Modified

- `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` — Added `EmbeddedFonts` and `Images` with `internal set`
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — Optional `resolvedImages` param; `<img>` → `ReplacedBox`; `NaturalWidth/Height` from decoded image dimensions
- `src/Muonroi.Pdf/Internal/Font/FontPipeline.cs` — Return type extended with `FontCollection` as third tuple element
- `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` — New `LayoutAsync` overload; `RunLayout` accepts optional `resolvedImages`

## Deviations from Plan

- `PositionedPageList` used `internal set` directly (instead of `init` then switching) — the plan's own recommendation after analysing init-after-construction constraints
- `BoxTreeBuilder._resolvedImages` is set at the start of `Build` (not thread-safe, acceptable per plan note)

## Verification

`dotnet test tests/Muonroi.Pdf.Tests/` — **23 passed, 0 failed**

## Known Issues

None.
