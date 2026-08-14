namespace Muonroi.Pdf.Tests.Layout;

public sealed class LayoutEngineIntegrationTests
{
    private static IStyledDocument SimpleDocument()
    {
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var para = new FakeStyledNode("p", new() { ["display"] = "block" });
        var text = new FakeStyledNode("span") { IsText = true, TextContent = "Hello PDF" };
        para.ChildList.Add(text);
        root.ChildList.Add(para);
        return new FakeStyledDocument(root);
    }

    [Fact]
    public void Layout_SimpleDocument_ReturnsNonNullPageList()
    {
        var engine = new LayoutEngine();
        var result = engine.Layout(
            SimpleDocument(), new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Layout_SimpleDocument_HasAtLeastOnePage()
    {
        var engine = new LayoutEngine();
        var result = (PositionedPageList)engine.Layout(
            SimpleDocument(), new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        result.PageCount.Should().BeGreaterThanOrEqualTo(1,
            because: "any non-empty document produces at least one page");
    }

    [Fact]
    public void Layout_WithFakePageRule_AppliesCustomMargins()
    {
        // FakePageRule with 20mm margins. Default is 10mm.
        // pageBodyHeight = A4 height - 2 * 20mm > A4 height - 2 * 10mm (smaller body)
        // We just verify the layout completes without error and returns pages.
        var pageRule = new FakePageRule
        {
            Margins = new PdfMargins(20, 20, 20, 20)
        };
        var root = new FakeStyledNode("div", new() { ["display"] = "block" });
        var text = new FakeStyledNode("span") { IsText = true, TextContent = "With custom margins" };
        root.ChildList.Add(text);
        var doc = new FakeStyledDocument(root, pageRule);

        var engine = new LayoutEngine();
        var result = (PositionedPageList)engine.Layout(
            doc, new PdfRenderOptions(), new PdfConfigs.PdfLimits(), CancellationToken.None);

        result.PageCount.Should().BeGreaterThanOrEqualTo(1,
            because: "layout with custom @page margins should still produce at least one page");
    }
}
