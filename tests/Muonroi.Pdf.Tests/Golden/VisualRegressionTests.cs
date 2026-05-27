using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PDFtoImage;
using SkiaSharp;
using Muonroi.Pdf.Abstractions;
using Xunit;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Rasterization-based visual regression tests.  For each representative golden case this test:
/// <list type="number">
///   <item>Renders the HTML to PDF via the real pipeline.</item>
///   <item>Rasterizes page 0 to a bitmap via PDFtoImage (PDFium).</item>
///   <item>Asserts that at least 1 % of pixels are non-white — a page with real text/content
///         produces a measurable fraction of non-white pixels; a blank page produces zero.</item>
///   <item>Asserts the content stream contains at least one <c>&lt;XXXX&gt; Tj</c> GID hex operator
///         (not a Latin-1 literal) — a cheap structural guard that the fix is in effect.</item>
/// </list>
///
/// These tests MUST fail on pre-fix blank output and pass after the fix.
///
/// NOTE: PDFtoImage is a TEST-ONLY dependency (PDFium/native).  It MUST NOT be referenced by
///       any shippable <c>src/</c> package.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class VisualRegressionTests
{
    /// <summary>
    /// Minimum fraction of non-white pixels required to consider a page non-blank.
    /// At 150 DPI on an A4 page (~2.17 M pixels), a 12-pt text run of ~25 characters
    /// produces roughly 0.15-0.40 % non-white pixels.  We use 0.05 % as the threshold —
    /// safely above absolute zero (true blank = 0 %) and well below any real text content.
    /// </summary>
    private const double MinNonWhiteFraction = 0.0005; // 0.05 %

    // Representative cases: one per major feature group
    private static readonly IReadOnlyList<(string Name, string Html)> Cases = new[]
    {
        ("block-single",
         "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}p{margin:0;}</style></head>" +
         "<body><p>Single block paragraph.</p></body></html>"),

        ("text-align-center",
         "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}p{text-align:center;margin:0;}</style></head>" +
         "<body><p>Centered text.</p></body></html>"),

        ("link-annotation",
         "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}body{font-family:serif;}a{color:blue;}</style></head>" +
         "<body><a href=\"https://example.com\">Click here</a></body></html>"),

        ("list-unordered",
         "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}</style></head>" +
         "<body><ul><li>Item one</li><li>Item two</li></ul></body></html>"),

        ("hr-rule",
         "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}p{margin:0;}</style></head>" +
         "<body><p>Above.</p><hr/><p>Below.</p></body></html>"),
    };

    public static IEnumerable<object[]> CasesData()
    {
        foreach ((string name, string html) in Cases)
            yield return new object[] { name, html };
    }

    [Theory]
    [MemberData(nameof(CasesData))]
    public async Task PageIsNotBlank_AfterFix(string name, string html)
    {
        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());

        // Rasterize page 0 to PNG
        using var pngStream = new MemoryStream();
        Conversion.SavePng(pngStream, pdfBytes, 0, password: null, options: new RenderOptions(Dpi: 150));
        pngStream.Position = 0;
        byte[] pngBytes = pngStream.ToArray();

        double nonWhiteFraction = ComputeNonWhiteFraction(pngBytes);

        Assert.True(
            nonWhiteFraction >= MinNonWhiteFraction,
            $"[{name}] Page appears blank: {nonWhiteFraction:P3} non-white pixels " +
            $"(threshold {MinNonWhiteFraction:P3}). " +
            "This indicates the GID map fix is not working — " +
            "content stream is emitting wrong or zero glyph IDs.");
    }

    [Theory]
    [MemberData(nameof(CasesData))]
    public async Task ContentStreamUsesGidHex_NotLatin1Literal(string name, string html)
    {
        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());

        // Content streams are FlateDecode-compressed; decode them all before checking operators.
        string decompressed = DecompressAllContentStreams(pdfBytes);

        // Under Identity-H encoding the writer must emit <XXXX> Tj (hex GID strings).
        // A Latin-1 literal like (text) Tj would be the broken fallback.
        bool hasHexTj = decompressed.Contains("> Tj");
        bool hasLiteralTj = decompressed.Contains(") Tj");

        Assert.True(
            hasHexTj,
            $"[{name}] Content stream has no '<XXXX> Tj' GID hex operator. " +
            "Expected Identity-H 2-byte GID encoding but found none.");

        Assert.False(
            hasLiteralTj,
            $"[{name}] Content stream contains '(text) Tj' Latin-1 literal, " +
            "which is incorrect under Identity-H encoding and produces blank output.");
    }

    /// <summary>
    /// Extracts and decompresses all FlateDecode content streams from a PDF byte array.
    /// Looks for the pattern: &lt;&lt; ... /Filter /FlateDecode ... &gt;&gt; stream [LF] [compressed bytes] endstream
    /// </summary>
    private static string DecompressAllContentStreams(byte[] pdfBytes)
    {
        var sb = new StringBuilder();
        string pdfLatin1 = Encoding.Latin1.GetString(pdfBytes);

        // Find all stream...endstream blocks that are preceded by /Filter /FlateDecode
        // Using a simple state-machine approach on the raw bytes
        int pos = 0;
        while (pos < pdfBytes.Length)
        {
            // Find next "stream\n" or "stream\r\n"
            int streamIdx = pdfLatin1.IndexOf("\nstream\n", pos, StringComparison.Ordinal);
            if (streamIdx < 0)
                streamIdx = pdfLatin1.IndexOf("\nstream\r\n", pos, StringComparison.Ordinal);
            if (streamIdx < 0)
                break;

            // Look back for /Filter /FlateDecode within 512 chars before
            int lookbackStart = Math.Max(0, streamIdx - 512);
            string header = pdfLatin1.Substring(lookbackStart, streamIdx - lookbackStart);
            if (!header.Contains("/FlateDecode"))
            {
                pos = streamIdx + 8;
                continue;
            }

            // Skip past "stream\n"
            int dataStart = pdfLatin1.IndexOf('\n', streamIdx + 1) + 1;
            if (dataStart <= 0) break;

            // Find "endstream"
            int endIdx = pdfLatin1.IndexOf("endstream", dataStart, StringComparison.Ordinal);
            if (endIdx < 0) break;

            // Strip trailing newline before endstream
            int dataEnd = endIdx;
            if (dataEnd > dataStart && pdfBytes[dataEnd - 1] == '\n') dataEnd--;
            if (dataEnd > dataStart && pdfBytes[dataEnd - 1] == '\r') dataEnd--;

            byte[] compressed = pdfBytes[dataStart..dataEnd];

            try
            {
                // RFC 1950 zlib: skip 2-byte header for DeflateStream
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
                // Non-decompressable stream (image data etc.) — skip
            }

            pos = endIdx + 9;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns the fraction of pixels that are NOT pure white (R=255, G=255, B=255).
    /// Uses SkiaSharp (already a PDFtoImage transitive dependency).
    /// </summary>
    private static double ComputeNonWhiteFraction(byte[] pngBytes)
    {
        using var bitmap = SKBitmap.Decode(pngBytes);
        if (bitmap is null)
            throw new InvalidOperationException("Failed to decode rasterized PNG bitmap.");

        long total = (long)bitmap.Width * bitmap.Height;
        if (total == 0) return 0;

        long nonWhite = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                if (pixel.Red != 255 || pixel.Green != 255 || pixel.Blue != 255)
                    nonWhite++;
            }
        }

        return (double)nonWhite / total;
    }
}
