namespace Muonroi.Pdf.Tests.Layout;

// WAVE-3 SMOKE GATE (19-03): two operand-value assertions proving the grid engine (a) places
// items into distinct column cells left-to-right (column-placement canary) and (b) distributes
// remaining free space across fr tracks proportionally (fr-distribution canary). Either failing
// means the engine reported build-green but is logic-broken. The full GRID-06 position-value
// suite (minmax/repeat/named-areas/auto-flow/span/nested) lands in Plan 04.
public sealed class GridLayoutEngineSmokeTests
{
    // Wire the engines explicitly (mirrors LayoutEngine ctor post-construction wiring).
    private static BlockLayoutEngine BuildWiredEngine()
    {
        var be = new BlockLayoutEngine();
        be.TableEngine = new TableLayoutEngine(be, be.InlineEngine);
        be.FlexEngine = new FlexLayoutEngine(be);
        be.GridEngine = new GridLayoutEngine(be);
        return be;
    }

    [Fact]
    public void Grid_ThreeFixedColumns_PlacesChildrenLeftToRight()
    {
        // display:grid, width:300px, grid-template-columns:100px 100px 100px, three children, no gap.
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["width"] = "300px",
            ["grid-template-columns"] = "100px 100px 100px",
        });
        var c1 = new FakeStyledNode("div", new() { ["display"] = "block" });
        var c2 = new FakeStyledNode("div", new() { ["display"] = "block" });
        var c3 = new FakeStyledNode("div", new() { ["display"] = "block" });
        grid.ChildList.Add(c1);
        grid.ChildList.Add(c2);
        grid.ChildList.Add(c3);
        root.ChildList.Add(grid);

        var rootBox = new BoxTreeBuilder().Build(root, null, allowModernLayout: true);

        var be = BuildWiredEngine();
        var ctx = new LayoutContext
        {
            PageWidth = 595f,
            PageHeight = 842f,
            AvailableWidth = 595f,
            CurrentY = 0f,
            PageMargins = PdfMargins.Zero,
        };
        var output = new List<PositionedElement>();
        be.Layout(rootBox, ctx, output, pageIndex: 0, isRoot: true);

        var gc = rootBox.Children.OfType<GridContainerBox>().Single();
        var pe1 = output.Single(e => ReferenceEquals(e.Source, gc.Children[0]));
        var pe2 = output.Single(e => ReferenceEquals(e.Source, gc.Children[1]));
        var pe3 = output.Single(e => ReferenceEquals(e.Source, gc.Children[2]));

        // 100px → 75pt. Left-to-right column placement: 2nd cell at +100px, 3rd at +200px.
        float hundredPx = 100f * (float)Units.PxToPt;
        pe2.Position.X.Should().BeApproximately(pe1.Position.X + hundredPx, 0.5f);
        pe3.Position.X.Should().BeApproximately(pe1.Position.X + 2f * hundredPx, 0.5f);
    }

    [Fact]
    public void Grid_FrColumns_DistributesFreeSpaceProportionally()
    {
        // display:grid, width:300px, grid-template-columns:1fr 2fr, two children, no gap.
        // Free space 300px split 1:2 → first cell 100px, second cell 200px.
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var grid = new FakeStyledNode("div", new()
        {
            ["display"] = "grid",
            ["width"] = "300px",
            ["grid-template-columns"] = "1fr 2fr",
        });
        var c1 = new FakeStyledNode("div", new() { ["display"] = "block" });
        var c2 = new FakeStyledNode("div", new() { ["display"] = "block" });
        grid.ChildList.Add(c1);
        grid.ChildList.Add(c2);
        root.ChildList.Add(grid);

        var rootBox = new BoxTreeBuilder().Build(root, null, allowModernLayout: true);

        var be = BuildWiredEngine();
        var ctx = new LayoutContext
        {
            PageWidth = 595f,
            PageHeight = 842f,
            AvailableWidth = 595f,
            CurrentY = 0f,
            PageMargins = PdfMargins.Zero,
        };
        var output = new List<PositionedElement>();
        be.Layout(rootBox, ctx, output, pageIndex: 0, isRoot: true);

        var gc = rootBox.Children.OfType<GridContainerBox>().Single();
        var pe1 = output.Single(e => ReferenceEquals(e.Source, gc.Children[0]));
        var pe2 = output.Single(e => ReferenceEquals(e.Source, gc.Children[1]));

        // 100px → 75pt, 200px → 150pt.
        float hundredPx = 100f * (float)Units.PxToPt;
        float twoHundredPx = 200f * (float)Units.PxToPt;
        pe1.Position.Width.Should().BeApproximately(hundredPx, 0.5f);
        pe2.Position.Width.Should().BeApproximately(twoHundredPx, 0.5f);
    }
}
