namespace Muonroi.Pdf.Tests.Layout;

// LAYOUT-07 (border-collapse:collapse) is covered in Muonroi.Pdf.Governance.Tests
// via DefaultStrictPolicy — that policy emits a PolicyViolation for border-collapse:collapse.

public sealed class TableLayoutTests
{
    private static LayoutContext MakeContext(float availableWidth = 400f) =>
        new()
        {
            PageWidth = availableWidth,
            PageHeight = 800f,
            AvailableWidth = availableWidth,
            CurrentY = 0f,
            CurrentPageIndex = 0,
            TotalPages = 0,
            TextMetrics = EstimatedTextMetrics.Instance,
            PageMargins = PdfMargins.Zero,
        };

    private static (BlockLayoutEngine block, TableLayoutEngine table) MakeEngines()
    {
        var block = new BlockLayoutEngine();
        var table = new TableLayoutEngine(block, block.InlineEngine);
        block.TableEngine = table;
        return (block, table);
    }

    // SC3: column widths sum to available width (minus border-spacing)
    [Fact]
    public void ThreeColumnTable_ColumnWidthsSumToAvailableWidth()
    {
        float borderSpacing = 0f;
        var (tableBox, row1Cells, _) = BuildSimpleTable(3, borderSpacing);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 300f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        // Use tracked row1 cell references (3 single-column cells)
        float totalWidth = row1Cells.Sum(cell => output.First(e => e.Source == cell).Position.Width);

        totalWidth.Should().BeApproximately(300f, precision: 1f,
            because: "column widths should span the full available width when border-spacing is zero");
    }

    // SC3: colspan=2 cell width == sum of the two underlying column widths
    [Fact]
    public void ColspanTwo_CellWidth_EqualsSumOfTwoColumnWidths()
    {
        float borderSpacing = 0f;
        var (tableBox, _, row2Cells) = BuildThreeColumnTableWithColspan(borderSpacing);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 300f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        // Find the colspan=2 cell (row1, col0-1)
        var colspanPe = output.First(e => e.Source is TableCellBox c && c.Colspan == 2);

        // Find the two single-column cells in row2 (col0 and col1)
        var row2CellPes = output
            .Where(e => e.Source is TableCellBox c && c.Colspan == 1)
            .OrderBy(e => e.Position.X)
            .ToList();

        float col0Width = row2CellPes[0].Position.Width;
        float col1Width = row2CellPes[1].Position.Width;
        float expectedColspanWidth = col0Width + col1Width; // no border-spacing between them

        colspanPe.Position.Width.Should().BeApproximately(expectedColspanWidth, precision: 1f,
            because: "a colspan=2 cell spans the widths of both underlying columns");
    }

    // SC3: border-spacing creates a gap between adjacent cells
    [Fact]
    public void BorderSpacing_CreatesGapBetweenAdjacentCells()
    {
        float borderSpacingPx = 8f;
        float borderSpacingPt = borderSpacingPx * Units.PxToPt; // 6pt

        var (tableBox, row1Cells, _) = BuildSimpleTable(3, borderSpacingPt);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 400f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        // Use tracked cell references to find exactly row1's col0 and col1 PEs
        var col0Pe = output.First(e => e.Source == row1Cells[0]);
        var col1Pe = output.First(e => e.Source == row1Cells[1]);

        // Gap between col0's right edge and col1's left edge should equal borderSpacing
        float gap = col1Pe.Position.X - (col0Pe.Position.X + col0Pe.Position.Width);

        gap.Should().BeApproximately(borderSpacingPt, precision: 0.5f,
            because: "border-spacing should appear as a gap between adjacent cell right/left edges");
    }

    // SC3: rowspan=2 cell height covers both rows
    [Fact]
    public void RowspanTwo_CellHeight_CoversBothRows()
    {
        // Build a 2x2 table where cell (0,0) has rowspan=2
        var tableBox = new TableBox { BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };

        var row1 = new TableRowBox();
        var cellRowspan = new TableCellBox { Rowspan = 2 };
        var cell1B = new TableCellBox { Rowspan = 1 };
        row1.Children.Add(cellRowspan);
        row1.Children.Add(cell1B);

        var row2 = new TableRowBox();
        var cell2B = new TableCellBox { Rowspan = 1 };
        row2.Children.Add(cell2B);

        tbody.Children.Add(row1);
        tbody.Children.Add(row2);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 200f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var rowspanPe = output.First(e => e.Source == cellRowspan);
        var singleRow1Pe = output.First(e => e.Source == cell1B);
        var singleRow2Pe = output.First(e => e.Source == cell2B);

        float expectedHeight = singleRow1Pe.Position.Height + singleRow2Pe.Position.Height;
        rowspanPe.Position.Height.Should().BeGreaterThanOrEqualTo(expectedHeight,
            because: "rowspan=2 cell height covers both row heights");
    }

