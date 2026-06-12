using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Image;
using Muonroi.Pdf.Tests.Helpers;
using NSubstitute;

namespace Muonroi.Pdf.Tests.Image;

public sealed class ImagePipelineTests
{
    private static readonly PdfConfigs.PdfLimits _limits = new();

    // ── PureImageDecoder tests ────────────────────────────────────────────────

    [Fact]
    public void Png_ValidIhdr_ReturnsCorrectDimensions()
    {
        byte[] bytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,  // PNG magic
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,  // length=13, "IHDR"
            0x00, 0x00, 0x00, 0x64,                            // width = 100
            0x00, 0x00, 0x00, 0xC8,                            // height = 200
            0x08,                                               // bit_depth = 8
            0x02                                                // color_type = 2 (RGB)
        ];

        var decoder = new PureImageDecoder();
        DecodedImage result = decoder.Decode(bytes, "image/png");

        result.Width.Should().Be(100);
        result.Height.Should().Be(200);
    }

    [Fact]
    public void Jpeg_ValidSof0_ReturnsCorrectDimensions()
    {
        // FF D8 = SOI; FF C0 = SOF0; pos=2 → height at pos+5=7, width at pos+7=9
        byte[] bytes =
        [
            0xFF, 0xD8,             // SOI
            0xFF, 0xC0,             // SOF0 marker
            0x00, 0x11,             // segment length (irrelevant for width/height extraction)
            0x08,                   // precision
            0x00, 0xF0,             // height = 240 (0x00F0)
            0x01, 0x40              // width = 320 (0x0140)
        ];

        var decoder = new PureImageDecoder();
        DecodedImage result = decoder.Decode(bytes, "image/jpeg");

        result.Width.Should().Be(320);
        result.Height.Should().Be(240);
    }

    [Fact]
    public void Jpeg_ProgressiveSof2_FindsMarker()
    {
        byte[] bytes =
        [
            0xFF, 0xD8,             // SOI
            0xFF, 0xC2,             // SOF2 (progressive)
            0x00, 0x11,             // segment length
            0x08,                   // precision
            0x01, 0xE0,             // height = 480 (0x01E0)
            0x02, 0x80              // width = 640 (0x0280)
        ];

        var decoder = new PureImageDecoder();
        DecodedImage result = decoder.Decode(bytes, "image/jpeg");

        result.Width.Should().Be(640);
        result.Height.Should().Be(480);
    }

    [Fact]
    public void Png_InvalidMagic_ThrowsPdfException()
    {
        byte[] bytes = new byte[24];
        bytes[0] = 0x00;

        var decoder = new PureImageDecoder();
        Action act = () => decoder.Decode(bytes, "image/png");

        act.Should().Throw<PdfFormatException>();
    }

    [Fact]
    public void Png_TooShort_ThrowsPdfException()
    {
        // Valid PNG magic but only 10 bytes total (< 24 required for IHDR)
        byte[] bytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,  // PNG magic (8 bytes)
            0x00, 0x00                                           // truncated
        ];

        var decoder = new PureImageDecoder();
        Action act = () => decoder.Decode(bytes, "image/png");

        act.Should().Throw<PdfFormatException>();
    }

    // ── DataUriDecoder tests ──────────────────────────────────────────────────

    [Fact]
    public void DataUri_PngBase64_DecodesBytes()
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x01, 0x02, 0x03, 0x04];
        string base64 = Convert.ToBase64String(pngBytes);
        string dataUri = $"data:image/png;base64,{base64}";

        (ReadOnlyMemory<byte> bytes, string contentType) = DataUriDecoder.Decode(dataUri);

        bytes.ToArray().Should().Equal(pngBytes);
        contentType.Should().Be("image/png");
    }

    [Fact]
    public void DataUri_WithWhitespace_StripAndDecode()
    {
        byte[] pngBytes = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        string base64 = Convert.ToBase64String(pngBytes);
        string base64WithNewlines = string.Join("\n",
            Enumerable.Range(0, (base64.Length + 75) / 76)
                .Select(i => base64.Substring(i * 76, Math.Min(76, base64.Length - i * 76))));

        string dataUri = $"data:image/png;base64,{base64WithNewlines}";

        (ReadOnlyMemory<byte> decoded, _) = DataUriDecoder.Decode(dataUri);

        decoded.ToArray().Should().Equal(pngBytes);
    }

    [Fact]
    public void DataUri_MissingBase64Flag_ImageType_Throws()
    {
        Action act = () => DataUriDecoder.Decode("data:image/png,not-base64-data");

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("IMG-FORMAT");
    }

    // ── ImagePipeline tests ───────────────────────────────────────────────────

    [Fact]
    public async Task ExternalSrc_RoutedThroughResolver_NeverDirectNetwork()
    {
        string imgSrc = "http://example.com/img.png";
        byte[] pngBytes = BuildMinimalPng(1, 1);
        var resolver = Substitute.For<IResourceResolver>();
        resolver.ResolveAsync(Arg.Any<Uri>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ResourceResult(new ReadOnlyMemory<byte>(pngBytes), "image/png"));

        var imgNode = new FakeStyledNode("img",
            attributes: new Dictionary<string, string> { ["src"] = imgSrc });
        var root = new FakeStyledNode("div");
        root.ChildList.Add(imgNode);
        var doc = new FakeStyledDocument(root);

        var decoder = new FakeImageDecoder(_ => new DecodedImage(1, 1, pngBytes, "image/png"));
        var pipeline = new ImagePipeline();

        await pipeline.ResolveAsync(doc, resolver, decoder, _limits, CancellationToken.None);

        await resolver.Received(1).ResolveAsync(
            Arg.Is<Uri>(u => u == new Uri(imgSrc)),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullResolverResult_ImageSkipped_EmptyDictionary()
    {
        var resolver = Substitute.For<IResourceResolver>();
        resolver.ResolveAsync(Arg.Any<Uri>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((ResourceResult?)null);

        var imgNode = new FakeStyledNode("img",
            attributes: new Dictionary<string, string> { ["src"] = "http://example.com/img.png" });
        var root = new FakeStyledNode("div");
        root.ChildList.Add(imgNode);
        var doc = new FakeStyledDocument(root);

        var decoder = new FakeImageDecoder(_ => new DecodedImage(1, 1, ReadOnlyMemory<byte>.Empty, "image/png"));
        var pipeline = new ImagePipeline();

        IReadOnlyDictionary<string, DecodedImage> result =
            await pipeline.ResolveAsync(doc, resolver, decoder, _limits, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MaxImagePixels_Exceeded_ThrowsLimitException()
    {
        // 5001 * 5000 = 25,005,000 > 25,000,000
        var decoder = new FakeImageDecoder(_ => new DecodedImage(5001, 5000, ReadOnlyMemory<byte>.Empty, "image/png"));

        var root = new FakeStyledNode("div");
        root.ChildList.Add(BuildImgNodeWithDataUri());
        var doc = new FakeStyledDocument(root);

        var resolver = Substitute.For<IResourceResolver>();
        var pipeline = new ImagePipeline();

        Func<Task> act = () =>
            pipeline.ResolveAsync(doc, resolver, decoder, _limits, CancellationToken.None);

        (await act.Should().ThrowAsync<PdfInputLimitException>())
            .Which.RuleId.Should().Be("IMG-MAX-PIXELS");
    }

    [Fact]
    public async Task MaxImagePixels_AtBoundary_NoException()
    {
        // 5000 * 5000 = exactly 25,000,000 — NOT over the limit (check is >)
        var decoder = new FakeImageDecoder(_ => new DecodedImage(5000, 5000, ReadOnlyMemory<byte>.Empty, "image/png"));

        var root = new FakeStyledNode("div");
        root.ChildList.Add(BuildImgNodeWithDataUri());
        var doc = new FakeStyledDocument(root);

        var resolver = Substitute.For<IResourceResolver>();
        var pipeline = new ImagePipeline();

        Func<Task> act = () =>
            pipeline.ResolveAsync(doc, resolver, decoder, _limits, CancellationToken.None);

        await act.Should().NotThrowAsync(because: "25,000,000 is exactly at the limit, not over");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FakeStyledNode BuildImgNodeWithDataUri()
    {
        byte[] smallBytes = [0x00, 0x01, 0x02];
        string dataUri = $"data:image/png;base64,{Convert.ToBase64String(smallBytes)}";
        return new FakeStyledNode("img",
            attributes: new Dictionary<string, string> { ["src"] = dataUri });
    }

    private static byte[] BuildMinimalPng(int width, int height)
    {
        byte[] bytes = new byte[26];
        bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47;
        bytes[4] = 0x0D; bytes[5] = 0x0A; bytes[6] = 0x1A; bytes[7] = 0x0A;
        bytes[8] = 0x00; bytes[9] = 0x00; bytes[10] = 0x00; bytes[11] = 0x0D;
        bytes[12] = 0x49; bytes[13] = 0x48; bytes[14] = 0x44; bytes[15] = 0x52;
        bytes[16] = (byte)(width >> 24); bytes[17] = (byte)(width >> 16);
        bytes[18] = (byte)(width >> 8); bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24); bytes[21] = (byte)(height >> 16);
        bytes[22] = (byte)(height >> 8); bytes[23] = (byte)height;
        bytes[24] = 0x08; // bit_depth = 8
        bytes[25] = 0x02; // color_type = 2 (RGB)
        return bytes;
    }

    private sealed class FakeImageDecoder : IImageDecoder
    {
        private readonly Func<(ReadOnlyMemory<byte> data, string contentType), DecodedImage> _decode;

        internal FakeImageDecoder(Func<(ReadOnlyMemory<byte>, string), DecodedImage> decode)
            => _decode = decode;

        public DecodedImage Decode(ReadOnlySpan<byte> data, string contentType)
            => _decode((data.ToArray(), contentType));
    }
}
