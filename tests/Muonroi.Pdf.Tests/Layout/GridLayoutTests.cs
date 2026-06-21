using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// GRID-06 operand-value position / track-size assertions for the CSS Grid layout engine. Each test
/// builds a <see cref="GridContainerBox"/> via <see cref="BoxTreeBuilder"/> (allowModernLayout:true),
/// runs <see cref="GridLayoutEngine"/> directly, and asserts <see cref="PositionedElement"/>.Position
/// X/Y/W/H (resolved cell rects / track sizes) by VALUE — never just a non-throwing render (per
/// memory pdf_phase15_radial_affine: a green non-throwing render once hid a real layout bug).
/// Units: 1 CSS px = <see cref="Units.PxToPt"/> (0.75pt); grid-template px literals are resolved to
/// pt at box-tree-build time, so a <c>100px</c> track is a 75pt track.
/// </summary>
public sealed class GridLayoutTests
{
    private const float Px = Units.PxToPt; // 0.75

    private static LayoutContext MakeContext(float availableWidth = 1000f, float pageHeight = 1000f) =>
        new()
        {
            PageWidth = availableWidth,
            PageHeight = pageHeight,
            AvailableWidth = availableWidth,
            CurrentY = 0f,
            CurrentPageIndex = 0,
            TotalPages = 0,
            TextMetrics = EstimatedTextMetrics.Instance,
            PageMargins = PdfMargins.Zero,
        };

    // Build a grid container box tree, then run the GridLayoutEngine directly so positions are
    // asserted straight off PositionedElement (originX = 0 with PdfMargins.Zero / no ContentOriginX).
    private static (GridContainerBox container, List<PositionedElement> elements) RunGrid(
        FakeStyledNode containerNode, float availableWidth = 1000f, float pageHeight = 1000f)
    {
        // BoxTreeBuilder.Build() materializes the ROOT node directly as a BlockBox (display is not
        // consulted for the root). Only CHILD nodes pass through CreateBox where display:grid maps to
        // GridContainerBox — so wrap the grid node in a plain block parent.
        var parent = new FakeStyledNode("div", new() { ["display"] = "block" });
        parent.ChildList.Add(containerNode);
        var root = new BoxTreeBuilder().Build(parent, null, allowModernLayout: true);
        var container = FindGrid(root) ?? throw new System.InvalidOperationException(
            "BoxTreeBuilder did not produce a GridContainerBox — check display:grid + allowModernLayout.");

        var be = BuildWiredEngine();
        var elements = new List<PositionedElement>();
        be.GridEngine!.Layout(container, MakeContext(availableWidth, pageHeight), elements, 0);
        return (container, elements);
    }

    private static BlockLayoutEngine BuildWiredEngine()
    {
        var be = new BlockLayoutEngine();
        be.TableEngine = new TableLayoutEngine(be, be.InlineEngine);
        be.FlexEngine = new FlexLayoutEngine(be);
        be.GridEngine = new GridLayoutEngine(be);
        return be;
    }

    private static GridContainerBox? FindGrid(BoxNode node)
    {
        if (node is GridContainerBox gc) return gc;
        foreach (var child in node.Children)
        {
            var found = FindGrid(child);
            if (found != null) return found;
        }

        return null;
    }

    private static GridContainerBox? FindGridBelow(BoxNode node)
    {
        foreach (var child in node.Children)
        {
            var found = FindGrid(child);
            if (found != null) return found;
        }

        return null;
    }

    private static FakeStyledNode Grid(Dictionary<string, string> style, params FakeStyledNode[] children)
    {
        style["display"] = style.GetValueOrDefault("display", "grid");
        var node = new FakeStyledNode("div", style);
        foreach (var c in children) node.ChildList.Add(c);
        return node;
    }

    private static FakeStyledNode Item(Dictionary<string, string> style, string? text = null)
    {
        style["display"] = style.GetValueOrDefault("display", "block");
        var node = new FakeStyledNode("div", style);
        if (text != null)
        {
            var t = new FakeStyledNode("#text") { IsElement = false, IsText = true, TextContent = text };
            node.ChildList.Add(t);
        }

        return node;
    }

    private static PositionedElement For(List<PositionedElement> elements, BoxNode source) =>
        elements.First(e => ReferenceEquals(e.Source, source));

