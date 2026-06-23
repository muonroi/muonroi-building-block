using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

public sealed class FlexBoxTreeTests
{
    private static BoxTreeBuilder Builder() => new();

    // Test 1 — flag ON: display:flex maps to FlexContainerBox.
    [Fact]
    public void Build_DisplayFlex_FlagOn_ProducesFlexContainerBox()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex" });
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<FlexContainerBox>();
    }

    // Test 2 — flag OFF: degrade path preserved, display:flex stays BlockBox.
    [Fact]
    public void Build_DisplayFlex_FlagOff_DegradesToBlockBox()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex" });
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: false);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<BlockBox>();
    }

    // Test 3 — container props resolve (gap 12px → 9pt).
    [Fact]
    public void Build_FlexContainer_ResolvesContainerProps()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new()
        {
            ["display"] = "flex",
            ["flex-direction"] = "column",
            ["flex-wrap"] = "wrap",
            ["justify-content"] = "space-between",
            ["align-items"] = "center",
            ["gap"] = "12px",
        });
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var fc = box.Children.Should().ContainSingle().Which.Should().BeOfType<FlexContainerBox>().Subject;
        fc.FlexDirection.Should().Be("column");
        fc.FlexWrap.Should().Be("wrap");
        fc.JustifyContent.Should().Be("space-between");
        fc.AlignItems.Should().Be("center");
        fc.RowGap.Should().BeApproximately(9f, 0.1f);
        fc.ColumnGap.Should().BeApproximately(9f, 0.1f);
    }

    // Test 4 — flex shorthand on items.
    [Fact]
    public void Build_FlexItem_ShorthandThreeValues_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex" });
        var child = new FakeStyledNode("div", new() { ["display"] = "block", ["flex"] = "1 1 200px" });
        flex.ChildList.Add(child);
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var fc = box.Children[0].Should().BeOfType<FlexContainerBox>().Subject;
        var item = fc.Children.Should().ContainSingle().Which;
        item.FlexGrow.Should().Be(1f);
        item.FlexShrink.Should().Be(1f);
        item.FlexBasisRaw.Should().Be("200px");
    }

    [Fact]
    public void Build_FlexItem_ShorthandSingleNumber_ExpandsToZeroPercentBasis()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex" });
        var child = new FakeStyledNode("div", new() { ["display"] = "block", ["flex"] = "1" });
        flex.ChildList.Add(child);
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var fc = box.Children[0].Should().BeOfType<FlexContainerBox>().Subject;
        var item = fc.Children.Should().ContainSingle().Which;
        item.FlexGrow.Should().Be(1f);
        item.FlexShrink.Should().Be(1f);
        // CSS spec: flex:<number> → <number> 1 0%. Locked literal "0%".
        item.FlexBasisRaw.Should().Be("0%");
    }

    [Fact]
    public void Build_FlexItem_ShorthandZeroZeroFifty_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex" });
        var child = new FakeStyledNode("div", new() { ["display"] = "block", ["flex"] = "0 0 50px" });
        flex.ChildList.Add(child);
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var fc = box.Children[0].Should().BeOfType<FlexContainerBox>().Subject;
        var item = fc.Children.Should().ContainSingle().Which;
        item.FlexGrow.Should().Be(0f);
        item.FlexShrink.Should().Be(0f);
        item.FlexBasisRaw.Should().Be("50px");
    }

    // Test 5 — order + align-self on item, flex-flow shorthand on container.
    [Fact]
    public void Build_FlexItem_OrderAndAlignSelf_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex" });
        var child = new FakeStyledNode("div", new() { ["display"] = "block", ["order"] = "2", ["align-self"] = "flex-end" });
        flex.ChildList.Add(child);
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var fc = box.Children[0].Should().BeOfType<FlexContainerBox>().Subject;
        var item = fc.Children.Should().ContainSingle().Which;
        item.Order.Should().Be(2);
        item.AlignSelf.Should().Be("flex-end");
    }

    [Fact]
    public void Build_FlexContainer_FlexFlowShorthand_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex", ["flex-flow"] = "column wrap" });
        root.ChildList.Add(flex);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var fc = box.Children.Should().ContainSingle().Which.Should().BeOfType<FlexContainerBox>().Subject;
        fc.FlexDirection.Should().Be("column");
        fc.FlexWrap.Should().Be("wrap");
    }
}
