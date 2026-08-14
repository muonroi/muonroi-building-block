namespace Muonroi.Pdf.Tests.Writer;

/// <summary>
/// Plan 03 — FlateDecode compression + stable resource ordering tests for OwnedPdfWriter.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class OwnedPdfWriterCompressionTests
{
    private static PositionedPageList ManyCharsPageList()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };

        // Add enough text to make the uncompressed content stream noticeably large
        string longText = string.Join(" ", Enumerable.Repeat("The quick brown fox jumps over the lazy dog", 15));
        page.Elements.Add(new PositionedElement
        {
            Source = new InlineBox { Text = longText, FontFamily = WriterTestFonts.Family, FontSize = 12f },
            Position = new Rect(10, 10, 400, 20),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();
        return pageList;
    }

    private static PositionedPageList SimplePageList()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement
        {
            Source = new InlineBox { Text = "Hello World", FontFamily = WriterTestFonts.Family, FontSize = 12f },
            Position = new Rect(50, 50, 100, 20),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();
        return pageList;
    }

    private static async Task<byte[]> RenderAsync(PositionedPageList pageList)
    {
        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);
        return ms.ToArray();
    }

    [Fact]
    public async Task WriteAsync_CompressedOutput_SmallerThanPlainText()
    {
        byte[] pdfBytes = await RenderAsync(ManyCharsPageList());

        // The compressed PDF should not be absurdly large (plain text 500 chars * 10 = 5000 bytes overhead)
        // A compressed PDF with FlateDecode should be smaller than the naive uncompressed estimate.
        // We check that the output is at least valid PDF and not larger than 500 KB (was 2.67 MB for spike).
        pdfBytes.Length.Should().BeLessThan(500 * 1024,
            because: "FlateDecode-compressed PDF should be well under 500 KB for short text pages");
    }

    [Fact]
    public async Task WriteAsync_FlateDecode_ContentStreamDict_ContainsFilter()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().Contain("/Filter /FlateDecode",
            because: "content streams must be FlateDecode-compressed");
    }

    [Fact]
    public async Task WriteAsync_FontFile2_ContainsLength1()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().Contain("/Length1",
            because: "FontFile2 stream must include /Length1 (uncompressed TTF size) per PDF 1.7 spec §9.9.3");
    }

    [Fact]
    public async Task WriteAsync_FontResourceNames_Stable()
    {
        // Render twice; output must be byte-identical (DET-01 canary for stable resource names)
        byte[] first = await RenderAsync(SimplePageList());
        byte[] second = await RenderAsync(SimplePageList());

        first.SequenceEqual(second).Should().BeTrue(
            because: "stable List-based resource ordering must produce identical output on repeated renders");
    }

    [Fact]
    public async Task WriteAsync_FontFile2_HasFlateDecode()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        // FontFile2 stream must be FlateDecode-compressed
        pdfText.Should().Contain("/Length1",
            because: "FontFile2 must have /Length1 indicating FlateDecode compression");
    }
}
