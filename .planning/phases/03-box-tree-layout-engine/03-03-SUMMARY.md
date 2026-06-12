# Plan 03-03 Summary: Geometry Helpers + Box Node Hierarchy

Defined the internal geometry types and complete box node hierarchy for the layout engine.

## Tasks Completed

| Task | Commit |
|------|--------|
| Task 1: Geometry helpers (Units, Rect, PdfPageSizeDimensions) | `3e33822` |
| Task 2: BoxNode abstract base + 9 concrete box types | `3229bb0` |

## Files Created

### Geometry (`src/Muonroi.Pdf/Internal/Layout/Geometry/`)
- `Units.cs` — CSS unit-to-points constants (MmToPt=2.834646f, InToPt=72f, CmToPt, PxToPt)
- `Rect.cs` — `readonly struct` with X/Y/Width/Height floats; implements IEquatable<Rect>; Right/Bottom computed properties
- `PdfPageSizeDimensions.cs` — switch expression returning (widthPt, heightPt) portrait dimensions; A4=(595.28f,841.89f), A3=(841.89f,1190.55f)

### Boxes (`src/Muonroi.Pdf/Internal/Layout/Boxes/`)
- `BoxNode.cs` — abstract base; margin/padding/border top/right/bottom/left (float); Width/Height (-1f=auto); Display, PageBreak*; Source IStyledNode?; Children List<BoxNode>
- `BlockBox.cs` — internal sealed, no additions
- `InlineBox.cs` — adds Text, FontFamily, FontSize, Bold, Italic, Color, VerticalAlign
- `AnonymousBox.cs` — internal sealed, no additions
- `ReplacedBox.cs` — adds Src, NaturalWidth, NaturalHeight (floats)
- `TableBox.cs` — adds TableLayout (string), BorderSpacing (float)
- `TableRowGroupBox.cs` — adds GroupType (TableRowGroupType enum: Header/Body/Footer)
- `TableRowBox.cs` — internal sealed, no additions
- `TableCellBox.cs` — adds Colspan=1, Rowspan=1 (int)

## Deviations

None. All must-haves satisfied:
- `PdfPageSizeDimensions.Get(PdfPageSize.A4)` returns `(595.28f, 841.89f)`
- All five box hierarchy branches extend BoxNode
- TableCellBox has Colspan and Rowspan int properties
- BoxNode has all four float edges for margin/padding/border
- All box types are `internal sealed`
- `dotnet build src/Muonroi.Pdf` exits 0 (51 pre-existing XML doc warnings, 0 errors)

## Known Issues

None.
