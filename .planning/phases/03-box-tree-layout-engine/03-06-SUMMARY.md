# Plan 03-06 Summary: TableLayoutEngine + PaginationEngine

## Accomplished

Implemented CSS 2.1 §17.5.2 table column sizing with colspan/rowspan support
(`TableLayoutEngine`) and full pagination with page-break controls, header/footer
repetition, and CSS counter substitution (`PaginationEngine`). Both engines are wired
into the existing `BlockLayoutEngine` via a lazy-injection pattern that avoids circular
constructor dependencies.

## Tasks Completed

| Task | Commit |
|------|--------|
| Task 1: TableLayoutEngine (column sizing, colspan/rowspan, border-spacing) | `e67d1e3` |
| Task 2: PaginationEngine (page breaks, header/footer, counter(page/pages)) | `345f875` |

## Files Created or Modified

| File | Change |
|------|--------|
| `src/Muonroi.Pdf/Internal/Layout/TableLayoutEngine.cs` | Created — CSS 2.1 §17.5.2 auto + fixed column widths, two-pass rowspan sizing, spanning algorithm (PITFALL 3 compliant) |
| `src/Muonroi.Pdf/Internal/Layout/PaginationEngine.cs` | Created — Y-order element distribution across pages, page-break-before/inside handling (PITFALL 4 anti-loop), counter substitution, header/footer per page |
| `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` | Modified — widened `Layout(BlockBox → BoxNode)`, replaced `LayoutTable` placeholder with `TableEngine?.Layout(...)`, exposed `InlineEngine` and `TableEngine` properties |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` | Modified — added `WidthRaw` property (already present from prior session) |
| `src/Muonroi.Pdf/Internal/Layout/Boxes/TableCellBox.cs` | Modified — added `ColumnIndex` property |
| `src/Muonroi.Pdf/Internal/GlobalUsings.cs` | Modified — added `global using System.Linq` |

## Deviations from Plan

- **BlockLayoutEngine.Layout signature**: widened from `BlockBox` to `BoxNode` (superset — all BlockBox callers still compile; required for TableLayoutEngine to call `_blockEngine.Layout(cell, ...)` where cell is `TableCellBox`). Plan 05 had already created this file with a `BlockBox` signature; widening was the minimal change.
- **BlockLayoutEngine dependency on TableLayoutEngine**: implemented via a settable `internal TableLayoutEngine? TableEngine { get; set; }` property (lazy injection after both engines are constructed), avoiding the circular constructor dependency described in the plan. `LayoutEngine` (Plan 07) will set this property.
- **PaginationEngine.Paginate signature**: added `pageBottomMarginPt` and `pageWidth` parameters beyond what the plan sketch listed — both are required for footer Y positioning.

## Known Issues

None. `dotnet build src/Muonroi.Pdf/Muonroi.Pdf.csproj` exits 0 with 0 errors.
Two-pass layout orchestration (Plan 07 LayoutEngine) will invoke `PaginationEngine.Paginate`
with `totalPages=0` on pass 1 and the actual count on pass 2, completing the counter(pages) flow.
