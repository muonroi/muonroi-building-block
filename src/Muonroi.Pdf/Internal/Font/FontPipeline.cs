namespace Muonroi.Pdf.Internal.Font;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfInputLimitException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy.")]
[SuppressMessage("Muonroi.CodeStandards", "MSTD0003",
    Justification = "Internal PDF pipeline helper is instantiated without DI; Debug.WriteLine is a DEBUG-only trace for unresolved fonts, compiled out in Release.")]
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

            // FONT-ALIAS-02 (Phase 12.1): when consumer @font-face matches a bundled canonical
            // (e.g. "Times New Roman" → Liberation Serif), also register the canonical's CSS
            // aliases (serif, Georgia, Times) pointing at the consumer's FontFamily. Without
            // this, elements that default to InlineBox.FontFamily="serif" hit the 0.6× width
            // heuristic, producing over-wide word spacing and incorrect column-width
            // allocation in tables. Parallel to LayoutEngine fix in commit 4bc0465 (11.6),
            // which addressed cpToNewGidMap; this addresses cssFamilyMap (metrics side).
            if (BundledFonts.TryGetFallback(decl.Family, decl.Weight, decl.Style, out _, out string canonical))
            {
                foreach (string alias in BundledFonts.GetAliasesForCanonical(canonical))
                {
                    if (!cssFamilyMap.ContainsKey(alias))
                        cssFamilyMap[alias] = addedFamily;
                }
            }
        }

        // Pre-load all three Liberation families as fallbacks for templates that reference
        // standard CSS family names (Times New Roman, Arial, Courier New, etc.) without @font-face.
        // Only registers variants whose canonical family is NOT already covered by @font-face above.
        // This prevents bundled bytes from shadowing caller-supplied fonts.
        var registeredCanonicalFamilies = new HashSet<string>(
            fontBytesMap.Keys.Select(k =>
                BundledFonts.TryGetFallback(k, FontWeight.Normal, Muonroi.Pdf.Abstractions.FontStyle.Normal, out _, out string canon)
                    ? canon : k),
            StringComparer.OrdinalIgnoreCase);

        foreach ((string family, FontWeight weight, Muonroi.Pdf.Abstractions.FontStyle style, ReadOnlyMemory<byte> bytes) in BundledFonts.AllVariants())
        {
            if (registeredCanonicalFamilies.Contains(family)) continue;

            FontFamily addedFamily = collection.Add(new MemoryStream(bytes.ToArray()));
            fontBytesMap.TryAdd(family, bytes);

            // Register canonical name.
            if (!cssFamilyMap.ContainsKey(family))
                cssFamilyMap[family] = addedFamily;

            // Register all alias names so InlineBox.FontFamily lookups succeed for e.g. "Times New Roman".
            string[] aliases = family switch
            {
                BundledFonts.FamilySerif => ["Times New Roman", "Times", "Georgia", "serif"],
                BundledFonts.FamilySans => ["Arial", "Helvetica", "Verdana", "sans-serif"],
                BundledFonts.FamilyMono => ["Courier New", "Courier", "monospace"],
                _ => []
            };

            foreach (string alias in aliases)
            {
                if (!cssFamilyMap.ContainsKey(alias))
                    cssFamilyMap[alias] = addedFamily;
            }
        }

        return (new SixLaborsTextMetrics(collection, cssFamilyMap), fontBytesMap, collection);
    }
}