    // SC-3 / Gap-6 regression: large colspan exceeding computed columnCount must not throw.
    [Fact]
    public void LargeColspanRowspan_DoesNotThrow()
    {
        // Row 0: one cell with colspan=36 (exceeds the natural 3-column grid)
        // Row 1: 3 normal cells (colspan=1)
        // Row 2: 3 normal cells (colspan=1)
        // ComputeColumnCount will use max(36, 3, 3) = 36. AssignColumnIndices must
        // handle rows 1 and 2 where col advances to 3 << 36 — no crash from occupied[].
        var tableBox = new TableBox { BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };

        var row0 = new TableRowBox();
        var wideCell = new TableCellBox { Colspan = 36, Rowspan = 1 };
        var textInline = new InlineBox { Text = "Wide", FontFamily = "serif", FontSize = 12f };
        wideCell.Children.Add(textInline);
        row0.Children.Add(wideCell);

        var row1 = new TableRowBox();
        for (int c = 0; c < 3; c++) row1.Children.Add(new TableCellBox { Colspan = 1, Rowspan = 1 });

        var row2 = new TableRowBox();
        for (int c = 0; c < 3; c++) row2.Children.Add(new TableCellBox { Colspan = 1, Rowspan = 1 });

        tbody.Children.Add(row0);
        tbody.Children.Add(row1);
        tbody.Children.Add(row2);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 400f);
        var output = new List<PositionedElement>();

        float height = 0f;
        var ex = Record.Exception(() => height = tableEngine.Layout(tableBox, ctx, output, pageIndex: 0));

        ex.Should().BeNull("TableLayoutEngine must not throw for colspan > columnCount");
        height.Should().BeGreaterThan(0f, "table with at least one row should have positive height");
    }

    // Helpers

    private static (TableBox table, List<TableCellBox> row1Cells, List<TableCellBox> row2Cells)
        BuildSimpleTable(int columns, float borderSpacing)
    {
        var tableBox = new TableBox { BorderSpacing = borderSpacing };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };

        var row1Cells = new List<TableCellBox>();
        var row1 = new TableRowBox();
        for (int c = 0; c < columns; c++)
        {
            var cell = new TableCellBox { Colspan = 1, Rowspan = 1 };
            row1.Children.Add(cell);
            row1Cells.Add(cell);
        }

        var row2Cells = new List<TableCellBox>();
        var row2 = new TableRowBox();
        for (int c = 0; c < columns; c++)
        {
            var cell = new TableCellBox { Colspan = 1, Rowspan = 1 };
            row2.Children.Add(cell);
            row2Cells.Add(cell);
        }

        tbody.Children.Add(row1);
        tbody.Children.Add(row2);
        tableBox.Children.Add(tbody);

        return (tableBox, row1Cells, row2Cells);
    }

    private static (TableBox table, List<TableCellBox> row1Cells, List<TableCellBox> row2Cells)
        BuildThreeColumnTableWithColspan(float borderSpacing)
    {
        // Row1: [td colspan=2][td]
        // Row2: [td][td][td]
        var tableBox = new TableBox { BorderSpacing = borderSpacing };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };

        var row1Cells = new List<TableCellBox>();
        var row1 = new TableRowBox();
        var cellColspan = new TableCellBox { Colspan = 2, Rowspan = 1 };
        var cellSingle = new TableCellBox { Colspan = 1, Rowspan = 1 };
        row1.Children.Add(cellColspan);
        row1.Children.Add(cellSingle);
        row1Cells.Add(cellColspan);
        row1Cells.Add(cellSingle);

        var row2Cells = new List<TableCellBox>();
        var row2 = new TableRowBox();
        for (int c = 0; c < 3; c++)
        {
            var cell = new TableCellBox { Colspan = 1, Rowspan = 1 };
            row2.Children.Add(cell);
            row2Cells.Add(cell);
        }

        tbody.Children.Add(row1);
        tbody.Children.Add(row2);
        tableBox.Children.Add(tbody);

        return (tableBox, row1Cells, row2Cells);
    }
}
