using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;

namespace Muonroi.Pdf.Tests.Font;

/// <summary>
/// Rejection tests for TrueTypeFontSubsetter against OTF-CFF, WOFF, and WOFF2 inputs (FIDELITY-08, FIDELITY-09).
/// </summary>
public sealed class FontRejectionTests
{
    private static ReadOnlyMemory<byte> GetTestFontBytes()
    {
        using Stream? stream = typeof(FontRejectionTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Subset_OtfCff_ThrowsPdfFormatException_FONT_OTF_CFF()
    {
        // OTF-CFF magic: sfntVersion = 0x4F54544F ('OTTO')
        byte[] otfCffBytes = [0x4F, 0x54, 0x54, 0x4F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                               0x00, 0x00, 0x00, 0x00];

        var subsetter = new TrueTypeFontSubsetter();
        Action act = () => subsetter.Subset(otfCffBytes, new HashSet<int> { 65, 66 });

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("FONT-OTF-CFF");
    }

    [Fact]
    public void Subset_Woff_ThrowsPdfFormatException_FONT_WOFF()
    {
        // WOFF magic: sfntVersion = 0x774F4646 ('wOFF')
        byte[] woffBytes = [0x77, 0x4F, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00];

        var subsetter = new TrueTypeFontSubsetter();
        Action act = () => subsetter.Subset(woffBytes, new HashSet<int> { 65 });

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("FONT-WOFF");
    }

    [Fact]
    public void Subset_Woff2_ThrowsPdfFormatException_FONT_WOFF()
    {
        // WOFF2 magic: sfntVersion = 0x774F4632 ('wOF2')
        byte[] woff2Bytes = [0x77, 0x4F, 0x46, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                             0x00, 0x00, 0x00, 0x00];

        var subsetter = new TrueTypeFontSubsetter();
        Action act = () => subsetter.Subset(woff2Bytes, new HashSet<int> { 65 });

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("FONT-WOFF");
    }

    [Fact]
    public void Subset_ValidTtf_DoesNotThrow()
    {
        // Use the existing test.ttf fixture — should subset successfully
        ReadOnlyMemory<byte> ttfBytes = GetTestFontBytes();

        var subsetter = new TrueTypeFontSubsetter();
        FontSubsetResult result = subsetter.Subset(ttfBytes, new HashSet<int> { 65, 66, 67 });

        result.SubsetBytes.Length.Should().BeGreaterThan(0,
            because: "valid TTF must produce a non-empty subset");
    }
}
