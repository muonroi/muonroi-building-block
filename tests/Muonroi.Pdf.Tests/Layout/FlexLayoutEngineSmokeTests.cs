using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

// WAVE-3 SMOKE GATE (18-03): a single operand-value assertion proving the flex engine packs
// items left-to-right along the main axis (not stacked at X=0 / build-green-but-logic-broken).
// The full FLEX-07 position-value suite lands in Plan 04.
public sealed class FlexLayoutEngineSmokeTests
{
    [Fact]
    public void FlexRow_TwoFiftyPxChildren_NoGap_PacksLeftToRight()
    {
        // display:flex row, width:300px, two children width:50px each, no gap, no grow.
        var root = new FakeStyledNode("section", new() { ["display"] = "block" });
        var flex = new FakeStyledNode("div", new() { ["display"] = "flex", ["width"] = "300px" });
        var c1 = new FakeStyledNode("div", new() { ["display"] = "block", ["width"] = "50px" });
        var c2 = new FakeStyledNode("div", new() { ["display"] = "block", ["width"] = "50px" });
        flex.ChildList.Add(c1);
        flex.ChildList.Add(c2);
        root.ChildList.Add(flex);

        var rootBox = new BoxTreeBuilder().Build(root, null, allowModernLayout: true);

        // Wire the engines explicitly (mirrors LayoutEngine ctor post-construction wiring).
        var be = new BlockLayoutEngine();
        be.TableEngine = new TableLayoutEngine(be, be.InlineEngine);
        be.FlexEngine = new FlexLayoutEngine(be);

        var ctx = new LayoutContext
        {
            PageWidth = 595f,
            PageHeight = 842f,
            AvailableWidth = 595f,
            CurrentY = 0f,
        };
        var output = new List<PositionedElement>();
        be.Layout(rootBox, ctx, output, pageIndex: 0, isRoot: true);

        // The two flex items are BlockBox children of the FlexContainerBox.
        var fc = rootBox.Children.OfType<FlexContainerBox>().Single();
        var item1 = fc.Children[0];
        var item2 = fc.Children[1];

        var pe1 = output.Single(e => ReferenceEquals(e.Source, item1));
        var pe2 = output.Single(e => ReferenceEquals(e.Source, item2));

        // 50px → 37.5pt. Left-to-right packing: item2.X ≈ item1.X + 50px*PxToPt.
        float fiftyPx = 50f * (float)Units.PxToPt;
        pe2.Position.X.Should().BeApproximately(pe1.Position.X + fiftyPx, 0.5f);
    }
}
