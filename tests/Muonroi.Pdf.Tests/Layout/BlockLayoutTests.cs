namespace Muonroi.Pdf.Tests.Layout;

public sealed class BlockLayoutTests
{
    private static LayoutContext MakeContext(float availableWidth = 400f, float pageHeight = 800f) =>
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

    // SC1: Adjacent block margins collapse to max(a,b) not a+b
    [Fact]
    public void AdjacentBlocks_MarginCollapsesTo_Maximum()
    {
        // child1: margin-bottom=20px (=15pt); child2: margin-top=30px (=22.5pt)
        // Collapsed gap = max(20,30)*PxToPt = 22.5pt, NOT (20+30)*PxToPt = 37.5pt
        var parent = new FakeStyledNode("div", new() { ["display"] = "block" });
        var child1 = new FakeStyledNode("p", new() { ["display"] = "block", ["margin-bottom"] = "20px" });
        var child2 = new FakeStyledNode("p", new() { ["display"] = "block", ["margin-top"] = "30px" });
        parent.ChildList.Add(child1);
        parent.ChildList.Add(child2);

        var builder = new BoxTreeBuilder();
        var parentBox = builder.Build(parent);
        var child1Box = parentBox.Children[0];
        var child2Box = parentBox.Children[1];

        var engine = new BlockLayoutEngine();
        var elements = new List<PositionedElement>();
        engine.Layout(parentBox, MakeContext(), elements, pageIndex: 0, isRoot: false);

        var pe1 = elements.First(e => e.Source == child1Box);
        var pe2 = elements.First(e => e.Source == child2Box);

        float gap = pe2.Position.Y - (pe1.Position.Y + pe1.Position.Height);
        float expectedGap = 30f * Units.PxToPt; // max(20,30) * PxToPt
        gap.Should().BeApproximately(expectedGap, precision: 0.1f,
            because: "adjacent margins collapse to the maximum, not the sum");
    }

    // SC1 variant: verify that the gap is NOT the sum (50px worth)
    [Fact]
    public void AdjacentBlocks_GapIsNotSumOfMargins()
    {
        var parent = new FakeStyledNode("div", new() { ["display"] = "block" });
        var child1 = new FakeStyledNode("p", new() { ["display"] = "block", ["margin-bottom"] = "20px" });
        var child2 = new FakeStyledNode("p", new() { ["display"] = "block", ["margin-top"] = "30px" });
        parent.ChildList.Add(child1);
        parent.ChildList.Add(child2);

        var parentBox = new BoxTreeBuilder().Build(parent);
        var child1Box = parentBox.Children[0];
        var child2Box = parentBox.Children[1];

        var elements = new List<PositionedElement>();
        new BlockLayoutEngine().Layout(parentBox, MakeContext(), elements, 0, isRoot: false);

        var pe1 = elements.First(e => e.Source == child1Box);
        var pe2 = elements.First(e => e.Source == child2Box);

        float gap = pe2.Position.Y - (pe1.Position.Y + pe1.Position.Height);
        float sumGap = (20f + 30f) * Units.PxToPt;
        gap.Should().BeLessThan(sumGap,
            because: "margin collapse produces gap < sum of both margins");
    }

    // SC1: BFC root (overflow:hidden) prevents parent-child margin collapse
    [Fact]
    public void BfcRoot_PreservesFirstChildMarginTop()
    {
        // Without BFC: parent (no border/padding) collapses with first child's margin-top
        // → child's margin-top is zeroed (parent absorbs it)
        // With BFC (overflow:hidden): first child's margin-top is preserved → child positioned lower

        float childMt = 30f * Units.PxToPt; // 30px = 22.5pt

        // Non-BFC parent: first child's margin-top is zeroed
        var nonBfcParent = new FakeStyledNode("div", new() { ["display"] = "block" });
        var childA = new FakeStyledNode("p", new() { ["display"] = "block", ["margin-top"] = "30px" });
        nonBfcParent.ChildList.Add(childA);

        var nonBfcBox = new BoxTreeBuilder().Build(nonBfcParent);
        var childABox = nonBfcBox.Children[0];
        var nonBfcElements = new List<PositionedElement>();
        new BlockLayoutEngine().Layout(nonBfcBox, MakeContext(), nonBfcElements, 0, isRoot: false);
        var peA = nonBfcElements.First(e => e.Source == childABox);

        // BFC parent: first child's margin-top is preserved
        var bfcParent = new FakeStyledNode("div",
            new() { ["display"] = "block", ["overflow"] = "hidden" });
        var childB = new FakeStyledNode("p", new() { ["display"] = "block", ["margin-top"] = "30px" });
        bfcParent.ChildList.Add(childB);

        var bfcBox = new BoxTreeBuilder().Build(bfcParent);
        var childBBox = bfcBox.Children[0];
        var bfcElements = new List<PositionedElement>();
        new BlockLayoutEngine().Layout(bfcBox, MakeContext(), bfcElements, 0, isRoot: false);
        var peB = bfcElements.First(e => e.Source == childBBox);

        // Non-BFC: child collapsed to Y=0 (margin absorbed by parent)
        peA.Position.Y.Should().BeApproximately(0f, 0.1f,
            because: "non-BFC parent collapses first child's margin-top");

        // BFC: child is offset by its margin-top (not collapsed)
        peB.Position.Y.Should().BeApproximately(childMt, 0.1f,
            because: "BFC root preserves first child's margin-top");
    }
}
