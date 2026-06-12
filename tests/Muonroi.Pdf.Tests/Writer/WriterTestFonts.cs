using System.Collections.Generic;
using System.Linq;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Internal.Font;

namespace Muonroi.Pdf.Tests.Writer;

/// <summary>
/// Supplies a deterministic embedded font (the project's TestFont.ttf resource) for writer
/// tests. Using an embedded face avoids depending on OS-installed fonts, which are absent on
/// some build hosts and would make output non-deterministic.
/// </summary>
internal static class WriterTestFonts
{
    public const string Family = "WriterTestFont";

    /// <summary>Printable ASCII (0x20–0x7E) used as the default codepoint set for test subsets.</summary>
    public static IReadOnlySet<int> PrintableAscii =>
        new HashSet<int>(Enumerable.Range(0x20, 0x7F - 0x20));

    public static byte[] LoadTestFontBytesRaw()
    {
        using System.IO.Stream? stream = typeof(WriterTestFonts).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new System.InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new System.IO.MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Returns a properly subsetted <see cref="EmbeddedFontInfo"/> for all printable ASCII codepoints.
    /// The subset is run through <see cref="TrueTypeFontSubsetter"/> so that <c>CpToNewGid</c> is
    /// populated — required by <see cref="Muonroi.Pdf.Internal.Writer.OwnedPdfWriter"/> to emit correct
    /// 2-byte GID hex strings under Identity-H encoding.
    /// </summary>
    public static IReadOnlyList<EmbeddedFontInfo> Embedded(IReadOnlySet<int>? codepoints = null)
    {
        IReadOnlySet<int> cp = codepoints ?? PrintableAscii;
        byte[] rawBytes = LoadTestFontBytesRaw();
        var subsetter = new TrueTypeFontSubsetter();
        FontSubsetResult result = subsetter.Subset(rawBytes, cp);
        return new List<EmbeddedFontInfo>
        {
            new(Family, FontWeight.Normal, FontStyle.Normal,
                result.SubsetBytes, cp,
                result.OldToNewGid, result.SortedGids, result.CpToNewGid)
        };
    }
}
