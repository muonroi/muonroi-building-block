namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Phase 14 (Group A): end-to-end render through the @page margin-box → running-content path, and
/// the API-wins-per-band precedence branch. Per-page stamping + counter substitution are covered at
/// the pagination level by <c>RunningHeaderFooterTests</c>; here we prove the service wires
/// <c>IPageRule</c> margin boxes into a <c>RunningContentSpec</c> without error and that
/// <c>options.Header</c> takes the override branch.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class MarginBoxRenderTests
{
    private const string FontFace =
        "<style>@font-face{font-family:serif;src:url(test.ttf);}</style>";

    // Two pages: a forced page break exercises per-page running-content stamping.
    private static string TwoPageBody(string headStyle) =>
        "<html><head>" + FontFace + headStyle + "</head><body>" +
        "<p>page one</p>" +
        "<div style=\"page-break-before:always;\">page two</div>" +
        "</body></html>";

    private static async Task<(byte[] Bytes, PdfRenderResult Meta)> RenderAsync(
        string html, PdfRenderOptions options)
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();
        using var ms = new MemoryStream();
        PdfRenderResult meta = await svc.RenderAsync(html, ms, options, default);
        return (ms.ToArray(), meta);
    }

    private static void ShouldBeValidPdf(byte[] bytes)
    {
        bytes.Length.Should().BeGreaterThan(0);
        Encoding.ASCII.GetString(bytes, 0, 8).Should().Be("%PDF-1.7");
    }

    [Fact]
    public async Task MarginBox_TopCenter_RendersAcrossPages_WithoutApiHeader()
    {
        string html = TwoPageBody(
            "<style>@page { @top-center { content: \"Trang \" counter(page) \"/\" counter(pages); } }</style>");

        (byte[] bytes, PdfRenderResult meta) = await RenderAsync(html, new PdfRenderOptions
        {
            TemplateId = PdfServiceTestHarness.TemplateId,
            // No options.Header — the running header must come from the @page margin box.
        });

        ShouldBeValidPdf(bytes);
        meta.PageCount.Should().Be(2, because: "the forced page break yields two pages");
    }

    [Fact]
    public async Task ApiHeader_OverridesMarginBox_PerBand()
    {
        // Both an @page top-center box AND options.Header are present → API wins (override branch).
        string html = TwoPageBody(
            "<style>@page { @top-center { content: \"FROM CSS\"; } }</style>");

        (byte[] bytes, PdfRenderResult meta) = await RenderAsync(html, new PdfRenderOptions
        {
            TemplateId = PdfServiceTestHarness.TemplateId,
            Header = new PdfHeaderFooter(
                CenterHtml: "FROM API counter(page)/counter(pages)",
                HeightMm: 16,
                ShowLine: true),
        });

        ShouldBeValidPdf(bytes);
        meta.PageCount.Should().Be(2);
    }

    [Fact]
    public async Task NoMarginBox_NoApiHeader_StillRenders()
    {
        string html = TwoPageBody("<style>@page { margin: 12mm; }</style>");

        (byte[] bytes, PdfRenderResult meta) = await RenderAsync(html, new PdfRenderOptions
        {
            TemplateId = PdfServiceTestHarness.TemplateId,
        });

        ShouldBeValidPdf(bytes);
        meta.PageCount.Should().Be(2);
    }
}
