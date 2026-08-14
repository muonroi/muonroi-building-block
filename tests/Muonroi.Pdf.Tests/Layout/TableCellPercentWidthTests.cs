namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G20 regression tests: table column solver honors cell width:% in auto + fixed modes.
/// Root cause: ComputeAutoColumnWidths only used content character widths and never
/// consulted cell.WidthRaw; ComputeFixedColumnWidths skipped Width=-1f (% sentinel).
/// </summary>
public sealed class TableCellPercentWidthTests
{
    private static LayoutContext MakeContext(float availableWidth = 500f) =>
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

    // -------------------------------------------------------------------------
    // Case 1: Auto table — 3 cells with declared % widths, table 500pt
    // <td style="width:50%">A</td><td style="width:30%">B</td><td style="width:20%">C</td>
    // Expected column widths approximately 250/150/100pt (border-spacing=0).
    // -------------------------------------------------------------------------
    [Fact]
    public void AutoTable_ThreeCells_PercentWidths_SolvedCorrectly()
    {
        var tableBox = BuildThreeCellTable(
            tableLayout: "auto",
            borderSpacing: 0f,
            cellDefs: [("50%", "A"), ("30%", "B"), ("20%", "C")]
        );

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        var col0Pe = output.First(e => e.Source == cells[0]);
        var col1Pe = output.First(e => e.Source == cells[1]);
        var col2Pe = output.First(e => e.Source == cells[2]);

        col0Pe.Position.Width.Should().BeApproximately(250f, precision: 2f,
            because: "50% of 500pt table = 250pt");
        col1Pe.Position.Width.Should().BeApproximately(150f, precision: 2f,
            because: "30% of 500pt table = 150pt");
        col2Pe.Position.Width.Should().BeApproximately(100f, precision: 2f,
            because: "20% of 500pt table = 100pt");
    }

    // -------------------------------------------------------------------------
    // Case 2: Fixed-layout table — same cells → same widths.
    // -------------------------------------------------------------------------
    [Fact]
    public void FixedTable_ThreeCells_PercentWidths_SolvedCorrectly()
    {
        var tableBox = BuildThreeCellTable(
            tableLayout: "fixed",
            borderSpacing: 0f,
            cellDefs: [("50%", "A"), ("30%", "B"), ("20%", "C")]
        );

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        var col0Pe = output.First(e => e.Source == cells[0]);
        var col1Pe = output.First(e => e.Source == cells[1]);
        var col2Pe = output.First(e => e.Source == cells[2]);

        col0Pe.Position.Width.Should().BeApproximately(250f, precision: 2f,
            because: "fixed-layout 50% of 500pt = 250pt");
        col1Pe.Position.Width.Should().BeApproximately(150f, precision: 2f,
            because: "fixed-layout 30% of 500pt = 150pt");
        col2Pe.Position.Width.Should().BeApproximately(100f, precision: 2f,
            because: "fixed-layout 20% of 500pt = 100pt");
    }

    // -------------------------------------------------------------------------
    // Case 3: Auto table — cell declares width:10% but has long content.
    // 10% of 500pt = 50pt. Long content has min-content >> 50pt.
    // Column must be AT LEAST min-content width (% is preferred, not max).
    //
    // With EstimatedTextMetrics (0.6 * fontSize per char, fontSize=12):
    //   each char = 7.2pt. "LongContentWord" = 15 chars × 7.2pt = 108pt min-content.
    //   10% of 500pt = 50pt < 108pt → column must be ≥ 108pt.
    // -------------------------------------------------------------------------
    [Fact]
    public void AutoTable_PercentWidthSmallerThanContent_ColumnIsAtLeastMinContent()
    {
        var tableBox = new TableBox { TableLayout = "auto", BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row = new TableRowBox();

        var longWordCell = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "10%", Width = -1f };
        // Single inline box with one long word (no spaces) — sets min-content width
        var longText = new InlineBox { Text = "LongContentWord", FontFamily = "serif", FontSize = 12f };
        longWordCell.Children.Add(longText);
        row.Children.Add(longWordCell);

        tbody.Children.Add(row);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        // 15 chars × 7.2pt/char = 108pt min-content
        float expectedMinContent = 15 * 12f * 0.6f; // 108pt
        float declaredPercent = 500f * 0.10f;        // 50pt

        var cellPe = output.First(e => e.Source == longWordCell);
        cellPe.Position.Width.Should().BeGreaterThanOrEqualTo(expectedMinContent - 1f,
            because: $"min-content ({expectedMinContent}pt) must floor the column even when width:{declaredPercent}pt is declared smaller");
    }

    // -------------------------------------------------------------------------
    // Case 4: Cell with WidthRaw=null falls back to content-measure behavior.
    // Regression guard — the fix must not affect null-WidthRaw cells.
    // -------------------------------------------------------------------------
    [Fact]
    public void AutoTable_NullWidthRaw_FallsBackToContentMeasure()
    {
        var tableBox = new TableBox { TableLayout = "auto", BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row = new TableRowBox();

        // Two cells: both WidthRaw=null
        var cellA = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = null, Width = -1f };
        var textA = new InlineBox { Text = "Short", FontFamily = "serif", FontSize = 12f };
        cellA.Children.Add(textA);

        var cellB = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = null, Width = -1f };
        var textB = new InlineBox { Text = "LongerContent", FontFamily = "serif", FontSize = 12f };
        cellB.Children.Add(textB);

        row.Children.Add(cellA);
        row.Children.Add(cellB);
        tbody.Children.Add(row);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();

        // Must not throw and must produce positive widths
        var ex = Record.Exception(() => tableEngine.Layout(tableBox, ctx, output, pageIndex: 0));
        ex.Should().BeNull(because: "null WidthRaw cells must not cause exceptions");

        var peCellA = output.First(e => e.Source == cellA);
        var peCellB = output.First(e => e.Source == cellB);

        peCellA.Position.Width.Should().BeGreaterThan(0f, because: "cell with null WidthRaw must get a positive width");
        peCellB.Position.Width.Should().BeGreaterThan(0f, because: "cell with null WidthRaw must get a positive width");
        // Total should fill the table width
        (peCellA.Position.Width + peCellB.Position.Width).Should().BeApproximately(500f, precision: 1f,
            because: "columns with no declared widths should fill available table width");
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static TableBox BuildThreeCellTable(
        string tableLayout,
        float borderSpacing,
        (string widthRaw, string text)[] cellDefs)
    {
        var tableBox = new TableBox { TableLayout = tableLayout, BorderSpacing = borderSpacing };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row = new TableRowBox();

        foreach (var (widthRaw, text) in cellDefs)
        {
            var cell = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = widthRaw, Width = -1f };
            var inline = new InlineBox { Text = text, FontFamily = "serif", FontSize = 12f };
            cell.Children.Add(inline);
            row.Children.Add(cell);
        }

        tbody.Children.Add(row);
        tableBox.Children.Add(tbody);
        return tableBox;
    }
}
