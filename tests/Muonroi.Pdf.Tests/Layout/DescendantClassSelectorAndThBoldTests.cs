using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G23c + G23d regression tests.
/// G23c: ".cls th, .cls td { border: Npx solid; padding: Npx }" descendant-selector rules
///       must apply to TH/TD cells even though the cells themselves carry no class attribute.
/// G23d: &lt;th&gt; must default to Bold=true via UA stylesheet (same as h1-h6).
/// </summary>
public sealed class DescendantClassSelectorAndThBoldTests
{
    private static BoxTreeBuilder Builder() => new();

    // Helper: create a text node (IsText=true, no styles).
    private static FakeStyledNode TextNode(string content) =>
        new("#text") { IsText = true, IsElement = false, TextContent = content };

    // Helper: flatten all TableCellBox descendants from a box tree node.
    private static List<TableCellBox> CollectCells(BoxNode root)
    {
        var result = new List<TableCellBox>();
        CollectCellsRecursive(root, result);
        return result;
    }

    private static void CollectCellsRecursive(BoxNode node, List<TableCellBox> result)
    {
        if (node is TableCellBox cell) result.Add(cell);
        foreach (var child in node.Children)
            CollectCellsRecursive(child, result);
    }

    // Helper: flatten all InlineBox descendants.
    private static List<InlineBox> CollectInlines(BoxNode root)
    {
        var result = new List<InlineBox>();
        CollectInlinesRecursive(root, result);
        return result;
    }

    private static void CollectInlinesRecursive(BoxNode node, List<InlineBox> result)
    {
        if (node is InlineBox inline) result.Add(inline);
        foreach (var child in node.Children)
            CollectInlinesRecursive(child, result);
    }

    // -------------------------------------------------------------------------
    // Main case: ".t th, .t td { border: 1px solid red; padding: 5px }"
    // <table class="t"><thead><tr><th>H</th></tr></thead><tbody><tr><td>D</td></tr></tbody></table>
    // -------------------------------------------------------------------------

