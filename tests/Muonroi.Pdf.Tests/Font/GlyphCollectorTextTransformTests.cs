using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Extensions;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Service;
using SixLabors.Fonts;

namespace Muonroi.Pdf.Tests.Font;

/// <summary>
/// G22 regression tests: GlyphCollector must apply text-transform before collecting codepoints
/// so the font subsetter receives the uppercase variants that InlineLayoutEngine emits.
/// </summary>
public sealed class GlyphCollectorTextTransformTests
{
    private const string FontFamily = "Noto Sans";

    private static FontCollection BuildCollection()
    {
        using Stream? stream = typeof(GlyphCollectorTextTransformTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var collection = new FontCollection();
        collection.Add(new MemoryStream(ms.ToArray()));
        return collection;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<int>> Collect(InlineBox box)
    {
        var element = new PositionedElement
        {
            Source = box,
            Position = new Rect(0, 0, 100, 20),
            PageIndex = 0
        };
        var page = new PositionedPage();
        page.Elements.Add(element);
        var pageList = new PositionedPageList();
        pageList.Pages.Add(page);

        var collector = new GlyphCollector();
        return collector.Collect(pageList, BuildCollection());
    }

    // ── Unit tests: uppercase transform ──────────────────────────────────────────────────────────

    /// <summary>
    /// G22 main case: "phiếu" with text-transform:uppercase must yield uppercase codepoints
    /// P/H/I/Ế/U — NOT lowercase p/h/i/ế/u.
    /// </summary>
    [Fact]
    public void TextTransformUppercase_CollectsUppercaseCodepoints()
    {
        var box = new InlineBox
        {
            Text = "phiếu",          // U+0070 U+0068 U+0069 U+1EBF U+0075
            FontFamily = FontFamily,
            FontSize = 12f,
            TextTransform = "uppercase"
        };

        IReadOnlyDictionary<string, IReadOnlySet<int>> result = Collect(box);

        result.Should().ContainKey(FontFamily);
        IReadOnlySet<int> cps = result[FontFamily];

        // Uppercase variants must be present
        cps.Should().Contain('P', because: "U+0050 — uppercase P from 'p'");
        cps.Should().Contain('H', because: "U+0048 — uppercase H from 'h'");
        cps.Should().Contain('I', because: "U+0049 — uppercase I from 'i'");
        cps.Should().Contain(0x1EBE, because: "U+1EBE Ế — uppercase of ế (U+1EBF); the diacritic missing in G22");
        cps.Should().Contain('U', because: "U+0055 — uppercase U from 'u'");

        // Lowercase must NOT be collected (they would not be emitted)
        cps.Should().NotContain('p', because: "lowercase p is not emitted when text-transform:uppercase");
        cps.Should().NotContain('h', because: "lowercase h is not emitted when text-transform:uppercase");
        cps.Should().NotContain('i', because: "lowercase i is not emitted when text-transform:uppercase");
        cps.Should().NotContain(0x1EBF, because: "U+1EBF ế is not emitted when text-transform:uppercase");
        cps.Should().NotContain('u', because: "lowercase u is not emitted when text-transform:uppercase");
    }

    /// <summary>
    /// Regression guard: already-uppercase source with no text-transform must pass through unchanged.
    /// Ensures the fix does not double-transform or alter codepoints when TextTransform is null.
    /// </summary>
    [Fact]
    public void TextTransformNull_AlreadyUppercase_CollectsAsIs()
    {
        var box = new InlineBox
        {
            Text = "PHIẾU",          // U+0050 U+0048 U+0049 U+1EBE U+0055
            FontFamily = FontFamily,
            FontSize = 12f,
            TextTransform = null
        };

        IReadOnlyDictionary<string, IReadOnlySet<int>> result = Collect(box);

        result.Should().ContainKey(FontFamily);
        IReadOnlySet<int> cps = result[FontFamily];

        cps.Should().Contain('P');
        cps.Should().Contain('H');
        cps.Should().Contain('I');
        cps.Should().Contain(0x1EBE, because: "U+1EBE Ế is the source character; no transform applied");
        cps.Should().Contain('U');
    }

    // ── Integration test: end-to-end render → subset cmap contains Ế ────────────────────────────

    /// <summary>
    /// G22 end-to-end: renders &lt;h2 class="text-uppercase"&gt;phiếu&lt;/h2&gt; to PDF bytes and
    /// asserts the embedded font subset's ToUnicode CMap contains U+1EBE (Ế).
    /// Before the G22 fix this assertion fails because only U+1EBF (ế) is in the subset.
    /// </summary>
    [Fact]
    public async Task UppercaseTransform_PdfSubset_ContainsUppercaseVietnameseDiacritic()
    {
        const string html =
            "<html><head><style>" +
            "@font-face { font-family: 'Noto Sans'; src: url('noto-sans.ttf'); }" +
            "h2 { font-family: 'Noto Sans'; font-size: 12pt; }" +
            ".text-uppercase { text-transform: uppercase; }" +
            "</style></head>" +
            "<body><h2 class=\"text-uppercase\">phiếu</h2></body></html>";

        var services = new ServiceCollection();
        services.AddTestDoubles(PdfServiceTestHarness.ValidConfig());
        services.AddPdf(PdfServiceTestHarness.ValidConfig());
        using ServiceProvider provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IMPdfService>();
        (byte[] pdfBytes, _) = await svc.RenderToBytesAsync(html, new PdfRenderOptions());

        pdfBytes.Should().NotBeEmpty(because: "render must succeed");
        Encoding.Latin1.GetString(pdfBytes, 0, Math.Min(pdfBytes.Length, 8))
            .Should().StartWith("%PDF-");

        // Decompress all FlateDecode streams and search for the uppercase diacritic U+1EBE (Ế).
        // The ToUnicode CMap emits entries as <GGGG> <CCCC> pairs; the codepoint hex must appear.
        string cmaps = ExtractAndDecompressPdfStreams(pdfBytes);

        bool hasUppercaseDiacritic = cmaps.Contains("1EBE", StringComparison.OrdinalIgnoreCase);
        hasUppercaseDiacritic.Should().BeTrue(
            because: "U+1EBE Ế must be in the font subset cmap when text-transform:uppercase is applied (G22)");
    }

    // ── Stream decompression helper (same approach as VietnameseDiacriticTests) ─────────────────

    private static string ExtractAndDecompressPdfStreams(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        ReadOnlySpan<byte> streamMarkerCrLf = "stream\r\n"u8;
        ReadOnlySpan<byte> streamMarkerLf = "stream\n"u8;
        ReadOnlySpan<byte> endStreamMarker = "endstream"u8;

        int pos = 0;
        while (pos < pdfBytes.Length)
        {
            int startCrLf = IndexOf(pdfBytes, streamMarkerCrLf, pos);
            int startLf = IndexOf(pdfBytes, streamMarkerLf, pos);

            if (startCrLf < 0 && startLf < 0) break;

            int chosen;
            int dataOffset;
            if (startCrLf >= 0 && (startLf < 0 || startCrLf <= startLf))
            {
                chosen = startCrLf;
                dataOffset = chosen + streamMarkerCrLf.Length;
            }
            else
            {
                chosen = startLf;
                dataOffset = chosen + streamMarkerLf.Length;
            }

            int endPos = IndexOf(pdfBytes, endStreamMarker, dataOffset);
            if (endPos < 0) break;

            int dictStart = Math.Max(0, chosen - 512);
            string header = Encoding.Latin1.GetString(pdfBytes, dictStart, chosen - dictStart);
            bool isFlate = header.Contains("/FlateDecode") || header.Contains("/Fl ");

            if (isFlate && endPos > dataOffset)
            {
                try
                {
                    using var compressed = new MemoryStream(pdfBytes, dataOffset, endPos - dataOffset);
                    int b0 = compressed.ReadByte();
                    int b1 = compressed.ReadByte();
                    if (b0 >= 0 && b1 >= 0)
                    {
                        using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                        using var output = new MemoryStream();
                        deflate.CopyTo(output);
                        sb.Append(Encoding.Latin1.GetString(output.ToArray()));
                    }
                }
                catch
                {
                    // Ignore decompression failures (binary streams, image data, etc.)
                }
            }

            pos = endPos + endStreamMarker.Length;
        }

        return sb.ToString();
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle, int start)
    {
        for (int i = start; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        }
        return -1;
    }
}
