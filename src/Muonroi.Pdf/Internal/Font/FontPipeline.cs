using System.Diagnostics;
using Muonroi.Pdf.Abstractions.Exceptions;
using SixLabors.Fonts;

namespace Muonroi.Pdf.Internal.Font;

internal sealed class FontPipeline
{
    internal async Task<(SixLaborsTextMetrics TextMetrics, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> FontBytesMap, FontCollection Collection)> ResolveAsync(
        IStyledDocument doc,
        IFontResolver resolver,
        PdfConfigs.PdfLimits limits,
        CancellationToken ct)
    {
        _ = limits;

        IReadOnlyList<FontFaceDeclaration> fontFaces = doc.FontFaces;

        if (fontFaces.Count > PdfConfigs.PdfLimits.Defaults.MaxFontFiles)
            throw new PdfInputLimitException(
                "FONT-MAX-FILES",
                "MaxFontFiles",
                fontFaces.Count,
                PdfConfigs.PdfLimits.Defaults.MaxFontFiles);

        var collection = new FontCollection();
        var fontBytesMap = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);

        foreach (FontFaceDeclaration decl in fontFaces)
        {
            ct.ThrowIfCancellationRequested();

            FontRequest request = new(decl.Family, decl.Weight, decl.Style);
            ReadOnlyMemory<byte>? bytes = await resolver.ResolveAsync(request, ct).ConfigureAwait(false);

            if (bytes == null)
            {
                Debug.WriteLine($"[FontPipeline] Font not resolved: {decl.Family}");
                continue;
            }

            collection.Add(new MemoryStream(bytes.Value.ToArray()));
            fontBytesMap[decl.Family] = bytes.Value;
        }

        return (new SixLaborsTextMetrics(collection), fontBytesMap, collection);
    }
}
