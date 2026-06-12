# Plan 03-04 Summary: DOM-to-Box-Tree Converter, Text Metrics Seam, Layout Context, Test Scaffold

**Completed:** 2026-05-27
**Phase:** 03-box-tree-layout-engine
**Plan:** 04

---

## One-Line Summary

Implemented BoxTreeBuilder (IStyledNode → BoxNode tree with CSS 2.1 §9.2.1 anonymous box wrapping), the ITextMetrics/EstimatedTextMetrics seam, LayoutContext, positioned output types, and the Muonroi.Pdf.Tests xunit scaffold.

---

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| Task 1: ITextMetrics, EstimatedTextMetrics, LayoutContext | `985743d` | Text metrics seam + monospace approximation + layout state carrier |
| Task 2: BoxTreeBuilder | `9afea7c` | IStyledNode → BoxNode converter with display:none, anonymous box wrapping, CSS property resolution |
| Task 3: Positioning types + test scaffold | `96da35a` | PositionedElement/Page/PageList + Muonroi.Pdf.Tests csproj + FakeStyledNode |

---

## Files Created

**src/Muonroi.Pdf/Internal/Layout/**
- `ITextMetrics.cs` — internal interface; 4 methods (GetCharWidth, GetLineHeight, GetAscender, GetDescender)
- `EstimatedTextMetrics.cs` — monospace approximation singleton (0.6f/1.2f/0.8f/0.2f multipliers)
- `LayoutContext.cs` — layout state carrier (page dims, y-cursor, page index, total pages, margins in pt)
- `BoxTreeBuilder.cs` — IStyledNode tree → BlockBox root converter; anonymous box wrapping per CSS 2.1 §9.2.1
- `PositionedElement.cs` — Rect + BoxNode source + page index
- `PositionedPage.cs` — List<PositionedElement> + page index
- `PositionedPageList.cs` — implements IPositionedPageList; PageCount == Pages.Count

**tests/Muonroi.Pdf.Tests/**
- `Muonroi.Pdf.Tests.csproj` — net8.0 xunit project; test packages auto-injected by Directory.Build.props
- `GlobalUsings.cs` — Xunit, FluentAssertions, Muonroi.Pdf.Abstractions, Muonroi.Pdf.Abstractions.Engine
- `Helpers/FakeStyledNode.cs` — FakeStyledNode + FakeComputedStyle stubs implementing IStyledNode/IComputedStyle

---

## Deviations from Plan

None. All files match the spec in the plan context exactly.

Notable implementation details:
- `LayoutContext.PageMargin*Pt` properties cast `double * float → float` since `PdfMargins` uses `double` (TopMm/RightMm/BottomMm/LeftMm) and `Units.MmToPt` is `float`.
- `ParseLength` uses `ReadOnlySpan<char>` for allocation-free suffix stripping.
- `NormalizeChildren` short-circuits when no block-level children are present (avoids unnecessary list allocation for pure-inline containers).

---

## Build Status

| Project | Result |
|---------|--------|
| `src/Muonroi.Pdf/Muonroi.Pdf.csproj` | ✅ 0 errors, 0 warnings |
| `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` | ✅ 0 errors, 0 warnings |

---

## Known Issues

None. Phase 4 (SixLabors font metrics) will replace EstimatedTextMetrics.Instance via constructor injection into the layout engine.