    [Fact]
    public void DescendantSelector_ThAndTd_BorderAndPaddingApplied()
    {
        // Arrange: body > style + table.t > thead/tbody > tr > th/td
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        // <style> block — CSS uses descendant selectors
        var styleNode = new FakeStyledNode("style")
        {
            TextContent = ".t th, .t td { border: 1px solid red; padding: 5px }"
        };
        body.ChildList.Add(styleNode);

        // Build table DOM: computed styles are empty (simulating AngleSharp failure path).
        // CreateBox uses the UA display-mapping fallback for table structural elements.
        var table = new FakeStyledNode("table", new() { ["display"] = "" },
            attributes: new() { ["class"] = "t" });
        var thead = new FakeStyledNode("thead", new() { ["display"] = "" });
        var tbody = new FakeStyledNode("tbody", new() { ["display"] = "" });
        var theadRow = new FakeStyledNode("tr", new() { ["display"] = "" });
        var tbodyRow = new FakeStyledNode("tr", new() { ["display"] = "" });

        var th = new FakeStyledNode("th", new() { ["display"] = "" });
        th.ChildList.Add(TextNode("H"));

        var td = new FakeStyledNode("td", new() { ["display"] = "" });
        td.ChildList.Add(TextNode("D"));

        theadRow.ChildList.Add(th);
        thead.ChildList.Add(theadRow);
        tbodyRow.ChildList.Add(td);
        tbody.ChildList.Add(tbodyRow);
        table.ChildList.Add(thead);
        table.ChildList.Add(tbody);
        body.ChildList.Add(table);

        // Act
        var root = Builder().Build(body);

        // Assert: collect both cells
        var cells = CollectCells(root);
        cells.Should().HaveCount(2, because: "one TH and one TD must produce TableCellBoxes");

        // Find TH and TD cells by their source element tag
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));
        var tdCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "td", StringComparison.OrdinalIgnoreCase));

        thCell.Should().NotBeNull(because: "<th> must produce a TableCellBox");
        tdCell.Should().NotBeNull(because: "<td> must produce a TableCellBox");

        // G23c: TH cell must have border applied via descendant-selector rule ".t th"
        thCell!.BorderTop.Should().BeGreaterThan(0f,
            because: ".t th { border: 1px solid red } must set BorderTop on the TH cell");
        thCell.BorderRight.Should().BeGreaterThan(0f,
            because: ".t th { border } must set BorderRight on the TH cell");
        thCell.BorderBottom.Should().BeGreaterThan(0f,
            because: ".t th { border } must set BorderBottom on the TH cell");
        thCell.BorderLeft.Should().BeGreaterThan(0f,
            because: ".t th { border } must set BorderLeft on the TH cell");

        // G23c: TD cell must also have border applied via descendant-selector rule ".t td"
        tdCell!.BorderTop.Should().BeGreaterThan(0f,
            because: ".t td { border: 1px solid red } must set BorderTop on the TD cell");
        tdCell.BorderRight.Should().BeGreaterThan(0f,
            because: ".t td { border } must set BorderRight on the TD cell");
        tdCell.BorderBottom.Should().BeGreaterThan(0f,
            because: ".t td { border } must set BorderBottom on the TD cell");
        tdCell.BorderLeft.Should().BeGreaterThan(0f,
            because: ".t td { border } must set BorderLeft on the TD cell");

        // G23c: padding must also be applied
        thCell.PaddingTop.Should().BeGreaterThan(0f,
            because: ".t th { padding: 5px } must set PaddingTop on the TH cell");
        tdCell.PaddingTop.Should().BeGreaterThan(0f,
            because: ".t td { padding: 5px } must set PaddingTop on the TD cell");
    }

    // -------------------------------------------------------------------------
    // G23d: <th> UA bold default — text run inside <th> must be Bold=true.
    // -------------------------------------------------------------------------

    [Fact]
    public void Th_NoExplicitFontWeight_InlineChildGetsBoldFromUa()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        var table = new FakeStyledNode("table", new() { ["display"] = "" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var th = new FakeStyledNode("th", new() { ["display"] = "" });
        th.ChildList.Add(TextNode("Header"));

        tr.ChildList.Add(th);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        // Find the TH cell box
        var cells = CollectCells(root);
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));
        thCell.Should().NotBeNull(because: "<th> must produce a TableCellBox");
        thCell!.Bold.Should().BeTrue(because: "UA stylesheet gives <th> font-weight:bold (G23d)");

        // Its inline text child must also be Bold via PropagateInheritedTextProps
        var inlines = CollectInlines(thCell);
        inlines.Should().NotBeEmpty(because: "text node inside <th> must produce an InlineBox");
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeTrue(because: "inline text inside <th> inherits UA bold"));
    }

    // -------------------------------------------------------------------------
    // Regression guard: <td> must NOT get UA bold (bold is only for <th>).
    // -------------------------------------------------------------------------

    [Fact]
    public void Td_NoExplicitFontWeight_InlineChildIsNotBold()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        var table = new FakeStyledNode("table", new() { ["display"] = "" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var td = new FakeStyledNode("td", new() { ["display"] = "" });
        td.ChildList.Add(TextNode("Data"));

        tr.ChildList.Add(td);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        var cells = CollectCells(root);
        var tdCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "td", StringComparison.OrdinalIgnoreCase));
        tdCell.Should().NotBeNull(because: "<td> must produce a TableCellBox");

        // UA bold must NOT apply to <td>
        var inlines = CollectInlines(tdCell!);
        inlines.Should().NotBeEmpty();
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeFalse(because: "UA bold does NOT apply to <td>, only <th>"));
    }

    // -------------------------------------------------------------------------
    // Author override beats UA bold for <th>.
    // -------------------------------------------------------------------------

    [Fact]
    public void Th_WithExplicitFontWeightNormal_InlineChildIsNotBold()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        var table = new FakeStyledNode("table", new() { ["display"] = "" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        // author-level font-weight:normal overrides UA bold
        var th = new FakeStyledNode("th", new() { ["display"] = "", ["font-weight"] = "normal" });
        th.ChildList.Add(TextNode("Header"));

        tr.ChildList.Add(th);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        var cells = CollectCells(root);
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));
        thCell.Should().NotBeNull();
        thCell!.Bold.Should().BeFalse(
            because: "explicit font-weight:normal must override UA bold for <th>");

        var inlines = CollectInlines(thCell);
        inlines.Should().NotBeEmpty();
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeFalse(because: "author font-weight:normal propagates to inline children"));
    }

    // -------------------------------------------------------------------------
    // Child combinator (.cls > TAG) must be treated same as descendant combinator.
    // Note: compound selectors (.a.b th, .a th.b) are NOT guaranteed to be handled;
    // only the simple ".cls > TAG" form is covered here.
    // -------------------------------------------------------------------------

    [Fact]
    public void DirectChildCombinator_ThBorder_AppliedViaDescendantFallback()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        var styleNode = new FakeStyledNode("style")
        {
            // Uses '>' child combinator — should be normalised to descendant rule
            TextContent = ".grid > th { border: 2px solid blue }"
        };
        body.ChildList.Add(styleNode);

        var table = new FakeStyledNode("table", new() { ["display"] = "" },
            attributes: new() { ["class"] = "grid" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var th = new FakeStyledNode("th", new() { ["display"] = "" });
        th.ChildList.Add(TextNode("H"));

        tr.ChildList.Add(th);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);

        var cells = CollectCells(root);
        var thCell = cells.FirstOrDefault(c =>
            string.Equals(c.Source?.LocalName, "th", StringComparison.OrdinalIgnoreCase));
        thCell.Should().NotBeNull();
        thCell!.BorderTop.Should().BeGreaterThan(0f,
            because: ".grid > th { border: 2px solid blue } must apply via child-combinator descendant rule");
    }
}
