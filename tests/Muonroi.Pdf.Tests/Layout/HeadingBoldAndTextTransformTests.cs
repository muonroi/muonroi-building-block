using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G18 regression tests: h1-h6 UA bold default + text-transform:uppercase inheritance.
/// Verifies that block-level headings carry Bold=true and TextTransform, and that these
/// values propagate to their inline text children at tree-build time.
/// </summary>
public sealed class HeadingBoldAndTextTransformTests
{
    private static BoxTreeBuilder Builder() => new();

    // -------------------------------------------------------------------------
    // Helper: flatten all InlineBox descendants from a box tree node.
    // -------------------------------------------------------------------------
    private static List<InlineBox> CollectInlines(BoxNode root)
    {
        var result = new List<InlineBox>();
        CollectInlinesRecursive(root, result);
        return result;
    }

    private static void CollectInlinesRecursive(BoxNode node, List<InlineBox> result)
    {
        if (node is InlineBox inline)
            result.Add(inline);
        foreach (var child in node.Children)
            CollectInlinesRecursive(child, result);
    }

    // Helper: create a text node (IsText=true, no styles).
    private static FakeStyledNode TextNode(string content) =>
        new("#text") { IsText = true, IsElement = false, TextContent = content };

    // Helper: make a minimal LayoutContext for InlineLayoutEngine tests.
    private static LayoutContext MakeContext(float availableWidth = 500f) =>
        new()
        {
            PageWidth = availableWidth,
            PageHeight = 800f,
            AvailableWidth = availableWidth,
            CurrentY = 0f,
            CurrentPageIndex = 0,
            TotalPages = 0,
            TextMetrics = EstimatedTextMetrics.Instance,
            PageMargins = PdfMargins.Zero,
            Exclusions = new List<FloatExclusion>(),
        };

    // -------------------------------------------------------------------------
    // Case 1: <h2>foo</h2> → UA bold applies; text run has Bold = true.
    // -------------------------------------------------------------------------

