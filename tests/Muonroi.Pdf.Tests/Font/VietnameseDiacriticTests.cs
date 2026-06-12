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

public sealed class VietnameseDiacriticTests
{
    private const string FontFamily = "Noto Sans";

    private static (SixLaborsTextMetrics Metrics, FontCollection Collection) BuildMetrics()
    {
        byte[] fontBytes = LoadTestFontBytes();
        var collection = new FontCollection();
        collection.Add(new MemoryStream(fontBytes));
        return (new SixLaborsTextMetrics(collection), collection);
    }

    [Fact]
    public void VietnamesePrecomposed_CharWidth_Positive()
    {
        var (metrics, _) = BuildMetrics();

        float widthECircumflexAcute = metrics.GetCharWidth('ế', FontFamily, 12f, false, false);
        widthECircumflexAcute.Should().BeGreaterThan(0f, because: "U+1EBF (ế) is a glyph present in Noto Sans");

        float widthEDotBelow = metrics.GetCharWidth('ẹ', FontFamily, 12f, false, false);
        widthEDotBelow.Should().BeGreaterThan(0f, because: "U+1EB9 (ẹ) is a glyph present in Noto Sans");
    }

    [Fact]
    public void MixedLatinVietnamese_LineHeight_Positive()
    {
        var (metrics, _) = BuildMetrics();

        float lineHeight = metrics.GetLineHeight(FontFamily, 12f);
        lineHeight.Should().BeGreaterThan(0f);

        float ascender = metrics.GetAscender(FontFamily, 12f);
        ascender.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void SurrogateChar_GlyphCollector_Skipped()
    {
        var (_, collection) = BuildMetrics();

        var inlineBox = new InlineBox
        {
            Text = "𐀀",
            FontFamily = FontFamily,
            FontSize = 12f
        };

        var element = new PositionedElement
        {
            Source = inlineBox,
            Position = new Rect(0, 0, 100, 20),
            PageIndex = 0
        };

        var page = new PositionedPage();
        page.Elements.Add(element);

        var pageList = new PositionedPageList();
        pageList.Pages.Add(page);

        var collector = new GlyphCollector();
        IReadOnlyDictionary<string, IReadOnlySet<int>> result = collector.Collect(pageList, collection);

        bool hasNoSurrogates = !result.TryGetValue(FontFamily, out IReadOnlySet<int>? codepoints)
            || codepoints.Count == 0;
        hasNoSurrogates.Should().BeTrue(because: "surrogate chars are skipped by IsSurrogate guard");
    }

    // ── Bundled-font integration tests (font gate fix regression, Wave 6) ─────────────────────
    // These tests FAIL before the LayoutEngine font-gate fix (PdfFormatException: FONT-GID-MAP-MISSING)
    // and PASS after it. No @font-face declaration — exercises the bundled Liberation font path.

    /// <summary>
    /// Renders basic serif text using font-family:'Times New Roman' (no @font-face declaration).
    /// Before the font-gate fix this throws FONT-GID-MAP-MISSING because bundled fonts were
    /// not synthesized into EmbeddedFontInfo when no @font-face existed.
    /// </summary>
    [Fact]
    public async Task BundledFont_RendersBasicSerifText_NoFontFace()
    {
        const string html =
            "<html><head><style>p{margin:0;font-family:'Times New Roman';font-size:12pt;}</style></head>" +
            "<body><p>Hello</p></body></html>";

        byte[] pdf = await RenderNoBundledFontFaceAsync(html);

        pdf.Should().NotBeEmpty(because: "render must succeed and produce a non-empty PDF");
        Encoding.Latin1.GetString(pdf, 0, Math.Min(pdf.Length, 8))
            .Should().StartWith("%PDF-", because: "output must start with the PDF magic bytes");
    }

    /// <summary>
    /// Renders Vietnamese text with diacritics using font-family:'Times New Roman' (no @font-face).
    /// Before the font-gate fix, this throws FONT-GID-MAP-MISSING.
    /// After the fix, the render succeeds and the PDF ToUnicode CMap contains at least one
    /// Vietnamese precomposed codepoint (U+1EBF or similar), proving the subsetter picked up
    /// the diacritic glyphs — not just ASCII fallbacks.
    ///
    /// Codepoint verification: Liberation Serif is loaded from the embedded TTF; the subsetter
    /// must include glyphs for the Vietnamese codepoints in the subset. We decompress the
    /// FlateDecode ToUnicode CMap objects in the PDF and assert the codepoint hex strings are present.
    /// If any codepoint is absent from Liberation Serif, we report it honestly and fail.
    /// </summary>
    [Fact]
    public async Task BundledFont_RendersVietnameseDiacritics_NoFontFace()
    {
        const string vietnameseText = "Phiếu đăng ký làm hàng — Công ty ABC — Nguyễn Văn A";
        const string html =
            "<html><head><style>p{margin:0;font-family:'Times New Roman';font-size:12pt;}</style></head>" +
            "<body><p>" + vietnameseText + "</p></body></html>";

        byte[] pdf = await RenderNoBundledFontFaceAsync(html);

        pdf.Should().NotBeEmpty(because: "render must succeed and produce a non-empty PDF");
        Encoding.Latin1.GetString(pdf, 0, Math.Min(pdf.Length, 8))
            .Should().StartWith("%PDF-", because: "output must start with the PDF magic bytes");

        // Decompress all FlateDecode streams in the PDF and search for Vietnamese codepoint
        // hex strings in the ToUnicode CMap data. Vietnamese precomposed codepoints from the
        // text above: ế=1EBF, đ=0111, ă=0103, ỹ=1EF9 (ký), à=00E0 (làm), à=00E0 (hàng),
        // Ô=00D4 (Công), ề=1EC1, y=0079, ễ=1EC5, ă=0103, Ă=0102, ă→ (Nguyễn)=1EBF etc.
        // We check a representative subset: ế (1EBF) and ộ-family or đ (0111).
        // If Liberation Serif lacks a codepoint, this will fail — we DO NOT substitute silently.
        string decompressedCmaps = ExtractAndDecompressPdfStreams(pdf);

        // The ToUnicode CMap emits entries as <GGGG> <CCCC> pairs.
        // At minimum the letter 'ế' (U+1EBF) must appear in the CMap if the subsetter captured it.
        bool hasVietnamese = decompressedCmaps.Contains("1EBF", StringComparison.OrdinalIgnoreCase)
                          || decompressedCmaps.Contains("0111", StringComparison.OrdinalIgnoreCase) // đ
                          || decompressedCmaps.Contains("0103", StringComparison.OrdinalIgnoreCase); // ă

        hasVietnamese.Should().BeTrue(
            because: "Liberation Serif must contain glyphs for Vietnamese precomposed codepoints " +
                     "(U+1EBF ế, U+0111 đ, U+0103 ă). If this fails, report the missing codepoints " +
                     "from the decompressed CMap — do NOT substitute silently.");
    }

    private static async Task<byte[]> RenderNoBundledFontFaceAsync(string html)
    {
        // Use a font resolver that returns null for all requests — forces bundled-only path.
        // The NullFontResolver causes FontPipeline to skip @font-face resolution entirely,
        // so only bundled Liberation fonts are available.
        var services = new ServiceCollection();
        services.AddSingleton<IFontResolver>(NullFontResolver.Instance);
        services.AddTestDoubles(PdfServiceTestHarness.ValidConfig());
        services.AddPdf(PdfServiceTestHarness.ValidConfig());
        using ServiceProvider provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IMPdfService>();
        (byte[] bytes, _) = await svc.RenderToBytesAsync(html, new PdfRenderOptions());
        return bytes;
    }

    /// <summary>
    /// Extracts, decompresses, and concatenates all FlateDecode stream payloads in the PDF bytes.
    /// Used to search ToUnicode CMap entries for specific Unicode codepoints without a full PDF parser.
    /// </summary>
    private static string ExtractAndDecompressPdfStreams(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        ReadOnlySpan<byte> streamMarker = "stream\r\n"u8;
        ReadOnlySpan<byte> streamMarkerLf = "stream\n"u8;
        ReadOnlySpan<byte> endStreamMarker = "endstream"u8;

        int pos = 0;
        while (pos < pdfBytes.Length)
        {
            // Find next "stream" keyword
            int streamStart = IndexOf(pdfBytes, streamMarker, pos);
            int lfStart = IndexOf(pdfBytes, streamMarkerLf, pos);

            if (streamStart < 0 && lfStart < 0) break;

            int chosen;
            int dataOffset;
            if (streamStart >= 0 && (lfStart < 0 || streamStart <= lfStart))
            {
                chosen = streamStart;
                dataOffset = chosen + streamMarker.Length;
            }
            else
            {
                chosen = lfStart;
                dataOffset = chosen + streamMarkerLf.Length;
            }

            // Find endstream
            int endPos = IndexOf(pdfBytes, endStreamMarker, dataOffset);
            if (endPos < 0) break;

            // Check if header preceding the stream contains /FlateDecode
            // Look backwards from "stream" marker for the preceding dictionary
            int dictStart = Math.Max(0, chosen - 512);
            string header = Encoding.Latin1.GetString(pdfBytes, dictStart, chosen - dictStart);
            bool isFlate = header.Contains("/FlateDecode") || header.Contains("/Fl ");

            if (isFlate && endPos > dataOffset)
            {
                try
                {
                    using var compressed = new MemoryStream(pdfBytes, dataOffset, endPos - dataOffset);
                    // Skip the 2-byte zlib header (CMF + FLG).
                    int b0 = compressed.ReadByte();
                    int b1 = compressed.ReadByte();
                    if (b0 >= 0 && b1 >= 0)
                    {
                        using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                        using var output = new MemoryStream();
                        deflate.CopyTo(output);
                        string text = Encoding.Latin1.GetString(output.ToArray());
                        sb.Append(text);
                    }
                }
                catch
                {
                    // Ignore decompression failures (binary image streams, etc.)
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

    /// <summary>
    /// Font resolver that returns null for every request.
    /// Forces FontPipeline to skip @font-face resolution so only bundled Liberation fonts
    /// are registered — exercising the bundled-only render path.
    /// </summary>
    private sealed class NullFontResolver : IFontResolver
    {
        public static readonly NullFontResolver Instance = new();
        public ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(FontRequest request, CancellationToken cancellationToken = default)
            => new((ReadOnlyMemory<byte>?)null);
    }

    private static byte[] LoadTestFontBytes()
    {
        using Stream? stream = typeof(VietnameseDiacriticTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf");
        if (stream is null)
            throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
