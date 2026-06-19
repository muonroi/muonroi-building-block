using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G23g + G23h regression tests.
/// G23g: &lt;th&gt; UA text-align:center must be seeded in BoxTreeBuilder when no
///       author-level text-align is present.
/// G23h: TableLayoutEngine.CellContext must propagate TextAlign from the cell box
///       into the child LayoutContext so InlineLayoutEngine actually centres the text.
/// </summary>
public sealed class CellTextAlignmentTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static BoxTreeBuilder Builder() => new();

    private static FakeStyledNode TextNode(string content) =>
        new("#text") { IsText = true, IsElement = false, TextContent = content };

    private static List<TableCellBox> CollectCells(BoxNode root)
    {
        var result = new List<TableCellBox>();
        Recurse(root, result);
        return result;

        static void Recurse(BoxNode node, List<TableCellBox> acc)
        {
            if (node is TableCellBox cell) acc.Add(cell);
            foreach (var child in node.Children) Recurse(child, acc);
        }
    }

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

    // -------------------------------------------------------------------------
    // G23g — BoxTreeBuilder UA text-align seed
    // -------------------------------------------------------------------------

    /// <summary>G23g: &lt;th&gt; with no author text-align → cell.TextAlign == "center".</summary>
    [Fact]
    public void Th_NoAuthorTextAlign_CellTextAlignIsCenter()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var table = new FakeStyledNode("table", new() { ["display"] = "" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var th = new FakeStyledNode("th", new() { ["display"] = "" });
        th.ChildList.Add(TextNode("X"));
        tr.ChildList.Add(th);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        var cells = CollectCells(root);
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));

        thCell.Should().NotBeNull(because: "<th> must produce a TableCellBox");
        thCell!.TextAlign.Should().Be("center",
            because: "UA stylesheet specifies text-align:center for <th> (HTML5 §14.3.9)");
    }

    /// <summary>G23g: &lt;th style="text-align:left"&gt; → author inline override stays "left".</summary>
    [Fact]
    public void Th_WithInlineTextAlignLeft_AuthorOverridesUa()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var table = new FakeStyledNode("table", new() { ["display"] = "" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        // Simulate inline style: computed style returns "left" for text-align.
        var th = new FakeStyledNode("th", new() { ["display"] = "", ["text-align"] = "left" });
        th.ChildList.Add(TextNode("X"));
        tr.ChildList.Add(th);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        var cells = CollectCells(root);
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));

        thCell.Should().NotBeNull();
        thCell!.TextAlign.Should().Be("left",
            because: "explicit author text-align:left must override UA center for <th>");
    }

    /// <summary>G23g: &lt;th class="text-left"&gt; with .text-left rule → class override stays "left".</summary>
    [Fact]
    public async Task Th_WithClassTextLeft_ClassRuleOverridesUa()
    {
        // Phase 12 B1.3: .text-left resolved through the owned cascade (was the class-rule fallback);
        // a matched author rule outranks the UA <th> center default.
        const string html = """
            <html><head><style>
              .text-left { text-align: left; }
            </style></head>
            <body><table><tbody><tr><th class="text-left">X</th></tr></tbody></table></body></html>
            """;

        var root = await CascadeBoxTree.BuildAsync(html);

        var cells = CollectCells(root);
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));

        thCell.Should().NotBeNull();
        thCell!.TextAlign.Should().Be("left",
            because: "class-rule text-align:left must override UA center for <th>");
    }

    /// <summary>G23g regression guard: &lt;td&gt; must NOT get UA center alignment.</summary>
    [Fact]
    public void Td_NoAuthorTextAlign_CellTextAlignIsNullOrLeft()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var table = new FakeStyledNode("table", new() { ["display"] = "" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var td = new FakeStyledNode("td", new() { ["display"] = "" });
        td.ChildList.Add(TextNode("X"));
        tr.ChildList.Add(td);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        var cells = CollectCells(root);
        var tdCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "td", StringComparison.OrdinalIgnoreCase));

        tdCell.Should().NotBeNull(because: "<td> must produce a TableCellBox");
        tdCell!.TextAlign.Should().NotBe("center",
            because: "UA text-align:center must NOT apply to <td>, only to <th>");
    }

    // -------------------------------------------------------------------------
    // G23h — end-to-end layout: TextAlign propagated through CellContext
    // -------------------------------------------------------------------------

    /// <summary>
    /// End-to-end: TH text positioned at cell horizontal center; TD with .text-center also centered.
    /// Uses TableLayoutEngine directly (bypasses BoxTreeBuilder) to test the propagation in isolation.
    /// </summary>
    [Fact]
    public void Layout_ThCenter_And_TdTextCenter_TextPositionedAtCellCenter()
    {
        // Build the box tree manually so we control TextAlign exactly.
        // TH cell: TextAlign = "center" (UA default as set by G23g)
        var thText = new InlineBox { Text = "H", FontFamily = "serif", FontSize = 10f };
        var thCell = new TableCellBox { TextAlign = "center", Colspan = 1, Rowspan = 1 };
        thCell.Children.Add(thText);

        // TD cell with .text-center: TextAlign = "center" (class-rule hit)
        var tdText = new InlineBox { Text = "D", FontFamily = "serif", FontSize = 10f };
        var tdCell = new TableCellBox { TextAlign = "center", Colspan = 1, Rowspan = 1 };
        tdCell.Children.Add(tdText);

        var tableBox = new TableBox { BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row1 = new TableRowBox();
        row1.Children.Add(thCell);
        var row2 = new TableRowBox();
        row2.Children.Add(tdCell);
        tbody.Children.Add(row1);
        tbody.Children.Add(row2);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 200f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        // Find emitted text elements for TH and TD cells.
        // PositionedElement.Source is the InlineBox for text runs.
        var thPe = output.FirstOrDefault(e => e.Source == thText);
        var tdPe = output.FirstOrDefault(e => e.Source == tdText);

        thPe.Should().NotBeNull(because: "TH text must be laid out");
        tdPe.Should().NotBeNull(because: "TD text must be laid out");

        // The cell occupies the full 200pt available width.
        // For center alignment: wordOffsetX = (cellWidth - wordWidth) / 2 > 0
        // (any non-zero-width word in a wider-than-word cell must be offset right of cellOriginX).
        // ContentOriginX == PageMarginLeftPt == 0 (PdfMargins.Zero).
        // So the word X must be > 0.
        thPe!.Position.X.Should().BeGreaterThan(0f,
            because: "center-aligned TH text must be offset right from the cell's left edge");
        tdPe!.Position.X.Should().BeGreaterThan(0f,
            because: "center-aligned TD text must be offset right from the cell's left edge");
    }

    /// <summary>
    /// Regression: TD with no explicit alignment (TextAlign = null) must be left-aligned
    /// (X position at cell origin, offset == 0 from ContentOriginX == 0).
    /// </summary>
    [Fact]
    public void Layout_TdNoAlignment_TextPositionedAtCellLeft()
    {
        var tdText = new InlineBox { Text = "D", FontFamily = "serif", FontSize = 10f };
        var tdCell = new TableCellBox { TextAlign = null, Colspan = 1, Rowspan = 1 };
        tdCell.Children.Add(tdText);

        var tableBox = new TableBox { BorderSpacing = 0f };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row1 = new TableRowBox();
        row1.Children.Add(tdCell);
        tbody.Children.Add(row1);
        tableBox.Children.Add(tbody);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(availableWidth: 200f);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        var tdPe = output.FirstOrDefault(e => e.Source == tdText);
        tdPe.Should().NotBeNull();
        // Left-aligned: wordOffsetX == 0, so word starts at ContentOriginX == 0 (PdfMargins.Zero).
        tdPe!.Position.X.Should().BeApproximately(0f, precision: 0.5f,
            because: "left-aligned (no TextAlign) TD text must start at the cell's content-left edge");
    }
}
