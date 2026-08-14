namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// FLEX-07 operand-value position assertions for the flexbox layout engine. Each test builds a
/// <see cref="FlexContainerBox"/> via <see cref="BoxTreeBuilder"/> (allowModernLayout:true) and
/// asserts <see cref="PositionedElement"/>.Position X/Y/W/H by VALUE — never just a non-throwing
/// render (per memory pdf_phase15_radial_affine: a green non-throwing render once hid a real
/// layout bug). Units: 1px = <see cref="Units.PxToPt"/> (0.75pt).
/// </summary>
public sealed class FlexLayoutTests
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

    // Build a flex container box tree, then run the FlexLayoutEngine directly so positions are
    // asserted straight off PositionedElement (originX = 0 with PdfMargins.Zero / no ContentOriginX).
    private static (FlexContainerBox container, List<PositionedElement> elements) RunFlex(
        FakeStyledNode containerNode, float availableWidth = 1000f, float pageHeight = 1000f)
    {
        // BoxTreeBuilder.Build() materializes the ROOT node directly as a BlockBox (display is not
        // consulted for the root). Only CHILD nodes pass through CreateBox where display:flex maps
        // to FlexContainerBox — so wrap the flex node in a plain block parent.
        var parent = new FakeStyledNode("div", new() { ["display"] = "block" });
        parent.ChildList.Add(containerNode);
        var root = new BoxTreeBuilder().Build(parent, null, allowModernLayout: true);
        var container = FindFlex(root) ?? throw new System.InvalidOperationException(
            "BoxTreeBuilder did not produce a FlexContainerBox — check display:flex + allowModernLayout.");

        var be = new BlockLayoutEngine();
        be.TableEngine = new TableLayoutEngine(be, be.InlineEngine);
        be.FlexEngine = new FlexLayoutEngine(be);

        var elements = new List<PositionedElement>();
        be.FlexEngine.Layout(container, MakeContext(availableWidth, pageHeight), elements, 0);
        return (container, elements);
    }

    private static FlexContainerBox? FindFlex(BoxNode node)
    {
        if (node is FlexContainerBox fc) return fc;
        foreach (var child in node.Children)
        {
            var found = FindFlex(child);
            if (found != null) return found;
        }

        return null;
    }

    // Finds a FlexContainerBox strictly below `node` (excludes `node` itself).
    private static FlexContainerBox? FindFlexBelow(BoxNode node)
    {
        foreach (var child in node.Children)
        {
            var found = FindFlex(child);
            if (found != null) return found;
        }

        return null;
    }

    private static FakeStyledNode Flex(Dictionary<string, string> style, params FakeStyledNode[] children)
    {
        style["display"] = style.GetValueOrDefault("display", "flex");
        var node = new FakeStyledNode("div", style);
        foreach (var c in children) node.ChildList.Add(c);
        return node;
    }

    private static FakeStyledNode Item(Dictionary<string, string> style, string? text = null)
    {
        var node = new FakeStyledNode("div", style);
        if (text != null)
        {
            var t = new FakeStyledNode("#text") { IsElement = false, IsText = true, TextContent = text };
            node.ChildList.Add(t);
        }

        return node;
    }

    private static PositionedElement For(List<PositionedElement> elements, BoxNode source) =>
        elements.First(e => e.Source == source);

    // --- RowDistribution: 3 width:50px children, width:300px container, no gap ---------------
    [Fact]
    public void RowDistribution_PositionsItemsLeftToRight()
    {
        var c1 = Item(new() { ["width"] = "50px" });
        var c2 = Item(new() { ["width"] = "50px" });
        var c3 = Item(new() { ["width"] = "50px" });
        var (container, elements) = RunFlex(Flex(new() { ["width"] = "300px" }, c1, c2, c3));

        var b1 = container.Children[0];
        var b2 = container.Children[1];
        var b3 = container.Children[2];

        For(elements, b1).Position.X.Should().BeApproximately(0f, 0.1f);
        For(elements, b2).Position.X.Should().BeApproximately(50f * Px, 0.1f);
        For(elements, b3).Position.X.Should().BeApproximately(100f * Px, 0.1f);

        For(elements, b1).Position.Width.Should().BeApproximately(50f * Px, 0.1f);
        For(elements, b2).Position.Width.Should().BeApproximately(50f * Px, 0.1f);
        For(elements, b3).Position.Width.Should().BeApproximately(50f * Px, 0.1f);

        float y = For(elements, b1).Position.Y;
        For(elements, b2).Position.Y.Should().BeApproximately(y, 0.1f);
        For(elements, b3).Position.Y.Should().BeApproximately(y, 0.1f);
    }

    // --- RowContentBasis: two TEXT children, no explicit width → packed by content size --------
    [Fact]
    public void RowContentBasis_MeasuresIntrinsicWidth()
    {
        var c1 = Item(new(), text: "Alpha");
        var c2 = Item(new(), text: "Beta");
        var (container, elements) = RunFlex(Flex(new() { ["width"] = "600px" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        var p1 = For(elements, b1);
        var p2 = For(elements, b2);

        p1.Position.Width.Should().BeGreaterThan(0f,
            because: "content-basis path must measure intrinsic width, not ship a 0-width stub");
        p2.Position.Width.Should().BeGreaterThan(0f);
        p2.Position.X.Should().BeApproximately(p1.Position.X + p1.Position.Width, 0.5f,
            because: "items are packed by content size, not overlapping at X=0");
    }

    // --- FlexGrow: two flex:1 children, basis 0, width:300px → each ~150px ---------------------
    [Fact]
    public void FlexGrow_DistributesPositiveFreeSpace()
    {
        var c1 = Item(new() { ["flex-grow"] = "1", ["flex-basis"] = "0" });
        var c2 = Item(new() { ["flex-grow"] = "1", ["flex-basis"] = "0" });
        var (container, elements) = RunFlex(Flex(new() { ["width"] = "300px" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        For(elements, b1).Position.Width.Should().BeApproximately(150f * Px, 0.5f);
        For(elements, b2).Position.Width.Should().BeApproximately(150f * Px, 0.5f);
        For(elements, b2).Position.X.Should().BeApproximately(
            For(elements, b1).Position.X + 150f * Px, 0.5f);
    }

    // --- FlexShrink: two width:80px children, width:100px, shrink:1 → each ~50px ---------------
    [Fact]
    public void FlexShrink_ShrinksOnOverflow()
    {
        var c1 = Item(new() { ["width"] = "80px", ["flex-shrink"] = "1" });
        var c2 = Item(new() { ["width"] = "80px", ["flex-shrink"] = "1" });
        var (container, elements) = RunFlex(Flex(new() { ["width"] = "100px" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        // total basis 160 > 100, free = -60 split by shrink*basis (equal) → each 80 - 30 = 50px
        For(elements, b1).Position.Width.Should().BeApproximately(50f * Px, 0.5f);
        For(elements, b2).Position.Width.Should().BeApproximately(50f * Px, 0.5f);
    }

    // --- JustifyContent: space-between → first at start, second at end -------------------------
    [Fact]
    public void JustifyContent_SpaceBetween()
    {
        var c1 = Item(new() { ["width"] = "50px" });
        var c2 = Item(new() { ["width"] = "50px" });
        var (container, elements) = RunFlex(
            Flex(new() { ["width"] = "300px", ["justify-content"] = "space-between" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        For(elements, b1).Position.X.Should().BeApproximately(0f, 0.1f);
        // second at end: 300 - 50 = 250px from start
        For(elements, b2).Position.X.Should().BeApproximately(250f * Px, 0.5f);
    }

    // --- JustifyContent: center and space-evenly ----------------------------------------------
    [Fact]
    public void JustifyContent_Center_And_SpaceEvenly()
    {
        // center: 2 items 50px in 300px → free 200, leading 100 → first.X = 100px
        var c1 = Item(new() { ["width"] = "50px" });
        var c2 = Item(new() { ["width"] = "50px" });
        var (cc, ce) = RunFlex(
            Flex(new() { ["width"] = "300px", ["justify-content"] = "center" }, c1, c2));
        For(ce, cc.Children[0]).Position.X.Should().BeApproximately(100f * Px, 0.5f);

        // space-evenly: free 200 / (n+1=3) = 66.667 before each → first.X = 66.667px
        var e1 = Item(new() { ["width"] = "50px" });
        var e2 = Item(new() { ["width"] = "50px" });
        var (ec, ee) = RunFlex(
            Flex(new() { ["width"] = "300px", ["justify-content"] = "space-evenly" }, e1, e2));
        For(ee, ec.Children[0]).Position.X.Should().BeApproximately((200f / 3f) * Px, 0.5f);
    }

    // --- AlignItems: stretch sets cross size; flex-start keeps content height ------------------
    [Fact]
    public void AlignItems_Stretch_SetsCrossSize()
    {
        var child = Item(new() { ["width"] = "50px" });
        var (sc, se) = RunFlex(
            Flex(new() { ["width"] = "300px", ["height"] = "100px", ["align-items"] = "stretch" }, child));
        For(se, sc.Children[0]).Position.Height.Should().BeApproximately(100f * Px, 0.5f,
            because: "align-items:stretch grows a cross-size-less item to the line cross size");

        var child2 = Item(new() { ["width"] = "50px" });
        var (fc, fe) = RunFlex(
            Flex(new() { ["width"] = "300px", ["height"] = "100px", ["align-items"] = "flex-start" }, child2));
        For(fe, fc.Children[0]).Position.Height.Should().BeLessThan(100f * Px,
            because: "align-items:flex-start keeps the item's content height, not the container height");
    }

    // --- FlexWrap: 3 width:50px children, width:120px, wrap → item3 wraps to line 2 ------------
    [Fact]
    public void FlexWrap_BreaksToSecondLine()
    {
        var c1 = Item(new() { ["width"] = "50px", ["height"] = "20px" });
        var c2 = Item(new() { ["width"] = "50px", ["height"] = "20px" });
        var c3 = Item(new() { ["width"] = "50px", ["height"] = "20px" });
        var (container, elements) = RunFlex(
            Flex(new() { ["width"] = "120px", ["flex-wrap"] = "wrap" }, c1, c2, c3));

        var b1 = container.Children[0];
        var b3 = container.Children[2];

        For(elements, b1).Position.X.Should().BeApproximately(0f, 0.1f);
        For(elements, b3).Position.X.Should().BeApproximately(0f, 0.1f,
            because: "the wrapped item restarts at main-start on line 2");
        For(elements, b3).Position.Y.Should().BeGreaterThan(For(elements, b1).Position.Y,
            because: "the wrapped item is on a lower line");
    }

    // --- Gap: two width:50px children, gap:20px → second.X = first + 50 + 20 -------------------
    [Fact]
    public void Gap_AddsMainAxisSpacing()
    {
        var c1 = Item(new() { ["width"] = "50px" });
        var c2 = Item(new() { ["width"] = "50px" });
        var (container, elements) = RunFlex(
            Flex(new() { ["width"] = "300px", ["gap"] = "20px" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        For(elements, b2).Position.X.Should().BeApproximately(
            For(elements, b1).Position.X + 50f * Px + 20f * Px, 0.5f);
    }

    // --- ColumnDirection: two height:40px children stack vertically ---------------------------
    [Fact]
    public void ColumnDirection_StacksVertically()
    {
        var c1 = Item(new() { ["height"] = "40px" });
        var c2 = Item(new() { ["height"] = "40px" });
        var (container, elements) = RunFlex(
            Flex(new() { ["flex-direction"] = "column", ["height"] = "300px" }, c1, c2));

        var b1 = container.Children[0];
        var b2 = container.Children[1];

        For(elements, b2).Position.Y.Should().BeApproximately(
            For(elements, b1).Position.Y + 40f * Px, 0.5f);
        For(elements, b2).Position.X.Should().BeApproximately(For(elements, b1).Position.X, 0.1f);
    }

    // --- Order: first child order:2, second order:1 → order:1 child placed at main-start -------
    [Fact]
    public void Order_ReordersVisually()
    {
        var c1 = Item(new() { ["width"] = "50px", ["order"] = "2" });
        var c2 = Item(new() { ["width"] = "50px", ["order"] = "1" });
        var (container, elements) = RunFlex(Flex(new() { ["width"] = "300px" }, c1, c2));

        // children index 0 = order:2, index 1 = order:1 → the order:1 box gets the smaller X.
        var orderTwoBox = container.Children[0];
        var orderOneBox = container.Children[1];

        For(elements, orderOneBox).Position.X.Should().BeApproximately(0f, 0.1f,
            because: "the lower order value is placed first at main-start");
        For(elements, orderTwoBox).Position.X.Should().BeApproximately(50f * Px, 0.5f,
            because: "the higher order value follows the order:1 item");
    }

    // --- NestedFlex: inner flex container's grandchild is offset by the outer item's X ---------
    [Fact]
    public void NestedFlex_Composes()
    {
        var grand1 = Item(new() { ["width"] = "30px" });
        var grand2 = Item(new() { ["width"] = "30px" });
        var inner = Flex(new() { ["display"] = "flex", ["width"] = "200px" }, grand1, grand2);

        // outer: first item is a 100px spacer, second item is the inner flex container.
        var spacer = Item(new() { ["width"] = "100px" });
        var outerNode = Flex(new() { ["width"] = "500px" }, spacer, inner);

        var wrapper = new FakeStyledNode("div", new() { ["display"] = "block" });
        wrapper.ChildList.Add(outerNode);
        var root = new BoxTreeBuilder().Build(wrapper, null, allowModernLayout: true);
        var outer = FindFlex(root)!;

        var be = new BlockLayoutEngine();
        be.TableEngine = new TableLayoutEngine(be, be.InlineEngine);
        be.FlexEngine = new FlexLayoutEngine(be);
        var elements = new List<PositionedElement>();
        be.FlexEngine.Layout(outer, MakeContext(), elements, 0);

        // The child collector may normalize/wrap outer children, so locate boxes by traversal
        // rather than fixed indices: the inner FlexContainerBox is a descendant of the outer,
        // and the spacer is the first non-flex leaf with width 100px*PxToPt.
        var innerBox = FindFlexBelow(outer)
            ?? throw new System.InvalidOperationException("inner FlexContainerBox not found");
        var grandBox1 = innerBox.Children[0];

        // spacer is the outer item placed at main-start (X≈0) with width 100px.
        var spacerWidth = elements
            .Where(e => System.MathF.Abs(e.Position.Width - 100f * Px) < 0.5f && e.Source is not FlexContainerBox)
            .Select(e => e.Position.Width)
            .First();
        spacerWidth.Should().BeApproximately(100f * Px, 0.5f);

        float innerX = For(elements, innerBox).Position.X;
        innerX.Should().BeApproximately(100f * Px, 0.5f,
            because: "the inner flex container is the outer's second item, offset past the spacer");

        // The inner grandchild's first item must be offset by the inner container's X (recursion).
        For(elements, grandBox1).Position.X.Should().BeApproximately(innerX, 0.5f,
            because: "nested flex items compose via dispatch — grandchild X starts at the inner container origin");
    }
}
