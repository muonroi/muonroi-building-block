using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Internal.Writer;

namespace Muonroi.Pdf.Tests.Writer;

[Collection(PdfRenderCollection.Name)]
public sealed class DeterminismTests
{
    private static InlineBox Inline(string text) => new()
    {
        Text = text,
        FontFamily = WriterTestFonts.Family,
        FontSize = 12f
    };

    private static PositionedPageList MultiBoxPageList()
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement { Source = Inline("First line"), Position = new Rect(50, 50, 200, 20), PageIndex = 0 });
        page.Elements.Add(new PositionedElement { Source = Inline("Second line"), Position = new Rect(50, 80, 200, 20), PageIndex = 0 });
        page.Elements.Add(new PositionedElement { Source = Inline("Third line"), Position = new Rect(50, 110, 200, 20), PageIndex = 0 });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();
        return pageList;
    }

    private static PositionedPageList MultiPagePageList()
    {
        var pageList = new PositionedPageList();
        for (int i = 0; i < 3; i++)
        {
            var page = new PositionedPage { PageIndex = i };
            page.Elements.Add(new PositionedElement
            {
                Source = Inline($"Page {i} content"),
                Position = new Rect(50, 50, 200, 20),
                PageIndex = i
            });
            pageList.Pages.Add(page);
        }
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();
        return pageList;
    }

    private static async Task<byte[]> RenderAsync(PositionedPageList pageList, PdfRenderOptions options)
    {
        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, options, ms, CancellationToken.None);
        return ms.ToArray();
    }

    [Fact]
    public async Task WriteAsync_SameInput_TwiceInProcess_ProducesIdenticalBytes()
    {
        var options = new PdfRenderOptions();

        byte[] first = await RenderAsync(MultiBoxPageList(), options);
        byte[] second = await RenderAsync(MultiBoxPageList(), options);

        first.SequenceEqual(second).Should().BeTrue("renders of the same input must be byte-identical (DET-01)");
    }

    [Fact]
    public async Task WriteAsync_SameInput_DifferentOptions_ProducesDifferentOutput()
    {
        byte[] a4 = await RenderAsync(MultiBoxPageList(), new PdfRenderOptions { PageSize = PdfPageSize.A4 });
        byte[] a5 = await RenderAsync(MultiBoxPageList(), new PdfRenderOptions { PageSize = PdfPageSize.A5 });

        a4.SequenceEqual(a5).Should().BeFalse("different page sizes must produce different output");
    }

    [Fact]
    public async Task WriteAsync_MultiPageOutput_DeterministicAcrossRenders()
    {
        var options = new PdfRenderOptions();

        byte[] first = await RenderAsync(MultiPagePageList(), options);
        byte[] second = await RenderAsync(MultiPagePageList(), options);

        first.SequenceEqual(second).Should().BeTrue("multi-page renders of the same input must be byte-identical");
    }
}