    [Fact]
    public void H2_NoExplicitFontWeight_InlineChildGetsBoldFromUa()
    {
        // Arrange: body > h2 > text-node "foo"
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var h2 = new FakeStyledNode("h2", new() { ["display"] = "" });
        h2.ChildList.Add(TextNode("foo"));
        body.ChildList.Add(h2);

        // Act
        var root = Builder().Build(body);

        // Assert: the h2 block box should have Bold=true (UA stylesheet default)
        var h2Box = root.Children.OfType<BlockBox>().FirstOrDefault();
        h2Box.Should().NotBeNull(because: "<h2> must produce a BlockBox");
        h2Box!.Bold.Should().BeTrue(because: "UA stylesheet gives h1-h6 font-weight:bold");

        // Assert: the InlineBox text child must inherit Bold=true
        var inlines = CollectInlines(root);
        inlines.Should().NotBeEmpty(because: "text node inside h2 must produce an InlineBox");
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeTrue(because: "inline text inside h2 inherits UA bold"));
    }

    // -------------------------------------------------------------------------
    // Case 2: <h2 class="text-uppercase">phiếu đăng ký</h2>
    //   → Bold=true (UA) AND text run uppercased at emit time.
    // -------------------------------------------------------------------------

    [Fact]
    public void H2_WithTextUppercaseClass_InlineChildGetsBoldAndTextIsUppercased()
    {
        // Arrange: inject a <style> element so ExtractClassRules picks up .text-uppercase.
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });

        var styleNode = new FakeStyledNode("style")
        {
            TextContent = ".text-uppercase { text-transform: uppercase; }"
        };
        body.ChildList.Add(styleNode);

        var h2 = new FakeStyledNode("h2",
            styles: new() { ["display"] = "" },
            attributes: new() { ["class"] = "text-uppercase" });
        h2.ChildList.Add(TextNode("phiếu đăng ký"));
        body.ChildList.Add(h2);

        // Act: build box tree
        var root = Builder().Build(body);

        // Assert: h2 BlockBox has Bold=true AND TextTransform="uppercase"
        // Note: the <style> element also produces a BlockBox child — find by source tag name.
        var h2Box = root.Children
            .OfType<BlockBox>()
            .FirstOrDefault(b => string.Equals(b.Source?.LocalName, "h2", StringComparison.OrdinalIgnoreCase));
        h2Box.Should().NotBeNull(because: "<h2> element must produce a BlockBox");
        h2Box!.Bold.Should().BeTrue(because: "UA bold applies to h2");
        h2Box.TextTransform.Should().Be("uppercase",
            because: ".text-uppercase class rule sets text-transform:uppercase on the block");

        // Assert: InlineBox children inherit both
        var inlines = CollectInlines(root);
        inlines.Should().NotBeEmpty();
        inlines.Should().AllSatisfy(b =>
        {
            b.Bold.Should().BeTrue(because: "inherits UA bold from h2");
            b.TextTransform.Should().Be("uppercase",
                because: "inherits text-transform:uppercase from h2");
        });

        // Assert: at layout time, InlineLayoutEngine uppercases the text run.
        var engine = new InlineLayoutEngine();
        var output = new List<PositionedElement>();
        engine.Layout(inlines, MakeContext(), output, pageIndex: 0);

        output.Should().NotBeEmpty(because: "layout must produce positioned elements");
        // Each emitted word must be uppercased.
        output.Should().AllSatisfy(pe =>
            pe.RenderedText.Should().Be(pe.RenderedText.ToUpperInvariant(),
                because: "text-transform:uppercase must uppercase all emitted text runs"));

        // Verify the specific words match the uppercased Vietnamese text.
        string joinedText = string.Join(" ", output.Select(pe => pe.RenderedText));
        joinedText.Should().Be("PHIẾU ĐĂNG KÝ",
            because: "full text after uppercase transform must equal 'PHIẾU ĐĂNG KÝ'");
    }

    // -------------------------------------------------------------------------
    // Case 3: <h2 style="font-weight:normal">foo</h2>
    //   → author override beats UA; text run has Bold = false.
    // -------------------------------------------------------------------------

    [Fact]
    public void H2_WithExplicitFontWeightNormal_InlineChildIsNotBold()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var h2 = new FakeStyledNode("h2",
            styles: new() { ["display"] = "", ["font-weight"] = "normal" });
        h2.ChildList.Add(TextNode("foo"));
        body.ChildList.Add(h2);

        var root = Builder().Build(body);

        // h2 box: author "font-weight:normal" must override UA bold
        var h2Box = root.Children.OfType<BlockBox>().FirstOrDefault();
        h2Box.Should().NotBeNull();
        h2Box!.Bold.Should().BeFalse(
            because: "explicit font-weight:normal overrides UA bold for h2");

        // Inline children must NOT be bold
        var inlines = CollectInlines(root);
        inlines.Should().NotBeEmpty();
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeFalse(because: "author font-weight:normal propagates to inline children"));
    }

    // -------------------------------------------------------------------------
    // Case 4: <p>plain</p> → Bold=false (no false-positive bold on non-headings).
    // -------------------------------------------------------------------------

    [Fact]
    public void P_PlainText_InlineChildIsNotBold()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var p = new FakeStyledNode("p", new() { ["display"] = "" });
        p.ChildList.Add(TextNode("plain"));
        body.ChildList.Add(p);

        var root = Builder().Build(body);

        var inlines = CollectInlines(root);
        inlines.Should().NotBeEmpty();
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeFalse(because: "<p> has no UA bold; inline text must not be bold"));
    }

    // -------------------------------------------------------------------------
    // Case 5: <p><strong>bar</strong></p> → InlineBox from <strong> has Bold=true.
    //   Existing InlineBox branch behavior must be unchanged after G18 refactor.
    // -------------------------------------------------------------------------

    [Fact]
    public void Strong_InsideP_InlineBoxIsBold()
    {
        var body = new FakeStyledNode("body", new() { ["display"] = "block" });
        var p = new FakeStyledNode("p", new() { ["display"] = "" });
        // <strong> with explicit font-weight:bold in computed style (UA value for <strong>).
        var strong = new FakeStyledNode("strong",
            styles: new() { ["display"] = "", ["font-weight"] = "bold" })
        { TextContent = "bar" };
        p.ChildList.Add(strong);
        body.ChildList.Add(p);

        var root = Builder().Build(body);

        var inlines = CollectInlines(root);
        inlines.Should().NotBeEmpty(because: "<strong> must produce an InlineBox");
        inlines.Should().AllSatisfy(b =>
            b.Bold.Should().BeTrue(because: "<strong> carries font-weight:bold via CSS"));
    }
}
