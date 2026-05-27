using System.Collections.Generic;
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

    private static byte[] LoadTestFontBytes()
    {
        using System.IO.Stream? stream = typeof(WriterTestFonts).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new System.InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new System.IO.MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static IReadOnlyList<EmbeddedFontInfo> Embedded()
    {
        byte[] bytes = LoadTestFontBytes();
        return new List<EmbeddedFontInfo>
        {
            new(Family, FontWeight.Normal, FontStyle.Normal, bytes, new HashSet<int>())
        };
    }
}
