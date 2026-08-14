namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G23e regression tests: fixed-layout tables with declared column widths summing to less
/// than the table width must scale columns proportionally to fill the table.
/// CSS 2.1 §17.5.2.1: "the remaining horizontal space is divided in proportion to the
/// column widths."
/// </summary>
public sealed class FixedTableColumnScalingTests
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

    private static TableBox BuildFixedTable(float borderSpacing, params string[] widthRaws)
    {
        var tableBox = new TableBox { TableLayout = "fixed", BorderSpacing = borderSpacing };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row = new TableRowBox();

        foreach (var wr in widthRaws)
        {
            var cell = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = wr, Width = -1f };
            var inline = new InlineBox { Text = "x", FontFamily = "serif", FontSize = 10f };
            cell.Children.Add(inline);
            row.Children.Add(cell);
        }

        tbody.Children.Add(row);
        tableBox.Children.Add(tbody);
        return tableBox;
    }

    // -------------------------------------------------------------------------
    // Case 1 (G23e core): 5-column CHNG_E scenario.
    // Columns declared 16%/10%/14%/14%/12% of a 500pt table (= 66% = 330pt).
    // Expected: each column scaled by 500/330 so totals equal 500pt.
    //   col0: 500 * 16/66 ≈ 121.21pt
    //   col1: 500 * 10/66 ≈  75.76pt
    //   col2: 500 * 14/66 ≈ 106.06pt
    //   col3: 500 * 14/66 ≈ 106.06pt
    //   col4: 500 * 12/66 ≈  90.91pt
    // -------------------------------------------------------------------------
    [Fact]
    public void FixedTable_FiveCols_SumLessThanTableWidth_ScalesProportionally()
    {
        var tableBox = BuildFixedTable(0f, "16%", "10%", "14%", "14%", "12%");

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        float[] colWidths = cells.Select(c => output.First(e => e.Source == c).Position.Width).ToArray();

        // Proportional scale = 500 / 330; declared shares (within 66%) determine ratios.
        const float tableWidth = 500f;
        const float totalDeclared = 66f; // sum of percentages
        float[] expected =
        [
            tableWidth * 16f / totalDeclared,  // ≈ 121.21pt
            tableWidth * 10f / totalDeclared,  // ≈  75.76pt
            tableWidth * 14f / totalDeclared,  // ≈ 106.06pt
            tableWidth * 14f / totalDeclared,  // ≈ 106.06pt
            tableWidth * 12f / totalDeclared,  // ≈  90.91pt
        ];

        for (int i = 0; i < expected.Length; i++)
        {
            colWidths[i].Should().BeApproximately(expected[i], precision: 0.5f,
                because: $"col{i} declared {new[] { 16, 10, 14, 14, 12 }[i]}% of {totalDeclared}% must scale to {expected[i]:F2}pt");
        }

        // Columns must also sum to full table width.
        colWidths.Sum().Should().BeApproximately(tableWidth, precision: 1f,
            because: "scaled columns must fill the full table width");
    }

    // -------------------------------------------------------------------------
    // Case 2: Declared widths sum > 100% (60%+60%=120%).
    // Phase 12.3 update: scale DOWN proportionally so the table fits its container.
    // Original design (no-shrink) caused production TCIS templates with header widths
    // summing to 108% to overflow page width — last columns rendered past the right
    // margin, producing phantom border rectangles + content merging into adjacent cells.
    // Chrome's PDF render normalizes both directions (scale-up when sum < 100%,
    // scale-down when sum > 100%); engine now mirrors that.
    // -------------------------------------------------------------------------
    [Fact]
    public void FixedTable_TwoCols_SumGreaterThanTableWidth_ScalesDownProportionally()
    {
        // 60% + 60% = 120% of 500pt = 600pt total declared — exceeds table width (500pt).
        // Expected: scale = 500/600 ≈ 0.8333 → each column becomes 250pt; sum = 500pt.
        var tableBox = BuildFixedTable(0f, "60%", "60%");

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        float col0Width = output.First(e => e.Source == cells[0]).Position.Width;
        float col1Width = output.First(e => e.Source == cells[1]).Position.Width;

        // 600 → 500 scale: each 300pt column becomes 250pt.
        col0Width.Should().BeApproximately(250f, precision: 1f,
            because: "over-declared column (60% × 2 = 120%) must be scaled down to fit 100%");
        col1Width.Should().BeApproximately(250f, precision: 1f,
            because: "over-declared column (60% × 2 = 120%) must be scaled down to fit 100%");

        // Sum must equal table width exactly.
        (col0Width + col1Width).Should().BeApproximately(500f, precision: 1f,
            because: "scaled columns must fill the full table width without overflow");
    }

    // -------------------------------------------------------------------------
    // Case 3: Declared widths sum exactly = 100% (50%+50%=100%).
    // No scaling should occur — columns already fill the table.
    // -------------------------------------------------------------------------
    [Fact]
    public void FixedTable_TwoCols_SumExactlyEqualsTableWidth_NoScaling()
    {
        // 50% + 50% = 100% of 500pt = 250pt + 250pt.
        var tableBox = BuildFixedTable(0f, "50%", "50%");

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        float col0Width = output.First(e => e.Source == cells[0]).Position.Width;
        float col1Width = output.First(e => e.Source == cells[1]).Position.Width;

        col0Width.Should().BeApproximately(250f, precision: 0.5f,
            because: "50% of 500pt = 250pt, no scaling needed");
        col1Width.Should().BeApproximately(250f, precision: 0.5f,
            because: "50% of 500pt = 250pt, no scaling needed");
        (col0Width + col1Width).Should().BeApproximately(500f, precision: 0.5f,
            because: "columns fill the full table width exactly");
    }

    // -------------------------------------------------------------------------
    // Case 4: Mixed declared + auto columns (30%/30%/auto).
    // The auto column absorbs remaining space. Declared columns must NOT be scaled.
    // This confirms the guard condition (autoCols == 0) prevents regression.
    // -------------------------------------------------------------------------
    [Fact]
    public void FixedTable_DeclaredPlusAutoColumn_AutoAbsorbsSlack_DeclaredUnchanged()
    {
        // 30% + 30% = 60% declared; 1 auto column fills remaining 40% of 500pt = 200pt.
        var tableBox = new TableBox { TableLayout = "fixed", BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row = new TableRowBox();

        var cellA = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "30%", Width = -1f };
        cellA.Children.Add(new InlineBox { Text = "A", FontFamily = "serif", FontSize = 10f });

        var cellB = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "30%", Width = -1f };
        cellB.Children.Add(new InlineBox { Text = "B", FontFamily = "serif", FontSize = 10f });

        // Auto column: WidthRaw=null, Width=-1f (no declared width)
        var cellC = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = null, Width = -1f };
        cellC.Children.Add(new InlineBox { Text = "C", FontFamily = "serif", FontSize = 10f });

        row.Children.Add(cellA);
        row.Children.Add(cellB);
        row.Children.Add(cellC);
        tbody.Children.Add(row);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        float wA = output.First(e => e.Source == cellA).Position.Width;
        float wB = output.First(e => e.Source == cellB).Position.Width;
        float wC = output.First(e => e.Source == cellC).Position.Width;

        wA.Should().BeApproximately(150f, precision: 1f,
            because: "declared 30% of 500pt = 150pt must not be scaled when auto col exists");
        wB.Should().BeApproximately(150f, precision: 1f,
            because: "declared 30% of 500pt = 150pt must not be scaled when auto col exists");
        wC.Should().BeApproximately(200f, precision: 1f,
            because: "auto column absorbs remaining 40% of 500pt = 200pt");
        (wA + wB + wC).Should().BeApproximately(500f, precision: 1f,
            because: "all columns together must fill the table width");
    }
}
