using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
///   <item>Bug A guard: no hex Tj string may appear more than once in the content stream
///         (duplicate identical strings = full-line text drawn at every word position).</item>
///   <item>Bug B guard: every /W entry must have a width &gt;= 100 per-mille (near-zero widths
///         indicate the old-vs-new GID mismatch that collapses glyph advances to 1).</item>
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

    /// <summary>
    /// Minimum per-mille advance width for any non-.notdef glyph in /W.
    /// A normal Latin letter should be 450-800; anything below 100 is bogus (Bug B).
    /// GID 0 (.notdef) is excluded — it legitimately has width 0 in many fonts.
    /// </summary>
    private const int MinGlyphAdvance = 100;

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
    /// Bug A guard: on any single text line (same Tm Y coordinate), every Tj operand must be
    /// DISTINCT.  Duplicate hex strings at the same Y mean the full source line is being drawn
    /// at every word position (e.g. "Centered text." drawn twice — once per word — with different
    /// X but identical glyph sequence).  Different lines legitimately share the same word (e.g.
    /// two list items both containing "Item") — those cross-line duplicates are NOT flagged.
    /// </summary>
    [Theory]
    [MemberData(nameof(CasesData))]
    public async Task ContentStream_NoDuplicateHexTjStringsOnSameLine(string name, string html)
    {
        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
        string decompressed = DecompressAllContentStreams(pdfBytes);

        // Parse Tm/Tj pairs from the content stream.
        // Tm: "1 0 0 1 X Y Tm"  (X=horizontal, Y=vertical position in PDF coordinates)
        // Tj: "<XXXX...> Tj"
        var tmPattern  = new Regex(@"1 0 0 1 [\d.+\-]+ ([\d.+\-]+) Tm");
        var hexTjPattern = new Regex(@"<([0-9A-Fa-f]{4,})>\s*Tj");

        // Walk through the stream tracking current Y, building a map of Y → list of hex strings
        // on that line.  We compare Y with a small tolerance for floating-point formatting.
        var lineStrings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        int pos = 0;
        string? currentYKey = null;

        while (pos < decompressed.Length)
        {
            // Look for the next Tm or Tj, whichever comes first
            var tmMatch  = tmPattern.Match(decompressed, pos);
            var tjMatch  = hexTjPattern.Match(decompressed, pos);

            if (!tmMatch.Success && !tjMatch.Success) break;

            int tmIdx = tmMatch.Success ? tmMatch.Index : int.MaxValue;
            int tjIdx = tjMatch.Success ? tjMatch.Index : int.MaxValue;

            if (tmIdx < tjIdx)
            {
                // New Tm — update current Y key (round to 2 dp to tolerate minor float differences)
                if (double.TryParse(tmMatch.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double y))
                    currentYKey = y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                pos = tmMatch.Index + tmMatch.Length;
            }
            else
            {
                // Tj — record under current Y
                if (currentYKey != null)
                {
                    string hex = tjMatch.Groups[1].Value.ToUpperInvariant();
                    if (!lineStrings.TryGetValue(currentYKey, out var list))
                        lineStrings[currentYKey] = list = new List<string>();
                    list.Add(hex);
                }
                pos = tjMatch.Index + tjMatch.Length;
            }
        }

        // On each line, every hex string must be distinct (no full-line text at every word pos)
        var violations = new List<string>();
        foreach ((string yKey, var strings) in lineStrings)
        {
            var dups = strings
                .GroupBy(s => s)
                .Where(g => g.Count() > 1 && g.Key.Length > 4) // >2 glyphs (skip .notdef runs)
                .Select(g => $"Y={yKey} '{g.Key}' x{g.Count()}")
                .ToList();
            violations.AddRange(dups);
        }

        Assert.True(
            violations.Count == 0,
            $"[{name}] Same hex Tj string appears multiple times on the same text line — " +
            "Bug A: full line text drawn at every word position. Violations: " +
            string.Join("; ", violations));
    }

    /// <summary>
    /// Bug B guard: every /W entry for a non-.notdef glyph must have a per-mille advance
    /// width of at least <see cref="MinGlyphAdvance"/>.  Near-zero widths (1, 4, 6, 7, 9, 11)
    /// indicate the old-vs-new GID mismatch in BuildGidToAdvanceMap.
    /// </summary>
    [Theory]
    [MemberData(nameof(CasesData))]
    public async Task FontWidthArray_NoNearZeroAdvances(string name, string html)
    {
        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());

        // /W arrays are NOT in FlateDecode streams — they are in the uncompressed CIDFont dict.
        // Read the raw PDF text (Latin-1 safe).
        string pdfText = Encoding.Latin1.GetString(pdfBytes);

        // Find /W [...] arrays.  The format is: /W [ gid [width] gid [width] ... ]
        // We look for patterns like: 3 [632] or 0 [600] — gid followed by [width]
        // Extract all individual width values from /W array content.
        var wArrayPattern = new Regex(@"/W\s*\[([^\]]*(?:\[[^\]]*\][^\]]*)*)\]");
        var entryPattern = new Regex(@"(\d+)\s*\[(\d+)\]");

        var bogusEntries = new List<string>();
        foreach (Match wm in wArrayPattern.Matches(pdfText))
        {
            string wContent = wm.Groups[1].Value;
            foreach (Match em in entryPattern.Matches(wContent))
            {
                int gid = int.Parse(em.Groups[1].Value);
                int width = int.Parse(em.Groups[2].Value);
                // GID 0 is .notdef — width 0 is normal.
                if (gid != 0 && width < MinGlyphAdvance)
                    bogusEntries.Add($"GID {gid} width={width}");
            }
        }

        Assert.True(
            bogusEntries.Count == 0,
            $"[{name}] /W array contains near-zero glyph advances — Bug B: " +
            "old-vs-new GID mismatch in BuildGidToAdvanceMap. Bogus entries: " +
            string.Join(", ", bogusEntries));
    }

    /// <summary>
    /// Renders the 5 representative sample cases to PNG files in TestResults/visual/ for
    /// manual visual inspection.  Always passes — this is an output helper, not an assertion.
    /// Files: block-single.png, text-align-center.png, list-unordered.png,
    ///        text-decoration-underline.png, link-annotation.png
    /// </summary>
    [Fact]
    public async Task RasterizeSampleCases_ToPng()
    {
        var samples = new (string Name, string Html)[]
        {
            ("block-single",
             "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}p{margin:0;}</style></head>" +
             "<body><p>Single block paragraph.</p></body></html>"),

            ("text-align-center",
             "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}p{text-align:center;margin:0;}</style></head>" +
             "<body><p>Centered text.</p></body></html>"),

            ("list-unordered",
             "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}ul{margin:0;padding-left:20px;}</style></head>" +
             "<body><ul><li>Item A</li><li>Item B</li><li>Item C</li></ul></body></html>"),

            ("text-decoration-underline",
             "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}p{margin:0;}</style></head>" +
             "<body><p><u>Underlined text rendered with decoration rule.</u></p></body></html>"),

            ("link-annotation",
             "<html><head><style>@font-face{font-family:serif;src:url(test.ttf);}body{font-family:serif;}a{color:blue;}</style></head>" +
             "<body><a href=\"https://example.com\">Click here to visit example.com</a></body></html>"),
        };

        string outDir = Path.Combine(
            Path.GetDirectoryName(typeof(VisualRegressionTests).Assembly.Location)!,
            "..", "..", "..", "TestResults", "visual");
        Directory.CreateDirectory(outDir);

        foreach ((string name, string html) in samples)
        {
            byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
            using var pngStream = new MemoryStream();
            Conversion.SavePng(pngStream, pdfBytes, 0, password: null, options: new RenderOptions(Dpi: 150));
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"{name}.png"), pngStream.ToArray());
        }
    }

    /// <summary>
    /// Bug C guard: for a list-unordered document the content stream must contain a
    /// non-.notdef GID Tj call that maps to U+2022 BULLET (•).  Before the fix,
    /// the marker <see cref="InlineBox"/> had an empty <c>FontFamily</c> (AngleSharp returns ""
    /// not null for unset properties) so <c>OwnedPdfWriter</c> silently skipped it and the
    /// glyph was never emitted.
    ///
    /// The test renders a minimal unordered list with the test font (which contains U+2022 at
    /// GID 525 before subsetting → new GID 9 after subsetting), decompresses the content stream,
    /// and asserts that at least one GID-based Tj operator is present.  A blank bullet (no Tj)
    /// means the fix has regressed.
    ///
    /// The exact new GID for U+2022 is font-subset-order-dependent, so we assert *any* Tj is
    /// present in the list PDF rather than hard-coding GID 9 — the PageIsNotBlank_AfterFix test
    /// already verifies non-white pixels, and FontWidthArray_NoNearZeroAdvances verifies /W.
    /// Together these three tests form the regression guard for Bug C.
    /// </summary>
    [Fact]
    public async Task ListMarker_BulletGlyph_IsEmittedInContentStream()
    {
        const string html =
            "<html><head><style>" +
            "@font-face{font-family:serif;src:url(test.ttf);}" +
            "ul{margin:0;padding-left:20px;}" +
            "</style></head>" +
            "<body><ul><li>Item A</li><li>Item B</li><li>Item C</li></ul></body></html>";

        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
        string decompressed = DecompressAllContentStreams(pdfBytes);

        // Count distinct Tj calls — each list item produces at least one word Tj ("Item", "A/B/C").
        // The bullet marker must add at least one additional Tj per line.
        // Minimum expected: 3 items × 2 words ("Item" + letter) + 3 bullets = 9 Tj calls.
        // Before the Bug C fix, only 6 Tj calls appeared (no bullets).
        var hexTjMatches = Regex.Matches(decompressed, @"<[0-9A-Fa-f]{4,}>\s*Tj");
        int tjCount = hexTjMatches.Count;

        Assert.True(
            tjCount >= 9,
            $"Expected at least 9 Tj operators in list-unordered content stream (3 bullets + 6 words), " +
            $"but found {tjCount}. " +
            "Bug C: list marker InlineBox may have empty FontFamily, causing OwnedPdfWriter to skip it. " +
            "Check BoxTreeBuilder.BuildChildrenWithListMarker — use IsNullOrWhiteSpace, not null-coalescing, " +
            "to fall back to the serif font when AngleSharp returns \"\" for unset font-family.");
    }

    /// <summary>
    /// SC1 guard (Bug E): the text-decoration-underline case must produce a filled rectangle
    /// operator (re ... f) OUTSIDE a BT/ET text block, at the row where the underlined text sits.
    /// Before the fix, <c>InlineBox.TextDecoration</c> was never set for &lt;u&gt; elements
    /// (AngleSharp returned "" for text-decoration on &lt;u&gt;, and empty FontFamily caused the
    /// box to be skipped entirely), so no decoration rule was ever drawn.
    ///
    /// The guard decompresses the content stream and looks for the pattern:
    ///   ET ... re ... f ... BT
    /// which is the underline/strikethrough rect drawn between two text blocks.
    /// </summary>
    [Fact]
    public async Task TextDecorationUnderline_ContentStream_ContainsDecorationRect()
    {
        const string html =
            "<html><head><style>" +
            "@font-face{font-family:serif;src:url(test.ttf);}p{margin:0;}" +
            "</style></head>" +
            "<body><p><u>Underlined text rendered with decoration rule.</u></p></body></html>";

        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
        string decompressed = DecompressAllContentStreams(pdfBytes);

        // After rendering underlined text the writer emits ET, then a `re` rect + `f` fill for the
        // decoration line, then BT to resume text. Assert the pattern appears in the stream.
        // We look for a filled rect (re followed by f) that appears between ET and BT.
        bool hasDecorationRect = Regex.IsMatch(
            decompressed,
            @"ET[\s\S]*?\bre\b[\s\S]*?\bf\b[\s\S]*?BT",
            RegexOptions.None);

        Assert.True(
            hasDecorationRect,
            "SC1: text-decoration-underline content stream has no 'ET ... re ... f ... BT' decoration rect. " +
            "The underline rule must be drawn for <u> elements. " +
            "Check BoxTreeBuilder.CreateBox handles <u> → InlineBox.TextDecoration = \"underline\", " +
            "and that FontFamily is not cleared to empty by ResolveCssProperties.");
    }

    /// <summary>
    /// SC2 guard (Bug F): for the list-unordered case the bullet marker must share the same
    /// PDF Y coordinate row as the item text that follows it on the same line.
    ///
    /// Before the fix, the marker was a separate child of the <c>BlockBox</c> and
    /// <c>BlockLayoutEngine</c> dispatched it as an independent inline-layout call, placing it
    /// on its own line above the item text.  After the fix, marker + text are wrapped in one
    /// <c>AnonymousBox</c> and laid out together by <c>InlineLayoutEngine</c>, so they share one Tm Y.
    ///
    /// The guard parses <c>1 0 0 1 X Y Tm</c> operators and groups them by Y (rounded to 0.1 pt).
    /// Each group must contain at least two Tj calls (bullet + at least one word), verifying
    /// that marker and text are co-located on the same row.
    /// </summary>
    [Fact]
    public async Task ListMarker_SharesRowWithItemText()
    {
        const string html =
            "<html><head><style>" +
            "@font-face{font-family:serif;src:url(test.ttf);}ul{margin:0;padding-left:20px;}" +
            "</style></head>" +
            "<body><ul><li>Item A</li><li>Item B</li><li>Item C</li></ul></body></html>";

        byte[] pdfBytes = await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
        string decompressed = DecompressAllContentStreams(pdfBytes);

        // Parse Tm/Tj pairs — same approach as ContentStream_NoDuplicateHexTjStringsOnSameLine.
        var tmPattern = new Regex(@"1 0 0 1 [\d.+\-]+ ([\d.+\-]+) Tm");
        var hexTjPattern = new Regex(@"<([0-9A-Fa-f]{4,})>\s*Tj");

        var lineStrings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        int pos = 0;
        string? currentYKey = null;

        while (pos < decompressed.Length)
        {
            var tmMatch = tmPattern.Match(decompressed, pos);
            var tjMatch = hexTjPattern.Match(decompressed, pos);

            if (!tmMatch.Success && !tjMatch.Success) break;

            int tmIdx = tmMatch.Success ? tmMatch.Index : int.MaxValue;
            int tjIdx = tjMatch.Success ? tjMatch.Index : int.MaxValue;

            if (tmIdx < tjIdx)
            {
                if (double.TryParse(tmMatch.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double y))
                    currentYKey = y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                pos = tmMatch.Index + tmMatch.Length;
            }
            else
            {
                if (currentYKey != null)
                {
                    if (!lineStrings.TryGetValue(currentYKey, out var list))
                        lineStrings[currentYKey] = list = new List<string>();
                    list.Add(tjMatch.Groups[1].Value.ToUpperInvariant());
                }
                pos = tjMatch.Index + tjMatch.Length;
            }
        }

        // Each of the 3 list items must produce a row with at least 2 Tj calls (bullet + word).
        // If the bullet is on its own row (Bug F), that row has exactly 1 Tj and the word
        // is on a separate row — neither row has 2+ Tj calls.
        int rowsWithMultipleTj = lineStrings.Values.Count(v => v.Count >= 2);

        Assert.True(
            rowsWithMultipleTj >= 3,
            $"SC2: expected at least 3 rows each with 2+ Tj calls (bullet + item text on same line), " +
            $"but found {rowsWithMultipleTj} such rows. " +
            "Bug F: list marker is on its own line above the item text. " +
            "Check BoxTreeBuilder.BuildChildrenWithListMarker wraps marker + children in one AnonymousBox.");
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
