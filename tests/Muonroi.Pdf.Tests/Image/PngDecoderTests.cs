namespace Muonroi.Pdf.Tests.Image;

/// <summary>
/// PngDecoder palette (color_type=3), RGBA (color_type=6), and grayscale (color_type=0/4) support.
/// Tests cover: palette without alpha, palette with tRNS, RGBA compositing, grayscale + grayscale-alpha
/// decoding, 16-bit rejection, and a full render-pipeline smoke test with an inline RGBA data URI.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class PngDecoderTests
{
    // ── 1. Palette without alpha ─────────────────────────────────────────────

    [Fact]
    public void PngDecoder_decodes_palette_without_alpha()
    {
        byte[] png = PngFixtureBuilder.Palette4Color();
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(png, "image/png");

        result.Width.Should().Be(16);
        result.Height.Should().Be(16);
        result.ContentType.Should().Be("image/png");
        result.Data.Length.Should().BeGreaterThan(0);

        // Run through the writer's full PNG→raw-RGB decode to confirm no exception
        // and correct output size (width * height * 3 bytes).
        byte[] rawRgb = RoundTripPngToRawRgb(png, 16, 16);
        rawRgb.Length.Should().Be(16 * 16 * 3);

        // Spot-check: the first pixel (top-left, block 0 = red) should decode to ~(255,0,0)
        rawRgb[0].Should().Be(255, "red channel of index-0 pixel");
        rawRgb[1].Should().Be(0,   "green channel of index-0 pixel");
        rawRgb[2].Should().Be(0,   "blue channel of index-0 pixel");
    }

    // ── 2. Palette with tRNS — transparent colour composited onto white ──────

    [Fact]
    public void PngDecoder_decodes_palette_with_trns_composites_to_white()
    {
        byte[] png = PngFixtureBuilder.PaletteTrns();
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(png, "image/png");

        result.Width.Should().Be(16);
        result.Height.Should().Be(16);

        byte[] rawRgb = RoundTripPngToRawRgb(png, 16, 16);
        rawRgb.Length.Should().Be(16 * 16 * 3);

        // Index 0 = red (#FF0000) with alpha=0 → composited onto white → should be (255,255,255)
        rawRgb[0].Should().Be(255, "fully-transparent pixel composited onto white — R");
        rawRgb[1].Should().Be(255, "fully-transparent pixel composited onto white — G");
        rawRgb[2].Should().Be(255, "fully-transparent pixel composited onto white — B");

        // Index 1 = green (#00FF00) with alpha=255 (opaque) → should be (0,255,0)
        // First index-1 pixel starts at column 4 in row 0: offset = 4 * 3
        rawRgb[4 * 3].Should().Be(0,   "opaque green pixel — R");
        rawRgb[4 * 3 + 1].Should().Be(255, "opaque green pixel — G");
        rawRgb[4 * 3 + 2].Should().Be(0,   "opaque green pixel — B");
    }

    // ── 3. RGBA composited onto white ────────────────────────────────────────

    [Fact]
    public void PngDecoder_decodes_rgba_composites_to_white()
    {
        byte[] png = PngFixtureBuilder.RgbaLogo();
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(png, "image/png");

        result.Width.Should().Be(32);
        result.Height.Should().Be(32);

        byte[] rawRgb = RoundTripPngToRawRgb(png, 32, 32);
        rawRgb.Length.Should().Be(32 * 32 * 3);

        // Top row (y=0): alpha=255 (opaque) → pixel = (50, 100, 200)
        rawRgb[0].Should().Be(50,  "top-row R — fully opaque blue pixel");
        rawRgb[1].Should().Be(100, "top-row G — fully opaque blue pixel");
        rawRgb[2].Should().Be(200, "top-row B — fully opaque blue pixel");

        // Bottom row (y=31): alpha=0 (transparent) → composited onto white → (255,255,255)
        int lastRowStart = 31 * 32 * 3;
        rawRgb[lastRowStart].Should().Be(255,     "bottom-row R — fully transparent composited to white");
        rawRgb[lastRowStart + 1].Should().Be(255, "bottom-row G — fully transparent composited to white");
        rawRgb[lastRowStart + 2].Should().Be(255, "bottom-row B — fully transparent composited to white");
    }

    // ── 4. Grayscale (color_type=0) decodes to R=G=B ─────────────────────────

    [Fact]
    public void PngDecoder_decodes_grayscale_to_rgb()
    {
        byte[] png = PngFixtureBuilder.Gray8();
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(png, "image/png");

        result.Width.Should().Be(8);
        result.Height.Should().Be(8);

        byte[] rawRgb = RoundTripPngToRawRgb(png, 8, 8);
        rawRgb.Length.Should().Be(8 * 8 * 3);

        // Column 0 gray=0 (black) → (0,0,0)
        rawRgb[0].Should().Be(0, "column-0 gray sample expanded to R");
        rawRgb[1].Should().Be(0, "column-0 gray sample expanded to G");
        rawRgb[2].Should().Be(0, "column-0 gray sample expanded to B");

        // Column 4 gray=128 → (128,128,128)
        rawRgb[4 * 3].Should().Be(128,     "column-4 gray sample expanded to R");
        rawRgb[4 * 3 + 1].Should().Be(128, "column-4 gray sample expanded to G");
        rawRgb[4 * 3 + 2].Should().Be(128, "column-4 gray sample expanded to B");
    }

    // ── 4b. Grayscale+alpha (color_type=4) composites onto white ─────────────

    [Fact]
    public void PngDecoder_decodes_grayscale_alpha_composites_to_white()
    {
        byte[] png = PngFixtureBuilder.GrayAlpha8();
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(png, "image/png");

        result.Width.Should().Be(8);
        result.Height.Should().Be(8);

        byte[] rawRgb = RoundTripPngToRawRgb(png, 8, 8);
        rawRgb.Length.Should().Be(8 * 8 * 3);

        // Top row (y=0): alpha=255 (opaque) → gray 200 → (200,200,200)
        rawRgb[0].Should().Be(200, "top-row opaque gray — R");
        rawRgb[1].Should().Be(200, "top-row opaque gray — G");
        rawRgb[2].Should().Be(200, "top-row opaque gray — B");

        // Bottom row (y=7): alpha=0 (transparent) → composited onto white → (255,255,255)
        int lastRowStart = 7 * 8 * 3;
        rawRgb[lastRowStart].Should().Be(255,     "bottom-row transparent composited to white — R");
        rawRgb[lastRowStart + 1].Should().Be(255, "bottom-row transparent composited to white — G");
        rawRgb[lastRowStart + 2].Should().Be(255, "bottom-row transparent composited to white — B");
    }

    // ── 4c. 16-bit grayscale still fails loud ────────────────────────────────

    [Fact]
    public void PngDecoder_16bit_grayscale_throws_clear_error()
    {
        // color_type=0, bit_depth=16 — 16-bit samples not supported
        byte[] pngBytes = BuildMinimalPngHeader(bitDepth: 16, colorType: 0);
        var decoder = new PureImageDecoder();

        Action act = () => decoder.Decode(pngBytes, "image/png");

        act.Should().Throw<PdfFormatException>()
           .Which.RuleId.Should().Be("PNG-16BIT");
    }

    // ── 5. Smoke: RGBA inline data URI → PDF render returns non-zero stream ──

    [Fact]
    public async Task PngDecoder_rgba_inline_datauri_renders_to_pdf_smoke()
    {
        byte[] rgbaPng = PngFixtureBuilder.RgbaLogo();
        string base64 = Convert.ToBase64String(rgbaPng);
        string dataUri = $"data:image/png;base64,{base64}";

        // Build a PositionedPageList with the RGBA PNG decoded and embedded
        var decoder = new PureImageDecoder();
        DecodedImage decoded = decoder.Decode(rgbaPng, "image/png");

        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement
        {
            Source = new ReplacedBox { Src = dataUri, NaturalWidth = 32, NaturalHeight = 32 },
            Position = new Rect(10, 10, 32, 32),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = [];
        pageList.Images = new Dictionary<string, DecodedImage> { [dataUri] = decoded };

        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        await writer.WriteAsync(pageList, new Muonroi.Pdf.Abstractions.PdfRenderOptions(), ms, CancellationToken.None);

        ms.Length.Should().BeGreaterThan(0, "PDF stream must be non-empty");

        // Verify it starts with %PDF
        ms.Position = 0;
        string header = Encoding.ASCII.GetString(ms.ToArray(), 0, 4);
        header.Should().Be("%PDF", "output must be a valid PDF");

        // Verify FlateDecode / DeviceRGB image XObject is present
        string pdfText = Encoding.Latin1.GetString(ms.ToArray());
        pdfText.Should().Contain("/FlateDecode",  "RGBA-sourced PNG image must be re-encoded as FlateDecode");
        pdfText.Should().Contain("/DeviceRGB",    "image XObject must use DeviceRGB after alpha composite");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Drives the writer's internal DecodePngToRawRgb path by embedding the PNG in a
    /// DecodedImage, rendering it through OwnedPdfWriter, and extracting the raw RGB output.
    /// We re-derive raw pixels by decompressing the /FlateDecode stream from the output PDF.
    ///
    /// For unit-test purposes we call the public pipeline (writer round-trip) rather than the
    /// private method directly, which keeps tests decoupled from internal implementation names.
    /// </summary>
    private static byte[] RoundTripPngToRawRgb(byte[] pngBytes, int width, int height)
    {
        // Use the writer's full PNG→XObject path to confirm no exception, then verify
        // that the compressed output, when inflated, has exactly width*height*3 bytes.
        const string src = "fixture.png";
        var image = new DecodedImage(width, height, pngBytes, "image/png");
        var pageList = new PositionedPageList();
        var page = new PositionedPage { PageIndex = 0 };
        page.Elements.Add(new PositionedElement
        {
            Source   = new ReplacedBox { Src = src, NaturalWidth = width, NaturalHeight = height },
            Position = new Rect(0, 0, width, height),
            PageIndex = 0
        });
        pageList.Pages.Add(page);
        pageList.EmbeddedFonts = [];
        pageList.Images = new Dictionary<string, DecodedImage> { [src] = image };

        var writer = new OwnedPdfWriter();
        using var ms = new MemoryStream();
        writer.WriteAsync(pageList, new Muonroi.Pdf.Abstractions.PdfRenderOptions(), ms, CancellationToken.None)
              .GetAwaiter().GetResult();

        // The raw RGB bytes we want are the FlateDecode-compressed stream inside the PDF.
        // Extract by finding the stream start and decompressing.
        byte[] pdfBytes = ms.ToArray();
        return ExtractFirstFlateDecodeStream(pdfBytes);
    }

    /// <summary>
    /// Extracts and decompresses the image XObject FlateDecode stream from a PDF.
    /// Finds the object dictionary containing both /DeviceRGB and /FlateDecode (the image XObject),
    /// then decompresses its stream data.
    /// </summary>
    private static byte[] ExtractFirstFlateDecodeStream(byte[] pdfBytes)
    {
        string pdfLatin = Encoding.Latin1.GetString(pdfBytes);

        // Find the image XObject: look for /DeviceRGB followed (within a reasonable range) by
        // /FlateDecode. Both must appear in the same object dictionary before "stream".
        // Strategy: scan for /DeviceRGB, then check if /FlateDecode appears before the next "stream".
        int searchFrom = 0;
        while (true)
        {
            int deviceRgb = pdfLatin.IndexOf("/DeviceRGB", searchFrom, StringComparison.Ordinal);
            if (deviceRgb < 0)
                throw new InvalidDataException("No /DeviceRGB entry found in PDF — image XObject missing");

            // Find the next "stream" after /DeviceRGB
            int streamKeyword = pdfLatin.IndexOf("stream", deviceRgb, StringComparison.Ordinal);
            if (streamKeyword < 0)
                throw new InvalidDataException("No stream keyword after /DeviceRGB");

            // Check /FlateDecode appears between /DeviceRGB and "stream"
            int flateDecode = pdfLatin.IndexOf("/FlateDecode", deviceRgb, streamKeyword - deviceRgb, StringComparison.Ordinal);
            if (flateDecode >= 0)
            {
                // Found the image XObject stream
                int dataStart = streamKeyword + 6; // skip "stream"
                if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r') dataStart++;
                if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n') dataStart++;

                int endStream = pdfLatin.IndexOf("endstream", dataStart, StringComparison.Ordinal);
                if (endStream < 0)
                    throw new InvalidDataException("No endstream keyword found after image stream");

                int dataEnd = endStream;
                while (dataEnd > dataStart && (pdfBytes[dataEnd - 1] == '\n' || pdfBytes[dataEnd - 1] == '\r'))
                    dataEnd--;

                byte[] compressed = pdfBytes[dataStart..dataEnd];
                using var compressedMs = new MemoryStream(compressed);
                using var zlib = new System.IO.Compression.ZLibStream(compressedMs, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zlib.CopyTo(outMs);
                return outMs.ToArray();
            }

            searchFrom = deviceRgb + 1;
        }
    }

    /// <summary>
    /// Builds a minimal PNG header (magic + IHDR) suitable for decoder-level rejection tests.
    /// Does NOT include IDAT — only used to test IHDR-level failures in PureImageDecoder.
    /// </summary>
    private static byte[] BuildMinimalPngHeader(byte bitDepth, byte colorType)
    {
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,  // PNG magic
            0x00, 0x00, 0x00, 0x0D,                            // IHDR chunk length = 13
            0x49, 0x48, 0x44, 0x52,                            // "IHDR"
            0x00, 0x00, 0x00, 0x01,                            // width = 1
            0x00, 0x00, 0x00, 0x01,                            // height = 1
            bitDepth,                                           // bit_depth
            colorType,                                          // color_type
            0x00, 0x00, 0x00,                                  // compression=0, filter=0, interlace=0
            0x00, 0x00, 0x00, 0x00                             // CRC (zeroed — decoder reads type before CRC)
        };
    }
}
