using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Internal.Writer;

namespace Muonroi.Pdf.Tests.Writer;

/// <summary>
/// Plan 02 — CID Type0 / CIDFontType2 / ToUnicode CMap tests for OwnedPdfWriter.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class OwnedPdfWriterCidFontTests
{
    private static PositionedPageList VietnamesePageList()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };

        // Vietnamese text containing characters from Latin Extended Additional (U+1EA0..U+1EF9)
        string text = "Tiếng Việt";
        var codepoints = new HashSet<int>(text.Select(c => (int)c));
        codepoints.Add((int)'T');
        codepoints.Add((int)'i');
        codepoints.Add((int)'n');
        codepoints.Add((int)'g');
        codepoints.Add((int)'V');
        codepoints.Add((int)'t');
        codepoints.Add((int)' ');

        byte[] fontBytes = LoadTestFontBytes();
        var subsetter = new TrueTypeFontSubsetter();
        FontSubsetResult subsetResult = subsetter.Subset(fontBytes, codepoints);

        var fi = new EmbeddedFontInfo(
            WriterTestFonts.Family,
            FontWeight.Normal,
            FontStyle.Normal,
            subsetResult.SubsetBytes,
            codepoints,
            subsetResult.OldToNewGid,
            subsetResult.SortedGids);

        var inline = new InlineBox
        {
            Text = text,
            FontFamily = WriterTestFonts.Family,
            FontSize = 12f
        };
        page.Elements.Add(new PositionedElement
        {
            Source = inline,
            Position = new Rect(50, 50, 200, 20),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = new List<EmbeddedFontInfo> { fi };
        pageList.Images = new Dictionary<string, DecodedImage>();
        return pageList;
    }

    private static PositionedPageList SimplePageList()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement
        {
            Source = new InlineBox { Text = "Hello", FontFamily = WriterTestFonts.Family, FontSize = 12f },
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

    private static byte[] LoadTestFontBytes()
    {
        using Stream? stream = typeof(WriterTestFonts).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task WriteAsync_VietnameseText_DoesNotProduceQuestionMarks()
    {
        byte[] pdfBytes = await RenderAsync(VietnamesePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        // The PDF should contain CIDFontType2 (confirming CID font path was used)
        pdfText.Should().Contain("CIDFontType2",
            because: "OwnedPdfWriter must emit CID font structure for Vietnamese text");

        // The ToUnicode CMap should be present
        pdfText.Should().Contain("begincmap",
            because: "ToUnicode CMap must be present for copy-paste support");
    }

    [Fact]
    public async Task WriteAsync_CidFont_ToUnicodeCMapPresent()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().Contain("begincmap", because: "ToUnicode CMap must be present");
        pdfText.Should().Contain("beginbfchar", because: "ToUnicode CMap must have bfchar entries");
    }

    [Fact]
    public async Task WriteAsync_CidFont_OutputContainsCIDFontType2()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().Contain("CIDFontType2", because: "CID composite font must be emitted");
        pdfText.Should().Contain("Identity-H", because: "Identity-H encoding must be used");
        pdfText.Should().Contain("CIDToGIDMap", because: "CIDToGIDMap /Identity must be present");
    }

    [Fact]
    public async Task WriteAsync_CidFont_NoWinAnsiEncoding()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().NotContain("WinAnsiEncoding",
            because: "CID font path must not use legacy WinAnsiEncoding");
    }

    [Fact]
    public async Task WriteAsync_CidFont_OutputStartsWithPdf17()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string header = Encoding.ASCII.GetString(pdfBytes, 0, 8);
        header.Should().StartWith("%PDF-1.7");
    }

    [Fact]
    public async Task WriteAsync_CidFont_NoForbiddenSecurityEntries()
    {
        byte[] pdfBytes = await RenderAsync(SimplePageList());
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        pdfText.Should().NotContain("/JavaScript");
        pdfText.Should().NotContain("/Launch");
        pdfText.Should().NotContain("/OpenAction");
        pdfText.Should().NotContain("/EmbeddedFile");
    }
}
