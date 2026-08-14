namespace Muonroi.Pdf.Tests.Writer;

[Collection(PdfRenderCollection.Name)]
public sealed class SecurityTests
{
    private static async Task<PolicyValidationResult> ValidateHtmlAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html, CancellationToken.None);

        var cascade = new AngleSharpCascadeEngine();
        IStyledDocument styled = await cascade.CascadeAsync(parsed, null, CancellationToken.None);

        var policy = new DefaultStrictPolicy();
        return await policy.ValidateAsync((IPdfDocumentContext)styled, CancellationToken.None);
    }

    [Fact]
    public async Task ThrowingResolver_FileUri_ThrowsPdfSecurityException()
    {
        var resolver = new ThrowingResourceResolver();

        Func<Task> act = async () =>
            await resolver.ResolveAsync(new Uri("file:///etc/passwd"), null, CancellationToken.None);

        await act.Should().ThrowAsync<PdfSecurityException>()
            .Where(ex => ex.RuleId == "SEC-06");
    }

    [Fact]
    public async Task ThrowingResolver_JavascriptUri_ThrowsPdfSecurityException()
    {
        var resolver = new ThrowingResourceResolver();

        Func<Task> act = async () =>
            await resolver.ResolveAsync(new Uri("javascript:alert(1)"), null, CancellationToken.None);

        await act.Should().ThrowAsync<PdfSecurityException>()
            .Where(ex => ex.RuleId == "SEC-06");
    }

    [Fact]
    public async Task ThrowingResolver_HttpsUri_ReturnsNull()
    {
        var resolver = new ThrowingResourceResolver();

        ResourceResult? result = await resolver.ResolveAsync(
            new Uri("https://example.com/image.png"), null, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ThrowingResolver_HttpUri_ReturnsNull()
    {
        var resolver = new ThrowingResourceResolver();

        ResourceResult? result = await resolver.ResolveAsync(
            new Uri("http://example.com/image.png"), null, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DefaultStrictPolicy_ScriptElement_ProducesViolation()
    {
        PolicyValidationResult result = await ValidateHtmlAsync(
            "<html><head></head><body><script>alert(1)</script><p>text</p></body></html>");

        result.Accepted.Should().BeFalse();
        result.Violations.Should().Contain(v => v.RuleId == "forbidden.script-element");
    }

    [Fact]
    public async Task DefaultStrictPolicy_NoScriptElement_NoViolation()
    {
        PolicyValidationResult result = await ValidateHtmlAsync(
            "<html><head></head><body><p>clean content</p></body></html>");

        result.Accepted.Should().BeTrue();
        result.Violations.Should().NotContain(v => v.RuleId == "forbidden.script-element");
    }

    [Fact]
    public async Task OwnedPdfWriter_OutputHeaderIsPdf17()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement
        {
            Source = new InlineBox { Text = "secure", FontFamily = WriterTestFonts.Family, FontSize = 12f },
            Position = new Rect(50, 50, 100, 20),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();

        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);
        byte[] pdfBytes = ms.ToArray();

        Encoding.ASCII.GetString(pdfBytes, 0, 8).Should().StartWith("%PDF-1.7");
    }
}
