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

        // Map from CSS @font-face family name → the actual SixLabors FontFamily.
        // SixLabors indexes fonts under the internal TTF name-table family name, which
        // often differs from the CSS family name (e.g. CSS "serif" vs TTF "Muon ITst").
        // Without this map, SixLaborsTextMetrics falls back to the 0.6× heuristic for
        // all character-width measurements, producing over-wide word spacing.
        var cssFamilyMap = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

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

            FontFamily addedFamily = collection.Add(new MemoryStream(bytes.Value.ToArray()));
            fontBytesMap[decl.Family] = bytes.Value;

            // Register the added family under the CSS @font-face name so metrics lookups
            // by CSS name (e.g. "serif") resolve to the correct internal FontFamily.
            // Without this, TryGetFamily("serif") misses because the collection indexes
            // the font under its internal TTF name-table family (e.g. "Noto Sans"), causing
            // all char-width measurements to fall back to the 0.6× heuristic.
            if (!cssFamilyMap.ContainsKey(decl.Family))
                cssFamilyMap[decl.Family] = addedFamily;
        }

        return (new SixLaborsTextMetrics(collection, cssFamilyMap), fontBytesMap, collection);
    }
}