    // --- FixedTracks: width:300px, 3×100px columns, 3 children → X at 0, 75pt, 150pt -------------
    [Fact]
    public void FixedTracks_PositionItemsIntoColumnCells()
    {
        var c1 = Item(new());
        var c2 = Item(new());
        var c3 = Item(new());
        var (container, elements) = RunGrid(
            Grid(new() { ["width"] = "300px", ["grid-template-columns"] = "100px 100px 100px" }, c1, c2, c3));

        var b1 = container.Children[0];
        var b2 = container.Children[1];
        var b3 = container.Children[2];

        For(elements, b1).Position.X.Should().BeApproximately(0f, 0.1f);
        For(elements, b2).Position.X.Should().BeApproximately(100f * Px, 0.1f);
        For(elements, b3).Position.X.Should().BeApproximately(200f * Px, 0.1f);

        For(elements, b1).Position.Width.Should().BeApproximately(100f * Px, 0.1f);
        For(elements, b2).Position.Width.Should().BeApproximately(100f * Px, 0.1f);
        For(elements, b3).Position.Width.Should().BeApproximately(100f * Px, 0.1f);

        float y = For(elements, b1).Position.Y;
        For(elements, b2).Position.Y.Should().BeApproximately(y, 0.1f);
        For(elements, b3).Position.Y.Should().BeApproximately(y, 0.1f);
    }

