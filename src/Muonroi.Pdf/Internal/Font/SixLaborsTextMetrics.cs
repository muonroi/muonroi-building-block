using SixLabors.Fonts;
using Muonroi.Pdf.Internal.Layout;
using SLFont = SixLabors.Fonts.Font;
using SLFontFamily = SixLabors.Fonts.FontFamily;
using SLFontStyle = SixLabors.Fonts.FontStyle;

namespace Muonroi.Pdf.Internal.Font;

/// <summary>
/// SixLabors-backed text metrics. Instances are created per render (see
/// <see cref="FontPipeline"/>), so the memoization caches below live for a single render only —
/// there is no cross-render shared mutable state and nothing to clear/leak.
///
/// ALLOC-01: <see cref="GetCharWidth"/> is invoked once per character of every text run. Without
/// caching, each call allocated a <see cref="SLFont"/> (CreateFont), a <see cref="TextOptions"/>,
/// a one-char <see cref="string"/>, plus SixLabors' internal measurement buffers — dominating
/// total allocations on text-heavy documents. The font / char-width / vertical-metrics caches
/// collapse the thousands of repeated (family, size, style, char) lookups in a render down to one
/// measurement each, eliminating the bulk of the churn while returning identical values.
/// </summary>
internal sealed class SixLaborsTextMetrics : ITextMetrics
{
    private readonly FontCollection _collection;

    // CSS family-name → actual SixLabors FontFamily. Populated by FontPipeline when the CSS
    // @font-face family name differs from the internal TTF name-table family name.
    private readonly IReadOnlyDictionary<string, SLFontFamily> _cssFamilyMap;

    // Per-render memoization. Keyed by the inputs that fully determine each result.
    private readonly Dictionary<(string Family, float Size, SLFontStyle Style), SLFont?> _fontCache = new();
    private readonly Dictionary<(string Family, float Size, SLFontStyle Style, char Ch), float> _charWidthCache = new();
    private readonly Dictionary<(string Family, float Size), VerticalMetrics> _verticalCache = new();

    private readonly record struct VerticalMetrics(float LineHeight, float Ascender, float Descender);

    internal SixLaborsTextMetrics(FontCollection fontCollection)
        : this(fontCollection, new Dictionary<string, SLFontFamily>(StringComparer.OrdinalIgnoreCase)) { }

    internal SixLaborsTextMetrics(
        FontCollection fontCollection,
        IReadOnlyDictionary<string, SLFontFamily> cssFamilyMap)
    {
        _collection = fontCollection;
        _cssFamilyMap = cssFamilyMap;
    }

    public float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic)
    {
        SLFontStyle style = bold && italic ? SLFontStyle.BoldItalic
            : bold ? SLFontStyle.Bold
            : italic ? SLFontStyle.Italic
            : SLFontStyle.Regular;

        (string, float, SLFontStyle, char) key = (fontFamily, fontSize, style, c);
        if (_charWidthCache.TryGetValue(key, out float cached))
            return cached;

        SLFont? font = GetFont(fontFamily, fontSize, style);
        float width = font is null
            ? fontSize * 0.6f
            : TextMeasurer.MeasureAdvance(c.ToString(), new TextOptions(font)).Width;

        _charWidthCache[key] = width;
        return width;
    }

    public float GetLineHeight(string fontFamily, float fontSize)
        => GetVerticalMetrics(fontFamily, fontSize).LineHeight;

    public float GetAscender(string fontFamily, float fontSize)
        => GetVerticalMetrics(fontFamily, fontSize).Ascender;

    public float GetDescender(string fontFamily, float fontSize)
        => GetVerticalMetrics(fontFamily, fontSize).Descender;

    private VerticalMetrics GetVerticalMetrics(string fontFamily, float fontSize)
    {
        (string, float) key = (fontFamily, fontSize);
        if (_verticalCache.TryGetValue(key, out VerticalMetrics cached))
            return cached;

        VerticalMetrics result;
        if (!TryGetFamily(fontFamily, out SLFontFamily family) ||
            !family.TryGetMetrics(SLFontStyle.Regular, out FontMetrics? m) || m == null)
        {
            // Fallbacks preserve the original per-accessor heuristics: 1.2/0.8/0.2 × fontSize.
            result = new VerticalMetrics(fontSize * 1.2f, fontSize * 0.8f, fontSize * 0.2f);
        }
        else
        {
            float scale = fontSize / m.UnitsPerEm;
            result = new VerticalMetrics(
                (float)m.HorizontalMetrics.LineHeight * scale,
                (float)m.HorizontalMetrics.Ascender * scale,
                Math.Abs((float)m.HorizontalMetrics.Descender * scale));
        }

        _verticalCache[key] = result;
        return result;
    }

    private SLFont? GetFont(string fontFamily, float fontSize, SLFontStyle style)
    {
        (string, float, SLFontStyle) key = (fontFamily, fontSize, style);
        if (_fontCache.TryGetValue(key, out SLFont? cached))
            return cached;

        SLFont? font = TryGetFamily(fontFamily, out SLFontFamily family)
            ? family.CreateFont(fontSize, style)
            : null;

        _fontCache[key] = font;
        return font;
    }

    private bool TryGetFamily(string fontFamily, out SLFontFamily family)
    {
        // First: try the CSS-family-name map built from @font-face declarations.
        // This resolves the mismatch where the CSS name is "serif" but the font's internal
        // TTF name-table says "Muon ITst" (or "Noto Sans" etc.) — the collection only
        // indexes under the internal name, not the CSS family name.
        if (_cssFamilyMap.TryGetValue(fontFamily, out family))
            return true;

        // Fall back to direct collection lookup (works when CSS name == internal TTF name).
        return _collection.TryGet(fontFamily, out family);
    }
}
