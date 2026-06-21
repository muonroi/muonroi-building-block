using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

public sealed class GridBoxTreeTests
{
    private static BoxTreeBuilder Builder() => new();

    // Test 1 — flag ON: display:grid maps to GridContainerBox.
    [Fact]
    public void Build_DisplayGrid_FlagOn_ProducesGridContainerBox()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new() { ["display"] = "grid" });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>();
    }

    // Test 2 — flag OFF: degrade path preserved, display:grid stays BlockBox.
    [Fact]
    public void Build_DisplayGrid_FlagOff_DegradesToBlockBox()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new() { ["display"] = "grid" });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: false);

        box.Children.Should().ContainSingle().Which.Should().BeOfType<BlockBox>();
    }

    [Fact]
    public void Build_DisplayInlineGrid_FlagOn_ProducesInlineGridContainerBox()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new() { ["display"] = "inline-grid" });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.IsInlineGrid.Should().BeTrue();
    }

    // Test 3 — track list + gap.
    [Fact]
    public void Build_GridContainer_TrackListAndGap_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["grid-template-columns"] = "100px 1fr 2fr",
            ["gap"] = "8px",
        });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.TemplateColumns.Should().HaveCount(3);
        gc.TemplateColumns[0].Kind.Should().Be(GridTrackKind.Length);
        gc.TemplateColumns[0].Length.Should().BeApproximately(75f, 0.1f);
        gc.TemplateColumns[1].Kind.Should().Be(GridTrackKind.Fraction);
        gc.TemplateColumns[1].Fraction.Should().Be(1f);
        gc.TemplateColumns[2].Kind.Should().Be(GridTrackKind.Fraction);
        gc.TemplateColumns[2].Fraction.Should().Be(2f);
        gc.RowGap.Should().BeApproximately(6f, 0.1f);
        gc.ColumnGap.Should().BeApproximately(6f, 0.1f);
    }

    // Test 4 — repeat + minmax.
    [Fact]
    public void Build_GridContainer_RepeatAndMinMax_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["grid-template-columns"] = "repeat(3, 1fr)",
            ["grid-template-rows"] = "minmax(50px, 1fr)",
        });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.TemplateColumns.Should().HaveCount(3);
        gc.TemplateColumns.Should().OnlyContain(t => t.Kind == GridTrackKind.Fraction && t.Fraction == 1f);

        gc.TemplateRows.Should().ContainSingle();
        var mm = gc.TemplateRows[0];
        mm.Kind.Should().Be(GridTrackKind.MinMax);
        mm.Min!.Kind.Should().Be(GridTrackKind.Length);
        mm.Min.Length.Should().BeApproximately(37.5f, 0.1f);
        mm.Max!.Kind.Should().Be(GridTrackKind.Fraction);
        mm.Max.Fraction.Should().Be(1f);
    }

    // Test 5 — auto-flow + auto-rows; trailing "dense" stripped.
    [Fact]
    public void Build_GridContainer_AutoFlowAndAutoRows_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var gridCol = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["grid-auto-flow"] = "column",
            ["grid-auto-rows"] = "40px",
        });
        root.ChildList.Add(gridCol);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.AutoFlow.Should().Be("column");
        gc.AutoRows!.Kind.Should().Be(GridTrackKind.Length);
        gc.AutoRows.Length.Should().BeApproximately(30f, 0.1f);
    }

    [Fact]
    public void Build_GridContainer_AutoFlowRowDense_StripsDense()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["grid-auto-flow"] = "row dense",
        });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.AutoFlow.Should().Be("row");
    }

    // Test 6 — template-areas.
    [Fact]
    public void Build_GridContainer_TemplateAreas_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["grid-template-areas"] = "\"head head\" \"nav main\"",
        });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.TemplateAreas.Should().HaveCount(2);
        gc.TemplateAreas[0].Should().Equal("head", "head");
        gc.TemplateAreas[1].Should().Equal("nav", "main");
    }

    // T-19-05 — ragged template-areas fall back to empty (no out-of-bounds cell math).
    [Fact]
    public void Build_GridContainer_RaggedTemplateAreas_FallsBackToEmpty()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["grid-template-areas"] = "\"head head\" \"nav\"",
        });
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children.Should().ContainSingle().Which.Should().BeOfType<GridContainerBox>().Subject;
        gc.TemplateAreas.Should().BeEmpty();
    }

    // Test 7 — item placement shorthands.
    [Fact]
    public void Build_GridItem_PlacementShorthands_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new() { ["display"] = "grid" });
        var child = new FakeStyledNode("div", new()
        {
            ["display"] = "block",
            ["grid-column"] = "1 / 3",
            ["grid-row"] = "span 2",
            ["justify-self"] = "center",
        });
        grid.ChildList.Add(child);
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children[0].Should().BeOfType<GridContainerBox>().Subject;
        var item = gc.Children.Should().ContainSingle().Which;
        item.GridColumnRaw.Should().Be("1 / 3");
        item.GridRowRaw.Should().Be("span 2");
        item.JustifySelf.Should().Be("center");
    }

    [Fact]
    public void Build_GridItem_GridArea_Resolved()
    {
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new() { ["display"] = "grid" });
        var child = new FakeStyledNode("div", new() { ["display"] = "block", ["grid-area"] = "main" });
        grid.ChildList.Add(child);
        root.ChildList.Add(grid);

        var box = Builder().Build(root, null, allowModernLayout: true);

        var gc = box.Children[0].Should().BeOfType<GridContainerBox>().Subject;
        var item = gc.Children.Should().ContainSingle().Which;
        item.GridAreaRaw.Should().Be("main");
    }

    // T-19-04 — repeat() count is clamped (no unbounded allocation).
    [Fact]
    public void ParseTrackList_RepeatHostileCount_ClampedToMax()
    {
        var tracks = GridTrack.ParseTrackList("repeat(99999999, 1fr)", 12f);
        tracks.Should().HaveCount(GridTrack.MaxRepeatCount);
        tracks.Should().OnlyContain(t => t.Kind == GridTrackKind.Fraction && t.Fraction == 1f);
    }

    // D-01 — auto-fill / auto-fit (non-integer first arg) is out of scope → repeat skipped.
    [Fact]
    public void ParseTrackList_RepeatAutoFill_Skipped()
    {
        var tracks = GridTrack.ParseTrackList("repeat(auto-fill, 1fr)", 12f);
        tracks.Should().BeEmpty();
    }

    // T-19-04 — malformed track tokens degrade to Auto, never throw.
    [Fact]
    public void ParseTrackList_MalformedToken_DegradesToAuto()
    {
        var tracks = GridTrack.ParseTrackList("garbage auto 1fr", 12f);
        tracks.Should().HaveCount(3);
        tracks[0].Kind.Should().Be(GridTrackKind.Auto);
        tracks[1].Kind.Should().Be(GridTrackKind.Auto);
        tracks[2].Kind.Should().Be(GridTrackKind.Fraction);
    }
}
