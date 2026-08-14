namespace Muonroi.Pdf.Tests.Layout;

public sealed class BoxTreeBuilderTests
{
    private static BoxTreeBuilder Builder() => new();

    [Fact]
    public void Build_DisplayNone_NodeExcludedFromTree()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var visible = new FakeStyledNode("p", new() { ["display"] = "block" });
        var hidden = new FakeStyledNode("span", new() { ["display"] = "none" });
        root.ChildList.Add(visible);
        root.ChildList.Add(hidden);

        var box = Builder().Build(root);

        box.Children.Should().HaveCount(1);
        box.Children[0].Should().BeOfType<BlockBox>();
    }

    [Fact]
    public void Build_InlineSiblingAlongsideBlock_WrappedInAnonymousBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var block = new FakeStyledNode("p", new() { ["display"] = "block" });
        var span = new FakeStyledNode("span", new() { ["display"] = "inline" });
        span.TextContent = "text";
        root.ChildList.Add(block);
        root.ChildList.Add(span);

        var box = Builder().Build(root);

        // block-level sibling forces inline to be wrapped in AnonymousBox
        box.Children.Should().HaveCount(2);
        box.Children[0].Should().BeOfType<BlockBox>();
        box.Children[1].Should().BeOfType<AnonymousBox>();
    }

    [Fact]
    public void Build_AllInlineChildren_NoAnonymousBoxCreated()
    {
        var root = new FakeStyledNode("p", new() { ["display"] = "block" });
        root.ChildList.Add(new FakeStyledNode("span", new() { ["display"] = "inline" }));
        root.ChildList.Add(new FakeStyledNode("span", new() { ["display"] = "inline" }));

        var box = Builder().Build(root);

        // All inline siblings: no AnonymousBox wrapping needed
        box.Children.Should().HaveCount(2);
        box.Children.Should().AllBeOfType<InlineBox>();
    }

    // G7 — UA-inline display default tests.
    // AngleSharp returns "" (empty string) for display when no explicit declaration exists.
    // The fix must map known UA-inline tags to InlineBox rather than BlockBox.

    [Fact]
    public void Span_NoExplicitDisplay_ProducesInlineBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var span = new FakeStyledNode("span", new() { ["display"] = "" });
        span.TextContent = "value";
        root.ChildList.Add(span);

        var box = Builder().Build(root);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<InlineBox>();
    }

    [Fact]
    public void Label_NoExplicitDisplay_ProducesInlineBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var label = new FakeStyledNode("label", new() { ["display"] = "" });
        label.TextContent = "Mã lô:";
        root.ChildList.Add(label);

        var box = Builder().Build(root);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<InlineBox>();
    }

    [Fact]
    public void Strong_NoExplicitDisplay_ProducesInlineBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var strong = new FakeStyledNode("strong", new() { ["display"] = "" });
        strong.TextContent = "LO12345";
        root.ChildList.Add(strong);

        var box = Builder().Build(root);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<InlineBox>();
    }

    [Fact]
    public void Em_NoExplicitDisplay_ProducesInlineBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var em = new FakeStyledNode("em", new() { ["display"] = "" });
        em.TextContent = "emphasis";
        root.ChildList.Add(em);

        var box = Builder().Build(root);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<InlineBox>();
    }

    [Fact]
    public void Div_NoExplicitDisplay_ProducesBlockBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var div = new FakeStyledNode("div", new() { ["display"] = "" });
        root.ChildList.Add(div);

        var box = Builder().Build(root);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<BlockBox>();
    }

    [Fact]
    public void P_NoExplicitDisplay_ProducesBlockBox()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var p = new FakeStyledNode("p", new() { ["display"] = "" });
        root.ChildList.Add(p);

        var box = Builder().Build(root);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<BlockBox>();
    }

    // G7b — Mixed text + inline element content tests.
    // Root cause: CollectChildren previously only iterated element children (node.Children
    // element-only API). Text nodes between elements were silently dropped.
    // Fix: AngleSharpStyledNode.Children already yields text nodes; whitespace-only nodes
    // are filtered in CollectChildren; BlockLayoutEngine.BatchInlineChildren groups
    // consecutive InlineBox siblings into one AnonymousBox for same-line flow.

    [Fact]
    public void MixedContent_P_TextThenAnchor_BothChildrenPresent()
    {
        // <p>Mã lô: <a>X</a></p>
        var p = new FakeStyledNode("p", new() { ["display"] = "block" });
        var textNode = new FakeStyledNode("#text") { IsText = true, IsElement = false, TextContent = "Mã lô: " };
        var anchor = new FakeStyledNode("a") { TextContent = "X" };
        p.ChildList.Add(textNode);
        p.ChildList.Add(anchor);

        var pBox = Builder().Build(p);

        pBox.Children.Should().HaveCount(2);
        pBox.Children[0].Should().BeOfType<InlineBox>().Which.Text.Should().Be("Mã lô: ");
        pBox.Children[1].Should().BeOfType<InlineBox>().Which.Text.Should().Be("X");
    }

    [Fact]
    public void MixedContent_P_TextThenStrong_BothChildrenPresent()
    {
        // <p>Label: <strong>Value</strong></p>
        var p = new FakeStyledNode("p", new() { ["display"] = "block" });
        var textNode = new FakeStyledNode("#text") { IsText = true, IsElement = false, TextContent = "Label: " };
        var strong = new FakeStyledNode("strong", new() { ["display"] = "" }) { TextContent = "Value" };
        p.ChildList.Add(textNode);
        p.ChildList.Add(strong);

        var pBox = Builder().Build(p);

        pBox.Children.Should().HaveCount(2);
        pBox.Children[0].Should().BeOfType<InlineBox>().Which.Text.Should().Be("Label: ");
        pBox.Children[1].Should().BeOfType<InlineBox>().Which.Text.Should().Be("Value");
    }

    [Fact]
    public void MixedContent_P_AnchorThenText_BothChildrenPresent()
    {
        // <p><a>X</a> trailing</p>
        var p = new FakeStyledNode("p", new() { ["display"] = "block" });
        var anchor = new FakeStyledNode("a") { TextContent = "X" };
        var textNode = new FakeStyledNode("#text") { IsText = true, IsElement = false, TextContent = " trailing" };
        p.ChildList.Add(anchor);
        p.ChildList.Add(textNode);

        var pBox = Builder().Build(p);

        pBox.Children.Should().HaveCount(2);
        pBox.Children[0].Should().BeOfType<InlineBox>().Which.Text.Should().Be("X");
        pBox.Children[1].Should().BeOfType<InlineBox>().Which.Text.Should().Be(" trailing");
    }

    [Fact]
    public void MixedContent_P_TextOnly_SingleChildOrFallback()
    {
        // <p>plain</p> — regression: pure-text paragraph must still produce content
        var p = new FakeStyledNode("p", new() { ["display"] = "block" });
        var textNode = new FakeStyledNode("#text") { IsText = true, IsElement = false, TextContent = "plain" };
        p.ChildList.Add(textNode);

        var pBox = Builder().Build(p);

        // Should produce exactly one InlineBox child carrying the text.
        pBox.Children.Should().ContainSingle().Which.Should().BeOfType<InlineBox>()
            .Which.Text.Should().Be("plain");
    }

    [Fact]
    public void MixedContent_P_TextOnlyWhitespace_DroppedOrEmpty()
    {
        // <p>   </p> — whitespace-only paragraph must not crash; may produce 0 or 1 children.
        var p = new FakeStyledNode("p", new() { ["display"] = "block" });
        var textNode = new FakeStyledNode("#text") { IsText = true, IsElement = false, TextContent = "   " };
        p.ChildList.Add(textNode);

        // Must not throw; children count is 0 (whitespace dropped if only child is whitespace)
        // or 1 if conservative path keeps it — either is acceptable, just no crash.
        Action act = () => Builder().Build(p);
        act.Should().NotThrow();
    }

    [Fact]
    public void Build_TableCellWithColspan_ColspanReadFromHtmlAttribute()
    {
        // Build a proper table structure: table > tbody > tr > td[colspan=2]
        var table = new FakeStyledNode("table", new() { ["display"] = "table" });
        var tbody = new FakeStyledNode("tbody", new() { ["display"] = "table-row-group" });
        var tr = new FakeStyledNode("tr", new() { ["display"] = "table-row" });
        var td = new FakeStyledNode("td",
            new() { ["display"] = "table-cell" },
            new Dictionary<string, string> { ["colspan"] = "2" });
        tr.ChildList.Add(td);
        tbody.ChildList.Add(tr);
        table.ChildList.Add(tbody);

        // Build(table) → BlockBox containing [TableRowGroupBox[TableRowBox[TableCellBox]]]
        var rootBox = Builder().Build(table);
        var tbodyBox = rootBox.Children[0] as TableRowGroupBox;
        var trBox = tbodyBox!.Children[0] as TableRowBox;
        var cellBox = trBox!.Children[0] as TableCellBox;

        cellBox.Should().NotBeNull();
        cellBox!.Colspan.Should().Be(2);
    }
}
