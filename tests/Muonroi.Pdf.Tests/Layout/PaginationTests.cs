using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

public sealed class PaginationTests
{
    // SC4: page-break-before:always forces the element to start on a new page
    [Fact]
    public void PageBreakBeforeAlways_SecondBlock_IsOnPageIndex1()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var block1 = new FakeStyledNode("p", new() { ["display"] = "block" });
        var textNode1 = new FakeStyledNode("span") { IsText = true, TextContent = "Page 1 content" };
        block1.ChildList.Add(textNode1);

        var block2 = new FakeStyledNode("p", new()
        {
            ["display"] = "block",
            ["page-break-before"] = "always",
        });
        var textNode2 = new FakeStyledNode("span") { IsText = true, TextContent = "Page 2 content" };
        block2.ChildList.Add(textNode2);

        root.ChildList.Add(block1);
        root.ChildList.Add(block2);

        var doc = new FakeStyledDocument(root);
        var engine = new LayoutEngine();
        var result = (PositionedPageList)engine.Layout(
            doc, new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        result.PageCount.Should().Be(2,
            because: "page-break-before:always forces the second block to start on a new page");
        result.Pages[1].Elements.Should().NotBeEmpty(
            because: "the second page should contain the forced-break element");
    }

    // SC5: counter(pages) is replaced with the total page count after two-pass layout
    [Fact]
    public void CounterPages_ResolvesToCorrectTotalAfterTwoPassLayout()
    {
        // Single-page document with an inline text node containing "counter(pages)"
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var counterText = new FakeStyledNode("span")
        {
            IsText = true,
            TextContent = "counter(pages)"
        };
        root.ChildList.Add(counterText);

        var doc = new FakeStyledDocument(root);
        var engine = new LayoutEngine();
        var result = (PositionedPageList)engine.Layout(
            doc, new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        // After two-pass layout the counter(pages) placeholder is replaced with the actual count
        var counterEl = result.Pages[0].Elements
            .First(e => e.Source is InlineBox);
        var inlineText = ((InlineBox)counterEl.Source).Text;

        inlineText.Should().NotContain("counter(",
            because: "the counter() placeholder must be replaced after two-pass layout");
        inlineText.Should().Be(result.PageCount.ToString(),
            because: "counter(pages) resolves to the total number of pages in the document");
    }

    // SC5: counter(page) is replaced with the 1-based page number
    [Fact]
    public void CounterPage_ResolvesToOneBased_PageNumber()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var counterText = new FakeStyledNode("span")
        {
            IsText = true,
            TextContent = "counter(page)"
        };
        root.ChildList.Add(counterText);

        var doc = new FakeStyledDocument(root);
        var engine = new LayoutEngine();
        var result = (PositionedPageList)engine.Layout(
            doc, new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        var counterEl = result.Pages[0].Elements.First(e => e.Source is InlineBox);
        var inlineText = ((InlineBox)counterEl.Source).Text;

        inlineText.Should().Be("1",
            because: "counter(page) on page 0 (index) resolves to 1 (one-based)");
    }

    // MaxPages: exceeding 1000 pages throws PdfInputLimitException
    [Fact]
    public void MaxPages_Exceeded_ThrowsPdfInputLimitException()
    {
        // Build 1001 blocks each with page-break-before:always (except the first)
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        for (int i = 0; i < 1002; i++)
        {
            var styles = new Dictionary<string, string> { ["display"] = "block" };
            if (i > 0) styles["page-break-before"] = "always";
            var block = new FakeStyledNode("p", styles);
            var t = new FakeStyledNode("span") { IsText = true, TextContent = $"p{i}" };
            block.ChildList.Add(t);
            root.ChildList.Add(block);
        }

        var doc = new FakeStyledDocument(root);
        var engine = new LayoutEngine();

        Action act = () => engine.Layout(
            doc, new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        act.Should().Throw<PdfInputLimitException>(
            because: "documents exceeding 1000 pages must throw PdfInputLimitException");
    }
}