    // --- FrDistribution: width:300px, 1fr 2fr → first cell 100px, second cell 200px -------------
    [Fact]
    public void FrDistribution_SplitsFreeSpace()
    {
        var c1 = Item(new());
        var c2 = Item(new());
        var (container, elements) = RunGrid(
            Grid(new() { ["width"] = "300px", ["grid-template-columns"] = "1fr 2fr" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        For(elements, b1).Position.Width.Should().BeApproximately(100f * Px, 0.5f);
        For(elements, b2).Position.Width.Should().BeApproximately(200f * Px, 0.5f);
        For(elements, b2).Position.X.Should().BeApproximately(
            For(elements, b1).Position.X + For(elements, b1).Position.Width, 0.5f);
    }

    // --- Minmax: floor wins when overflowed, max(1fr) wins when there is room --------------------
    [Fact]
    public void Minmax_ClampsTrackSize()
    {
        // width:80px (60pt) with two minmax(50px,1fr): 2×37.5pt floor = 75pt > 60pt → each clamps to
        // its 50px (37.5pt) min floor (no positive free space to distribute).
        var f1 = Item(new());
        var f2 = Item(new());
        var (fc, fe) = RunGrid(
            Grid(new() { ["width"] = "80px", ["grid-template-columns"] = "minmax(50px,1fr) minmax(50px,1fr)" }, f1, f2));
        For(fe, fc.Children[0]).Position.Width.Should().BeApproximately(50f * Px, 0.5f);
        For(fe, fc.Children[1]).Position.Width.Should().BeApproximately(50f * Px, 0.5f);

        // width:300px (225pt): 1fr max wins, free space 225pt split 1:1 → each track 150px (112.5pt).
        var g1 = Item(new());
        var g2 = Item(new());
        var (gc, ge) = RunGrid(
            Grid(new() { ["width"] = "300px", ["grid-template-columns"] = "minmax(50px,1fr) minmax(50px,1fr)" }, g1, g2));
        For(ge, gc.Children[0]).Position.Width.Should().BeApproximately(150f * Px, 0.5f);
        For(ge, gc.Children[1]).Position.Width.Should().BeApproximately(150f * Px, 0.5f);
    }

    // --- Repeat: repeat(3,1fr) width:300px → three 100px cells; third.X = 200px ------------------
    [Fact]
    public void Repeat_ExpandsTracks()
    {
        var c1 = Item(new());
        var c2 = Item(new());
        var c3 = Item(new());
        var (container, elements) = RunGrid(
            Grid(new() { ["width"] = "300px", ["grid-template-columns"] = "repeat(3,1fr)" }, c1, c2, c3));

        var b1 = container.Children[0];
        var b3 = container.Children[2];

        For(elements, b1).Position.Width.Should().BeApproximately(100f * Px, 0.5f);
        For(elements, b3).Position.Width.Should().BeApproximately(100f * Px, 0.5f);
        For(elements, b3).Position.X.Should().BeApproximately(
            For(elements, b1).Position.X + 200f * Px, 0.5f);
    }

    // --- Gap: width:300px, 100px 100px, column-gap:20px → second.X = first + 100px + 20px --------
    [Fact]
    public void Gap_AddsBetweenTracks()
    {
        var c1 = Item(new());
        var c2 = Item(new());
        var (container, elements) = RunGrid(
            Grid(new()
            {
                ["width"] = "300px",
                ["grid-template-columns"] = "100px 100px",
                ["column-gap"] = "20px",
            }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        For(elements, b2).Position.X.Should().BeApproximately(
            For(elements, b1).Position.X + 100f * Px + 20f * Px, 0.5f);
    }

    // --- ExplicitLinePlacement: grid-column:"2 / 3" → child placed in column 2 (X = 100px) -------
    [Fact]
    public void ExplicitLinePlacement_PlacesItemAtLine()
    {
        var c1 = Item(new() { ["grid-column"] = "2 / 3" });
        var (container, elements) = RunGrid(
            Grid(new() { ["grid-template-columns"] = "repeat(3,100px)" }, c1));

        var b1 = container.Children[0];
        For(elements, b1).Position.X.Should().BeApproximately(100f * Px, 0.5f);
        For(elements, b1).Position.Width.Should().BeApproximately(100f * Px, 0.5f);
    }

    // --- SpanPlacement: grid-column:"span 2" → cell width spans two 100px tracks (200px) ---------
    [Fact]
    public void SpanPlacement_SpansTracks()
    {
        var c1 = Item(new() { ["grid-column"] = "1 / span 2" });
        var (container, elements) = RunGrid(
            Grid(new() { ["grid-template-columns"] = "repeat(3,100px)" }, c1));

        var b1 = container.Children[0];
        For(elements, b1).Position.X.Should().BeApproximately(0f, 0.5f);
        For(elements, b1).Position.Width.Should().BeApproximately(200f * Px, 0.5f);
    }

    // --- AutoPlacementRow: repeat(2,100px) auto-flow:row, 3 children → child3 wraps to row 2 -----
    [Fact]
    public void AutoPlacementRow_WrapsToNextRow()
    {
        var c1 = Item(new() { ["height"] = "40px" });
        var c2 = Item(new() { ["height"] = "40px" });
        var c3 = Item(new() { ["height"] = "40px" });
        var (container, elements) = RunGrid(
            Grid(new()
            {
                ["grid-template-columns"] = "repeat(2,100px)",
                ["grid-auto-flow"] = "row",
            }, c1, c2, c3));

        var b1 = container.Children[0];
        var b2 = container.Children[1];
        var b3 = container.Children[2];

        For(elements, b1).Position.X.Should().BeApproximately(0f, 0.5f);
        For(elements, b2).Position.X.Should().BeApproximately(100f * Px, 0.5f);
        For(elements, b2).Position.Y.Should().BeApproximately(For(elements, b1).Position.Y, 0.5f);

        For(elements, b3).Position.X.Should().BeApproximately(0f, 0.5f,
            because: "the third item wraps to column 1 of row 2 in row-flow");
        For(elements, b3).Position.Y.Should().BeGreaterThan(For(elements, b1).Position.Y,
            because: "the wrapped item is on a lower row");
    }

    // --- AutoPlacementColumn: rows repeat(2,40px) auto-flow:column, 3 children -------------------
    [Fact]
    public void AutoPlacementColumn_WrapsToNextColumn()
    {
        var c1 = Item(new() { ["width"] = "100px" });
        var c2 = Item(new() { ["width"] = "100px" });
        var c3 = Item(new() { ["width"] = "100px" });
        var (container, elements) = RunGrid(
            Grid(new()
            {
                ["grid-template-rows"] = "repeat(2,40px)",
                ["grid-auto-flow"] = "column",
            }, c1, c2, c3));

        var b1 = container.Children[0];
        var b2 = container.Children[1];
        var b3 = container.Children[2];

        // child1 row1 col1; child2 row2 col1 (same X, greater Y); child3 wraps to col2 row1 (X > child1).
        For(elements, b2).Position.X.Should().BeApproximately(For(elements, b1).Position.X, 0.5f);
        For(elements, b2).Position.Y.Should().BeGreaterThan(For(elements, b1).Position.Y,
            because: "column-flow advances down the rows of the first column before wrapping");
        For(elements, b3).Position.X.Should().BeGreaterThan(For(elements, b1).Position.X,
            because: "the third item wraps to the next column");
        For(elements, b3).Position.Y.Should().BeApproximately(For(elements, b1).Position.Y, 0.5f);
    }

    // --- NamedAreas: "head head"/"nav main" → head spans both cols of row1; main = col2 row2 -----
    [Fact]
    public void NamedAreas_PlaceItemsByArea()
    {
        var head = Item(new() { ["grid-area"] = "head" });
        var main = Item(new() { ["grid-area"] = "main" });
        var (container, elements) = RunGrid(
            Grid(new()
            {
                ["grid-template-areas"] = "\"head head\" \"nav main\"",
                ["grid-template-columns"] = "100px 100px",
                ["grid-template-rows"] = "40px 40px",
            }, head, main));

        var headBox = container.Children[0];
        var mainBox = container.Children[1];

        // head spans both columns of row 1 → width 200px, Y at 0.
        For(elements, headBox).Position.Width.Should().BeApproximately(200f * Px, 0.5f);
        For(elements, headBox).Position.Y.Should().BeApproximately(0f, 0.5f);

        // main is column 2, row 2 → X at 100px, Y at 40px.
        For(elements, mainBox).Position.X.Should().BeApproximately(100f * Px, 0.5f);
        For(elements, mainBox).Position.Y.Should().BeApproximately(40f * Px, 0.5f);
    }

    // --- JustifySelf: item width:40px in a 100px track, justify-self:center → centered X ---------
    [Fact]
    public void JustifySelf_CentersItemWithinCell()
    {
        var c1 = Item(new() { ["width"] = "40px", ["justify-self"] = "center" });
        var (container, elements) = RunGrid(
            Grid(new() { ["grid-template-columns"] = "100px" }, c1));

        var b1 = container.Children[0];
        // cell X = 0, cell width 100px, item width 40px → centered offset = (100-40)/2 = 30px.
        For(elements, b1).Position.X.Should().BeApproximately(30f * Px, 0.5f);
        For(elements, b1).Position.Width.Should().BeApproximately(40f * Px, 0.5f);
    }

    // --- NestedGrid: inner grid grandchildren offset by the outer cell's X (recursion via dispatch)
    [Fact]
    public void NestedGrid_Composes()
    {
        var grand1 = Item(new() { ["width"] = "30px" });
        var grand2 = Item(new() { ["width"] = "30px" });
        var inner = Grid(new() { ["display"] = "grid", ["width"] = "200px", ["grid-template-columns"] = "100px 100px" },
            grand1, grand2);

        // outer: first cell is a 100px spacer (column 1), second cell is the inner grid (column 2).
        var spacer = Item(new() { ["width"] = "100px" });
        var outerNode = Grid(new() { ["grid-template-columns"] = "100px 200px" }, spacer, inner);

        var wrapper = new FakeStyledNode("div", new() { ["display"] = "block" });
        wrapper.ChildList.Add(outerNode);
        var root = new BoxTreeBuilder().Build(wrapper, null, allowModernLayout: true);
        var outer = FindGrid(root)!;

        var be = BuildWiredEngine();
        var elements = new List<PositionedElement>();
        be.GridEngine!.Layout(outer, MakeContext(), elements, 0);

        var innerBox = FindGridBelow(outer)
            ?? throw new System.InvalidOperationException("inner GridContainerBox not found");
        var grandBox1 = innerBox.Children[0];

        // The inner grid occupies the outer's column 2 → offset by 100px (the first track).
        float innerX = For(elements, innerBox).Position.X;
        innerX.Should().BeApproximately(100f * Px, 0.5f,
            because: "the inner grid is the outer's second cell, offset past the 100px column");

        // The inner grid's first grandchild starts at the inner container origin (recursion via dispatch).
        For(elements, grandBox1).Position.X.Should().BeApproximately(innerX, 0.5f,
            because: "nested grid items compose via dispatch — grandchild X starts at the inner cell origin");
    }
}
