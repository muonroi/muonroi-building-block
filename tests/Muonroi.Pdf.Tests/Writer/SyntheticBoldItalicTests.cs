using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Internal.Writer;
using Muonroi.Pdf.Tests.Golden;

namespace Muonroi.Pdf.Tests.Writer;

/// <summary>
/// G23f — synthetic bold (text stroke, Tr=2) + italic (Tm skew 0.2) in OwnedPdfWriter.
///
/// These tests verify that OwnedPdfWriter emits the correct PDF operators when
/// InlineBox.Bold and/or InlineBox.Italic are set, producing visually distinct text
/// even when the resolved font has no separate bold/italic variant.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class SyntheticBoldItalicTests
{
    // HTML template: @font-face declares "serif" so the writer uses the embedded test font.
    // .bold-text and .italic-text classes use explicit CSS properties to guarantee cascade without
    // relying on AngleSharp's UA stylesheet propagating font-weight for <strong>/<em> in all modes.
    private static string WrapHtml(string body) =>
        "<html><head><style>" +
        "@font-face{font-family:serif;src:url(test.ttf);}" +
        "body{font-family:serif;font-size:13px;}" +
        ".bold-text{font-weight:bold;}" +
        ".italic-text{font-style:italic;}" +
        ".bold-italic-text{font-weight:bold;font-style:italic;}" +
        "</style></head><body>" + body + "</body></html>";

    // -----------------------------------------------------------------------
    // Full-pipeline render via GoldenPdf.RenderAsync (IMPdfService + real layout).
    // -----------------------------------------------------------------------

    private static async Task<string> RenderAndDecompressAsync(string html)
    {
        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
        return DecompressAllContentStreams(pdfBytes);
    }

    // -----------------------------------------------------------------------
    // Low-level render: OwnedPdfWriter directly with a hand-built PositionedPageList.
    // Used to verify writer-level emission with explicit Bold/Italic flags.
    // -----------------------------------------------------------------------

    private static async Task<string> RenderInlineAndDecompressAsync(
        string text, bool bold = false, bool italic = false, float fontSize = 13f)
    {
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };

        var inline = new InlineBox
        {
            Text = text,
            FontFamily = WriterTestFonts.Family,
            FontSize = fontSize,
            Bold = bold,
            Italic = italic,
        };

        page.Elements.Add(new PositionedElement
        {
            Source = inline,
            RenderedText = text,
            Position = new Rect(50, 50, 200, 20),
            PageIndex = 0,
        });

        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();

        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);
        return DecompressAllContentStreams(ms.ToArray());
    }

    // -----------------------------------------------------------------------
    // Synthetic bold: Tr=2 + RG stroke + 0 Tr reset.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Bold_ContentStream_Contains2Tr()
    {
        string content = await RenderInlineAndDecompressAsync("bold text", bold: true);

        content.Should().Contain("2 Tr",
            because: "synthetic bold must switch to fill+stroke rendering mode (Tr=2)");
    }

    [Fact]
    public async Task Bold_ContentStream_Contains0TrReset()
    {
        string content = await RenderInlineAndDecompressAsync("bold text", bold: true);

        content.Should().Contain("0 Tr",
            because: "synthetic bold must reset to fill-only rendering mode (Tr=0) after Tj");
    }

    [Fact]
    public async Task Bold_ContentStream_ContainsStrokeColorRG()
    {
        string content = await RenderInlineAndDecompressAsync("bold text", bold: true);

        content.Should().Contain(" RG",
            because: "synthetic bold must set stroke color (RG operator) to match fill color");
    }

    [Fact]
    public async Task Bold_ResetAfterTj_PrecedesNextText()
    {
        // Two-word inline — the "0 Tr" must appear between the two Tj calls.
        // Build a page with two inline elements: one bold, one plain.
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };

        var boldInline = new InlineBox
        {
            Text = "bold",
            FontFamily = WriterTestFonts.Family,
            FontSize = 13f,
            Bold = true,
        };
        var plainInline = new InlineBox
        {
            Text = "plain",
            FontFamily = WriterTestFonts.Family,
            FontSize = 13f,
            Bold = false,
        };

        page.Elements.Add(new PositionedElement
        {
            Source = boldInline,
            RenderedText = "bold",
            Position = new Rect(50, 50, 80, 20),
            PageIndex = 0,
        });
        page.Elements.Add(new PositionedElement
        {
            Source = plainInline,
            RenderedText = "plain",
            Position = new Rect(140, 50, 80, 20),
            PageIndex = 0,
        });

        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = WriterTestFonts.Embedded();
        pageList.Images = new Dictionary<string, DecodedImage>();

        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, new PdfRenderOptions(), ms, CancellationToken.None);
        string content = DecompressAllContentStreams(ms.ToArray());

        // The stream must have "2 Tr" before the bold Tj, then "0 Tr" before the plain Tj.
        int idx2Tr = content.IndexOf("2 Tr", StringComparison.Ordinal);
        int idx0Tr = content.IndexOf("0 Tr", StringComparison.Ordinal);

        idx2Tr.Should().BeGreaterThanOrEqualTo(0, because: "bold run must emit 2 Tr");
        idx0Tr.Should().BeGreaterThan(idx2Tr, because: "0 Tr reset must follow 2 Tr");

        // "plain" Tj must come AFTER the "0 Tr" reset
        int idxPlainTj = content.IndexOf("> Tj", idx0Tr, StringComparison.Ordinal);
        idxPlainTj.Should().BeGreaterThan(idx0Tr,
            because: "non-bold text must be emitted after the 0 Tr reset so it renders in fill-only mode");
    }

    // -----------------------------------------------------------------------
    // Synthetic italic: Tm matrix must have skew factor 0.2 as c-term.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Italic_ContentStream_ContainsSkewedTm()
    {
        string content = await RenderInlineAndDecompressAsync("italic text", italic: true);

        content.Should().Contain("1 0 0.2 1",
            because: "synthetic italic must emit a skewed text matrix (c=0.2) in the Tm operator");
    }

    [Fact]
    public async Task Italic_ContentStream_TmHasTmOperator()
    {
        string content = await RenderInlineAndDecompressAsync("italic text", italic: true);

        // Full pattern: "1 0 0.2 1 <x> <y> Tm"
        content.Should().MatchRegex(@"1 0 0\.2 1 [\d\.\-]+ [\d\.\-]+ Tm",
            because: "synthetic italic Tm operator must be followed by position coordinates and Tm keyword");
    }

    // -----------------------------------------------------------------------
    // Combined bold + italic: both effects present simultaneously.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BoldItalic_ContentStream_ContainsBothEffects()
    {
        string content = await RenderInlineAndDecompressAsync("bold italic", bold: true, italic: true);

        content.Should().Contain("2 Tr",
            because: "bold+italic must still emit Tr=2 for synthetic bold");
        content.Should().Contain("0 Tr",
            because: "bold+italic must still reset to Tr=0 after Tj");
        content.Should().Contain("1 0 0.2 1",
            because: "bold+italic must still skew the Tm matrix for synthetic italic");
        content.Should().Contain(" RG",
            because: "bold+italic must still emit stroke color RG operator");
    }

    // -----------------------------------------------------------------------
    // Plain text regression guard: no synthetic effects on unstyled text.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Plain_ContentStream_NoSyntheticBoldOrItalic()
    {
        string content = await RenderInlineAndDecompressAsync("plain text", bold: false, italic: false);

        content.Should().NotContain("2 Tr",
            because: "plain text must NOT emit Tr=2 (fill+stroke mode)");
        content.Should().NotContain("1 0 0.2 1",
            because: "plain text must NOT skew the Tm matrix (no italic)");
    }

    // -----------------------------------------------------------------------
    // Tiny font guard: no stroke below 8pt (anti-artifact rule).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Bold_TinyFont_NoStrokeEmitted()
    {
        // Font size 7pt is below the 8pt threshold — must NOT emit synthetic bold operators.
        string content = await RenderInlineAndDecompressAsync("small bold", bold: true, fontSize: 7f);

        content.Should().NotContain("2 Tr",
            because: "bold text at <8pt must not emit Tr=2 to avoid noisy stroke artifacts");
        content.Should().NotContain(" RG",
            because: "bold text at <8pt must not emit stroke color RG to avoid noisy stroke artifacts");
    }

    // -----------------------------------------------------------------------
    // Full-pipeline smoke: <strong> via GoldenPdf.RenderAsync (HTML→layout→writer).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FullPipeline_BoldCssClass_EmitsSyntheticBold()
    {
        // Use explicit CSS class font-weight:bold to guarantee cascade without relying on
        // AngleSharp's UA stylesheet propagating font-weight for <strong> in all modes.
        string content = await RenderAndDecompressAsync(
            WrapHtml("<p><span class=\"bold-text\">bold text</span></p>"));

        content.Should().Contain("2 Tr",
            because: "font-weight:bold via CSS class must produce Bold=true on InlineBox, triggering synthetic bold (Tr=2)");
        content.Should().Contain("0 Tr",
            because: "synthetic bold reset (Tr=0) must follow the bold Tj in the content stream");
        content.Should().Contain(" RG",
            because: "stroke color (RG) must be emitted for synthetic bold");
    }

    [Fact]
    public async Task FullPipeline_ItalicCssClass_EmitsSyntheticItalic()
    {
        string content = await RenderAndDecompressAsync(
            WrapHtml("<p><span class=\"italic-text\">italic text</span></p>"));

        content.Should().Contain("1 0 0.2 1",
            because: "font-style:italic via CSS class must produce Italic=true on InlineBox, triggering skewed Tm matrix (c=0.2)");
    }

    [Fact]
    public async Task FullPipeline_BoldItalicCssClass_EmitsBothEffects()
    {
        string content = await RenderAndDecompressAsync(
            WrapHtml("<p><span class=\"bold-italic-text\">combined</span></p>"));

        content.Should().Contain("2 Tr",
            because: "bold+italic combined must emit Tr=2");
        content.Should().Contain("0 Tr",
            because: "bold+italic combined must reset to Tr=0");
        content.Should().Contain("1 0 0.2 1",
            because: "bold+italic combined must skew the Tm matrix");
    }

    [Fact]
    public async Task FullPipeline_PlainParagraph_NoSyntheticEffects()
    {
        string content = await RenderAndDecompressAsync(WrapHtml("<p>plain text</p>"));

        content.Should().NotContain("2 Tr",
            because: "plain text must not emit Tr=2");
        content.Should().NotContain("1 0 0.2 1",
            because: "plain text must not use skewed Tm matrix");
    }

    // -----------------------------------------------------------------------
    // Helper: extract and decompress all FlateDecode content streams from PDF bytes.
    // Copied from VisualRegressionTests — intentionally inlined to keep this test
    // class self-contained without a shared test-only utility project.
    // -----------------------------------------------------------------------

    private static string DecompressAllContentStreams(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        string pdfLatin1 = Encoding.Latin1.GetString(pdfBytes);

        int pos = 0;
        while (pos < pdfBytes.Length)
        {
            int streamIdx = pdfLatin1.IndexOf("\nstream\n", pos, StringComparison.Ordinal);
            if (streamIdx < 0)
                streamIdx = pdfLatin1.IndexOf("\nstream\r\n", pos, StringComparison.Ordinal);
            if (streamIdx < 0)
                break;

            int lookbackStart = Math.Max(0, streamIdx - 512);
            string header = pdfLatin1.Substring(lookbackStart, streamIdx - lookbackStart);
            if (!header.Contains("/FlateDecode"))
            {
                pos = streamIdx + 8;
                continue;
            }

            int dataStart = pdfLatin1.IndexOf('\n', streamIdx + 1) + 1;
            if (dataStart <= 0) break;

            int endIdx = pdfLatin1.IndexOf("endstream", dataStart, StringComparison.Ordinal);
            if (endIdx < 0) break;

            int dataEnd = endIdx;
            if (dataEnd > dataStart && pdfBytes[dataEnd - 1] == '\n') dataEnd--;
            if (dataEnd > dataStart && pdfBytes[dataEnd - 1] == '\r') dataEnd--;

            byte[] compressed = pdfBytes[dataStart..dataEnd];

            try
            {
                if (compressed.Length >= 2)
                {
                    using var ms = new MemoryStream(compressed, 2, compressed.Length - 2);
                    using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    sb.Append(Encoding.Latin1.GetString(output.ToArray()));
                }
            }
            catch
            {
                // Non-decompressable stream (font data, image etc.) — skip.
            }

            pos = endIdx + 9;
        }

        return sb.ToString();
    }
}
