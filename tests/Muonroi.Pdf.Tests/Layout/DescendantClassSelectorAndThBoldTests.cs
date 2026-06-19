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

    // -------------------------------------------------------------------------
    // G25: multi-level descendant selector ".t tr.no-border td { border: none }"
    // must suppress the base ".t td { border: 1px }" on cells of a <tr class="no-border">,
    // while leaving cells of a plain <tr> bordered. Previously the 3-level selector was
    // misfiled as a flat ".t" rule and never reached the <td>, so every cell stayed bordered.
    // -------------------------------------------------------------------------

    [Fact]
    public void DescendantSelector_NoBorderRow_SuppressesBaseCellBorder()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        var styleNode = new FakeStyledNode("style")
        {
            TextContent = ".t td { border: 1px solid #008080; padding: 5px } " +
                          ".t tr.no-border td { border: none }"
        };
        body.ChildList.Add(styleNode);

        var table = new FakeStyledNode("table", new() { ["display"] = "" },
            attributes: new() { ["class"] = "t" });
        var tbody = new FakeStyledNode("tbody", new() { ["display"] = "" });

        // Plain row — its <td> must keep the base 1px border.
        var plainRow = new FakeStyledNode("tr", new() { ["display"] = "" });
        var plainTd = new FakeStyledNode("td", new() { ["display"] = "" });
        plainTd.ChildList.Add(TextNode("A"));
        plainRow.ChildList.Add(plainTd);

        // no-border row — its <td> must have the border suppressed to none.
        var noBorderRow = new FakeStyledNode("tr", new() { ["display"] = "" },
            attributes: new() { ["class"] = "no-border" });
        var noBorderTd = new FakeStyledNode("td", new() { ["display"] = "" });
        noBorderTd.ChildList.Add(TextNode("B"));
        noBorderRow.ChildList.Add(noBorderTd);

        tbody.ChildList.Add(plainRow);
        tbody.ChildList.Add(noBorderRow);
        table.ChildList.Add(tbody);
        body.ChildList.Add(table);

        var root = Builder().Build(body);
        var cells = CollectCells(root);
        cells.Should().HaveCount(2);

        var plainCell = cells.First(c => ReferenceEquals(c.Source, plainTd));
        var noBorderCell = cells.First(c => ReferenceEquals(c.Source, noBorderTd));

        plainCell.BorderTop.Should().BeGreaterThan(0f,
            because: "a plain row's <td> keeps the base '.t td { border: 1px }' border");
        noBorderCell.BorderTop.Should().Be(0f,
            because: "'.t tr.no-border td { border: none }' must suppress the base border (G25)");
        noBorderCell.BorderLeft.Should().Be(0f);
        noBorderCell.BorderRight.Should().Be(0f);
        noBorderCell.BorderBottom.Should().Be(0f);
    }

    // -------------------------------------------------------------------------
    // G27: the 'padding' shorthand fallback must expand 2-value form "V H" into
    // distinct vertical/horizontal padding (previously only the first token was read,
    // so horizontal padding could never exceed vertical on %-width tables).
    // -------------------------------------------------------------------------

    [Fact]
    public void PaddingShorthand_TwoValue_AppliesVerticalAndHorizontalSeparately()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var styleNode = new FakeStyledNode("style")
        {
            TextContent = ".t td { padding: 2px 6px }"
        };
        body.ChildList.Add(styleNode);

        var table = new FakeStyledNode("table", new() { ["display"] = "" },
            attributes: new() { ["class"] = "t" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var td = new FakeStyledNode("td", new() { ["display"] = "" });
        td.ChildList.Add(TextNode("X"));
        tr.ChildList.Add(td);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);
        var cell = CollectCells(root).First(c => ReferenceEquals(c.Source, td));

        // px -> pt is ×0.75: 2px -> 1.5pt, 6px -> 4.5pt.
        cell.PaddingLeft.Should().BeApproximately(4.5f, 0.01f,
            because: "horizontal token (6px) must apply to left/right (G27)");
        cell.PaddingRight.Should().BeApproximately(4.5f, 0.01f);
        cell.PaddingTop.Should().BeApproximately(1.5f, 0.01f,
            because: "vertical token (2px) must apply to top/bottom (G27)");
        cell.PaddingBottom.Should().BeApproximately(1.5f, 0.01f);
        cell.PaddingLeft.Should().BeGreaterThan(cell.PaddingTop,
            because: "horizontal padding must exceed vertical for 'padding: 2px 6px'");
    }

    // -------------------------------------------------------------------------
    // G28: word-break declared via a descendant selector ".t td { word-break: break-word }"
    // must reach a <td> that carries a DIFFERENT own class (e.g. "text-center"). Previously the
    // resolver consulted own-class rules only, so long unbreakable cell values overflowed the
    // column and overlapped neighbouring cells.
    // -------------------------------------------------------------------------

    [Fact]
    public void WordBreak_DescendantSelector_AppliesToCellWithDifferentOwnClass()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var styleNode = new FakeStyledNode("style")
        {
            TextContent = ".t td { word-break: break-word }"
        };
        body.ChildList.Add(styleNode);

        var table = new FakeStyledNode("table", new() { ["display"] = "" },
            attributes: new() { ["class"] = "t" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        // The cell's own class is "text-center", NOT "t" — only the descendant rule matches.
        var td = new FakeStyledNode("td", new() { ["display"] = "" },
            attributes: new() { ["class"] = "text-center" });
        td.ChildList.Add(TextNode("ONES_EAL12133"));
        tr.ChildList.Add(td);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);
        var cell = CollectCells(root).First(c => ReferenceEquals(c.Source, td));

        cell.WordBreak.Should().Be("break-word",
            because: "'.t td { word-break: break-word }' must resolve via the descendant fallback (G28)");
    }

    // -------------------------------------------------------------------------
    // G29: white-space declared via a descendant selector ".t td { white-space: nowrap }"
    // must resolve on the cell (own class differs) AND propagate to inline text children,
    // so a value the author wants on one line is not wrapped.
    // -------------------------------------------------------------------------

    [Fact]
    public void WhiteSpace_DescendantSelector_NowrapResolvesAndPropagates()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var styleNode = new FakeStyledNode("style")
        {
            TextContent = ".t td { white-space: nowrap }"
        };
        body.ChildList.Add(styleNode);

        var table = new FakeStyledNode("table", new() { ["display"] = "" },
            attributes: new() { ["class"] = "t" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "" });
        var td = new FakeStyledNode("td", new() { ["display"] = "" },
            attributes: new() { ["class"] = "text-center" });
        td.ChildList.Add(TextNode("ONEE0000002"));
        tr.ChildList.Add(td);
        table.ChildList.Add(tr);
        body.ChildList.Add(table);

        var root = Builder().Build(body);
        var cell = CollectCells(root).First(c => ReferenceEquals(c.Source, td));

        cell.WhiteSpace.Should().Be("nowrap",
            because: "'.t td { white-space: nowrap }' must resolve via the descendant fallback (G29)");

        var inlines = CollectInlines(cell);
        inlines.Should().NotBeEmpty();
        inlines.Should().AllSatisfy(b => b.WhiteSpace.Should().Be("nowrap",
            because: "white-space must propagate from the cell to inline text children (G29)"));
    }
}
