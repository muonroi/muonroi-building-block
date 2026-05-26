using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Helpers;

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
