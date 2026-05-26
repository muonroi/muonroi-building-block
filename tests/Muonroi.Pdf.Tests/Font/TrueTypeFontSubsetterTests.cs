using System.Buffers.Binary;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;

namespace Muonroi.Pdf.Tests.Font;

public sealed class TrueTypeFontSubsetterTests
{
    private static ReadOnlyMemory<byte> GetTestFontBytes()
    {
        using Stream? stream = typeof(TrueTypeFontSubsetterTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void CffOtf_PassthroughUnchanged()
    {
        // CFF-OTF sfntVersion = 0x4F54544F ('OTTO')
        byte[] cffBytes = [0x4F, 0x54, 0x54, 0x4F, 0x00, 0x05, 0x00, 0x20, 0x00, 0x10, 0x00, 0x10];

        var subsetter = new TrueTypeFontSubsetter();
        ReadOnlyMemory<byte> result = subsetter.Subset(cffBytes, new HashSet<int> { 65, 66 });

        result.Length.Should().Be(cffBytes.Length);
        result.ToArray().Should().Equal(cffBytes);
    }

    [Fact]
    public void UnrecognizedFormat_ThrowsPdfException()
    {
        byte[] unknownBytes = [0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

        var subsetter = new TrueTypeFontSubsetter();
        Action act = () => subsetter.Subset(unknownBytes, new HashSet<int> { 65 });

        act.Should().Throw<PdfFormatException>()
            .Which.RuleId.Should().Be("FONT-FORMAT");
    }

    [Fact]
    public void TtfSubset_SmallerThanOriginal()
    {
        ReadOnlyMemory<byte> original = GetTestFontBytes();
        var asciiCodepoints = Enumerable.Range(65, 26).Concat(Enumerable.Range(97, 26)).ToHashSet();

        var subsetter = new TrueTypeFontSubsetter();
        ReadOnlyMemory<byte> subset = subsetter.Subset(original, asciiCodepoints);

        subset.Length.Should().BeLessThan(original.Length,
            because: "subsetting to ASCII only should reduce font size significantly");

        // TTF magic: 0x00010000
        subset.Span[0].Should().Be(0x00);
        subset.Span[1].Should().Be(0x01);
        subset.Span[2].Should().Be(0x00);
        subset.Span[3].Should().Be(0x00);
    }

    [Fact]
    public void TtfSubset_ValidTableDirectory()
    {
        ReadOnlyMemory<byte> original = GetTestFontBytes();
        var codepoints = new HashSet<int> { 65, 66, 67, 68, 69 };

        var subsetter = new TrueTypeFontSubsetter();
        ReadOnlyMemory<byte> subset = subsetter.Subset(original, codepoints);

        uint sfntVersion = BinaryPrimitives.ReadUInt32BigEndian(subset.Span);
        sfntVersion.Should().Be(0x00010000u, because: "output must have TTF sfntVersion");

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(subset.Span.Slice(4, 2));
        numTables.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void MaxpNumGlyphs_UpdatedInSubset()
    {
        ReadOnlyMemory<byte> original = GetTestFontBytes();
        var codepoints = new HashSet<int> { (int)'A', (int)'B', (int)'C' };

        var subsetter = new TrueTypeFontSubsetter();
        ReadOnlyMemory<byte> subset = subsetter.Subset(original, codepoints);

        // Find maxp table in output
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(subset.Span.Slice(4, 2));
        int maxpOffset = -1;
        for (int i = 0; i < numTables; i++)
        {
            int recOff = 12 + i * 16;
            string tag = System.Text.Encoding.ASCII.GetString(subset.Span.Slice(recOff, 4));
            if (tag == "maxp")
            {
                maxpOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(subset.Span.Slice(recOff + 8, 4));
                break;
            }
        }

        maxpOffset.Should().BeGreaterThan(0, because: "maxp table must exist in subset");
        ushort numGlyphs = BinaryPrimitives.ReadUInt16BigEndian(subset.Span.Slice(maxpOffset + 4, 2));
        numGlyphs.Should().BeGreaterThan(0).And.BeLessThan(100,
            because: "subset has .notdef + ~3 glyphs, far fewer than Noto Sans ~2400+");
    }
}
