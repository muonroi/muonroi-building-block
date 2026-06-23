using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Image;

namespace Muonroi.Pdf.Tests.Image;

/// <summary>
/// Rejection tests for PureImageDecoder.DecodePng against unsupported PNG variants (FIDELITY-10, FIDELITY-11).
/// </summary>
public sealed class ImageRejectionTests
{
    // Minimal 33-byte PNG: 8-byte magic + 4-byte length + 4-byte "IHDR" + 4-byte width + 4-byte height +
    // 1-byte bit_depth + 1-byte color_type + 3-byte compression/filter/interlace + 4-byte CRC
    private static byte[] BuildMinimalPng(byte bitDepth, byte colorType)
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,  // PNG magic
            0x00, 0x00, 0x00, 0x0D,                            // IHDR chunk length = 13
            0x49, 0x48, 0x44, 0x52,                            // "IHDR"
            0x00, 0x00, 0x00, 0x01,                            // width = 1
            0x00, 0x00, 0x00, 0x01,                            // height = 1
            bitDepth,                                           // bit_depth (offset 24)
            colorType,                                          // color_type (offset 25)
            0x00, 0x00, 0x00,                                  // compression=0, filter=0, interlace=0
            0x00, 0x00, 0x00, 0x00                             // CRC (zeroed — decoder reads IHDR before CRC)
        ];
    }

    [Fact]
    public void DecodePng_Rgba_PassesThroughWithOriginalBytes()
    {
        // color_type=6 (RGBA) is now supported — PureImageDecoder passes through original bytes.
        byte[] pngBytes = BuildMinimalPng(bitDepth: 0x08, colorType: 0x06);
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(pngBytes, "image/png");

        result.Should().NotBeNull();
        result.Width.Should().Be(1);
        result.Height.Should().Be(1);
    }

    [Fact]
    public void DecodePng_Palette_PassesThroughWithOriginalBytes()
    {
        // color_type=3 (palette/indexed) is now supported — PureImageDecoder passes through original bytes.
        byte[] pngBytes = BuildMinimalPng(bitDepth: 0x08, colorType: 0x03);
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(pngBytes, "image/png");

        result.Should().NotBeNull();
        result.Width.Should().Be(1);
        result.Height.Should().Be(1);
    }

    [Fact]
    public void DecodePng_Grayscale8_PassesThroughWithOriginalBytes()
    {
        // color_type=0 (8-bit grayscale) is now supported — PureImageDecoder passes through original bytes.
        byte[] pngBytes = BuildMinimalPng(bitDepth: 0x08, colorType: 0x00);
        var decoder = new PureImageDecoder();

        DecodedImage result = decoder.Decode(pngBytes, "image/png");

        result.Should().NotBeNull();
        result.Width.Should().Be(1);
        result.Height.Should().Be(1);
    }

    [Fact]
    public void DecodePng_16BitGrayscale_ThrowsPdfFormatException_PNG_16BIT()
    {
        byte[] pngBytes = BuildMinimalPng(bitDepth: 0x10, colorType: 0x00); // color_type=0 (grayscale), bit_depth=16
        var decoder = new PureImageDecoder();

        Action act = () => decoder.Decode(pngBytes, "image/png");

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("PNG-16BIT");
    }

    [Fact]
    public void DecodePng_16BitRgb_ThrowsPdfFormatException_PNG_16BIT()
    {
        byte[] pngBytes = BuildMinimalPng(bitDepth: 0x10, colorType: 0x02); // color_type=2 (RGB), bit_depth=16
        var decoder = new PureImageDecoder();

        Action act = () => decoder.Decode(pngBytes, "image/png");

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("PNG-16BIT");
    }

    [Fact]
    public void DecodePng_ValidRgb8_DoesNotThrow()
    {
        byte[] pngBytes = BuildMinimalPng(bitDepth: 0x08, colorType: 0x02); // color_type=2 (RGB), bit_depth=8
        var decoder = new PureImageDecoder();

        // DecodePng validates IHDR and returns a DecodedImage — no exception for valid 8-bit RGB
        DecodedImage result = decoder.Decode(pngBytes, "image/png");

        result.Should().NotBeNull();
        result.Width.Should().Be(1);
        result.Height.Should().Be(1);
    }
}
