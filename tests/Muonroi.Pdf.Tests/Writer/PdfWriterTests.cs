namespace Muonroi.Pdf.Tests.Writer;

[Collection(PdfRenderCollection.Name)]
public sealed class PdfWriterTests
{
    private static PositionedPageList MinimalPageList(string text = "Hello PDF")
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        var inline = new InlineBox
        {
            Text = text,
            FontFamily = WriterTestFonts.Family,
            FontSize = 12f,
            Bold = false,
            Italic = false
        };
        page.Elements.Add(new PositionedElement
        {
            Source = inline,
            Position = new Rect(50, 50, 100, 20),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();
        return pageList;
    }

    private static async Task<byte[]> RenderAsync(PositionedPageList pageList, PdfRenderOptions? options = null)
    {
        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, options ?? new PdfRenderOptions(), ms, CancellationToken.None);
        return ms.ToArray();
    }

    [Fact]
    public async Task WriteAsync_MinimalPageList_ProducesNonEmptyOutput()
    {
        var pageList = MinimalPageList();
        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();

        long bytesWritten = await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);

        bytesWritten.Should().BeGreaterThan(0);
        ms.ToArray().Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_OutputStartsWithPdf17Header()
    {
        byte[] pdfBytes = await RenderAsync(MinimalPageList());

        string header = Encoding.ASCII.GetString(pdfBytes, 0, 8);
        header.Should().StartWith("%PDF-1.7");
    }

    [Fact]
    public async Task WriteAsync_OutputContainsNoForbiddenPdfEntries()
    {
        byte[] pdfBytes = await RenderAsync(MinimalPageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().NotContain("/JavaScript");
        pdfText.Should().NotContain("/Launch");
        pdfText.Should().NotContain("/OpenAction");
        pdfText.Should().NotContain("/EmbeddedFile");
    }

    [Fact]
    public async Task WriteAsync_OutputContainsNoCurrentTimestamps()
    {
        byte[] pdfBytes = await RenderAsync(MinimalPageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().NotContain($"D:{DateTime.UtcNow.Year}");
    }

    [Fact]
    public async Task WriteAsync_WithReplacedBox_NoExceptionWhenImageMissing()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement
        {
            Source = new ReplacedBox { Src = "missing.png", NaturalWidth = 100, NaturalHeight = 100 },
            Position = new Rect(10, 10, 100, 100),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = [];
        pageList.Images = new Dictionary<string, DecodedImage>();

        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();

        Func<Task> act = async () => await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WriteAsync_EmptyPageList_ProducesValidPdfWithZeroPages()
    {
        // OwnedPdfWriter (unlike PdfSharpCore) emits a structurally valid PDF even with zero pages.
        // A 0-page PDF is technically valid per the spec and OwnedPdfWriter does not throw.
        var pageList = new PositionedPageList
        {
            EmbeddedFonts = [],
            Images = new Dictionary<string, DecodedImage>()
        };
        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();

        long bytesWritten = await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);

        bytesWritten.Should().BeGreaterThan(0);
        Encoding.ASCII.GetString(ms.ToArray(), 0, 8).Should().StartWith("%PDF-1.7");
    }
}
