# Plan 03-08 Summary: Phase 3 Unit Tests (SC1–SC5)

**Accomplished**: Wrote and ran 22 unit tests across BoxTreeBuilder, BlockLayout,
InlineLayout, TableLayout, Pagination, and LayoutEngine integration. All 22 pass
(`dotnet test` exits 0). Also fixed a margin-collapse bug in BlockLayoutEngine
discovered while writing the SC1 test.

---

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| Task 1 | InternalsVisibleTo + FakeStyledDocument + BoxTreeBuilderTests (4) + BlockLayoutTests (3) | `4ce29af` |
| Task 2 | InlineLayoutTests (4) + TableLayoutTests (4) | `4ce29af` |
| Task 3 | PaginationTests (4) + LayoutEngineIntegrationTests (3) + FakePageRule | `4ce29af` |

---

## Files Created

| File | Tests |
|------|-------|
| `tests/Muonroi.Pdf.Tests/Helpers/FakeStyledDocument.cs` | Fake IStyledDocument for integration tests |
| `tests/Muonroi.Pdf.Tests/Helpers/FakePageRule.cs` | Fake IPageRule for pagination tests |
| `tests/Muonroi.Pdf.Tests/Layout/BoxTreeBuilderTests.cs` | 4 tests: display:none, AnonymousBox wrapping, all-inline, colspan attribute |
| `tests/Muonroi.Pdf.Tests/Layout/BlockLayoutTests.cs` | 3 tests: SC1 margin collapse (max not sum), gap < sum, BFC root |
| `tests/Muonroi.Pdf.Tests/Layout/InlineLayoutTests.cs` | 4 tests: SC2 vertical-align:top/middle/baseline, line height |
| `tests/Muonroi.Pdf.Tests/Layout/TableLayoutTests.cs` | 4 tests: SC3 column widths, colspan width, border-spacing gap, rowspan height |
| `tests/Muonroi.Pdf.Tests/Layout/PaginationTests.cs` | 4 tests: SC4 page-break-before:always, SC5 counter(pages)/counter(page), MaxPages |
| `tests/Muonroi.Pdf.Tests/Layout/LayoutEngineIntegrationTests.cs` | 3 tests: non-null, PageCount>=1, FakePageRule margins |

## Files Modified

| File | Change |
|------|--------|
| `src/Muonroi.Pdf/Muonroi.Pdf.csproj` | Added `<InternalsVisibleTo Include="Muonroi.Pdf.Tests" />` |
| `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` | Fixed margin-collapse bug + parent-loop formula |

---

## Deviations from Plan

1. **BlockLayoutEngine bug fix (unplanned)**: The plan assumed SC1 would pass with the
   existing implementation. Code review during test writing revealed that `DispatchLayout`
   baked `child.MarginBottom` into `ctx.CurrentY`, and the parent loop double-counted the
   height (`childY = ctx.CurrentY + childMarginTop + childHeight`). This produced `sum`
   instead of `max` for adjacent margin gaps. Fixed both: removed `MarginBottom` from
   `ctx.CurrentY` in DispatchLayout; changed parent formula to `childY = childContext.CurrentY`.

2. **`limits.MaxPages` is unused in LayoutEngine**: The plan said "set limits.MaxPages = 1"
   but `PdfConfigs.PdfLimits.MaxPages` is a `const = 1000`. LayoutEngine checks the const,
   not an instance property. The MaxPages test creates 1002 page-break-before:always blocks
   to exceed 1000 pages and trigger the exception via the const threshold.

3. **BoxTreeBuilder.Build always returns BlockBox**: The plan's context example showed
   `engine.Layout(doc, new PdfRenderOptions(), PdfConfigs.Limits.Default)` which does not
   match the actual signature. Used `new PdfConfigs.PdfLimits()` and added `CancellationToken.None`.

---

## Known Issues

None. `dotnet test tests/Muonroi.Pdf.Tests` exits 0 with 22 passing tests.
