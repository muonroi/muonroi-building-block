# Plan 03-05 Summary — Block and Inline Layout Engines

Implemented BlockLayoutEngine (BFC with CSS 2.1 §8.3.1 margin collapsing) and InlineLayoutEngine (IFC with line boxes and baseline alignment), covering SC1 and SC2 of Phase 3.

## Tasks Completed

| Task | Commit |
|------|--------|
| Task 1: BlockLayoutEngine + BoxNode.WidthRaw + BoxTreeBuilder update | `1cf308d` |
| Task 2: InlineLayoutEngine | `6c0bd11` |

## Files Created / Modified

- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` — **new**: BFC layout, CollapseMargins, IsBfcRoot, DispatchLayout, percent-width resolution, TableLayoutEngine placeholder
- `src/Muonroi.Pdf/Internal/Layout/InlineLayoutEngine.cs` — **new**: word splitting, line accumulation, line commit with baseline/top/middle/bottom vertical-align
- `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` — added `WidthRaw` property (stores raw CSS width string, e.g. "50%")
- `src/Muonroi.Pdf/Internal/Layout/BoxTreeBuilder.cs` — stores `WidthRaw` from the `width` CSS value during box construction

## Deviations

None. Plan was followed as specified. TableLayoutEngine is a private placeholder returning `box.Height > 0 ? box.Height : 100f`; Plan 06 replaces it.

## Known Issues

None. `dotnet build src/Muonroi.Pdf/Muonroi.Pdf.csproj` exits 0 with 0 errors (51 pre-existing XML doc warnings only).
