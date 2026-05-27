using Muonroi.Pdf.Internal.Font;
using PdfSharpCore.Fonts;

using AbstractionsFontStyle = Muonroi.Pdf.Abstractions.FontStyle;

namespace Muonroi.Pdf.Internal.Writer;

/// <summary>
/// Bridges custom-resolved font bytes (<see cref="EmbeddedFontInfo"/>) into PdfSharpCore's
/// font subsystem so <c>XFont</c> can resolve faces produced by the Muonroi font pipeline.
/// Falls back to <see cref="PlatformFontResolver"/> (OS fonts) when no embedded face matches.
/// </summary>
internal sealed class PdfSharpFontResolverAdapter : PdfSharpCore.Fonts.IFontResolver
{
    private const int BoldWeightThreshold = 600;

    // Face key format: "{family}#{weight}#{style}" e.g. "Roboto#400#Normal".
    // Volatile swap target: a single resolver instance is installed once into
    // GlobalFontSettings.FontResolver (which PdfSharpCore forbids reassigning after first
    // use), and the backing map is swapped per render under the writer's lock.
    private volatile Dictionary<string, byte[]> _fontBytes = new(StringComparer.OrdinalIgnoreCase);

    public PdfSharpFontResolverAdapter()
    {
    }

    public PdfSharpFontResolverAdapter(IReadOnlyList<EmbeddedFontInfo> embeddedFonts)
    {
        SetEmbeddedFonts(embeddedFonts);
    }

    /// <summary>
    /// Swaps the backing embedded-font map. Enables one resolver instance to be installed once
    /// into <c>GlobalFontSettings.FontResolver</c> while still serving per-render font sets.
    /// </summary>
    public void SetEmbeddedFonts(IReadOnlyList<EmbeddedFontInfo> embeddedFonts)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (EmbeddedFontInfo font in embeddedFonts)
        {
            string key = BuildKey(font.Family, (int)font.Weight >= BoldWeightThreshold, IsItalic(font.Style));
            map[key] = font.SubsetBytes.ToArray();
        }
        _fontBytes = map;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        // 1. Exact match on (weight, style).
        string exactKey = BuildKey(familyName, bold, italic);
        if (_fontBytes.ContainsKey(exactKey))
        {
            return new FontResolverInfo(exactKey);
        }

        // 2. Weight-only match (ignore style).
        string boldKey = BuildKey(familyName, bold, italic: false);
        if (_fontBytes.ContainsKey(boldKey))
        {
            return new FontResolverInfo(boldKey);
        }

        string italicKey = BuildKey(familyName, bold: false, italic);
        if (_fontBytes.ContainsKey(italicKey))
        {
            return new FontResolverInfo(italicKey);
        }

        // 3. Any face for this family (prefix match on "{family}#").
        string familyPrefix = familyName + "#";
        foreach (string key in _fontBytes.Keys)
        {
            if (key.StartsWith(familyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new FontResolverInfo(key);
            }
        }

        // 4. OS font fallback.
        return PlatformFontResolver.ResolveTypeface(familyName, bold, italic);
    }

    public byte[]? GetFont(string faceName)
        => _fontBytes.TryGetValue(faceName, out byte[]? bytes) ? bytes : null;

    // PdfSharpCore 1.3.65 adds this property to IFontResolver (not in the plan's interface
    // snapshot). Used as the family when a typeface cannot otherwise be resolved.
    public string DefaultFontName => "Arial";

    private static string BuildKey(string family, bool bold, bool italic)
        => $"{family}#{(bold ? "Bold" : "Regular")}#{(italic ? "Italic" : "Normal")}";

    private static bool IsItalic(AbstractionsFontStyle style)
        => style is AbstractionsFontStyle.Italic or AbstractionsFontStyle.Oblique;
}
