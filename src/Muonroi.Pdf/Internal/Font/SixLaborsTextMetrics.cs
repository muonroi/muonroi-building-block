using SixLabors.Fonts;
using Muonroi.Pdf.Internal.Layout;
using SLFont = SixLabors.Fonts.Font;
using SLFontFamily = SixLabors.Fonts.FontFamily;
using SLFontStyle = SixLabors.Fonts.FontStyle;

namespace Muonroi.Pdf.Internal.Font;

internal sealed class SixLaborsTextMetrics : ITextMetrics
{
    private readonly FontCollection _collection;

    internal SixLaborsTextMetrics(FontCollection fontCollection)
    {
        _collection = fontCollection;
    }

    public float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic)
    {
        SLFontStyle style = bold && italic ? SLFontStyle.BoldItalic
            : bold ? SLFontStyle.Bold
            : italic ? SLFontStyle.Italic
            : SLFontStyle.Regular;

        if (!TryGetFamily(fontFamily, out SLFontFamily family))
            return fontSize * 0.6f;

        SLFont font = family.CreateFont(fontSize, style);
        TextOptions opts = new(font);
        return TextMeasurer.MeasureAdvance(c.ToString(), opts).Width;
    }

    public float GetLineHeight(string fontFamily, float fontSize)
    {
        if (!TryGetFamily(fontFamily, out SLFontFamily family))
            return fontSize * 1.2f;

        if (!family.TryGetMetrics(SLFontStyle.Regular, out FontMetrics? m) || m == null)
            return fontSize * 1.2f;

        return (float)m.HorizontalMetrics.LineHeight * fontSize / m.UnitsPerEm;
    }

    public float GetAscender(string fontFamily, float fontSize)
    {
        if (!TryGetFamily(fontFamily, out SLFontFamily family))
            return fontSize * 0.8f;

        if (!family.TryGetMetrics(SLFontStyle.Regular, out FontMetrics? m) || m == null)
            return fontSize * 0.8f;

        return (float)m.HorizontalMetrics.Ascender * fontSize / m.UnitsPerEm;
    }

    public float GetDescender(string fontFamily, float fontSize)
    {
        if (!TryGetFamily(fontFamily, out SLFontFamily family))
            return fontSize * 0.2f;

        if (!family.TryGetMetrics(SLFontStyle.Regular, out FontMetrics? m) || m == null)
            return fontSize * 0.2f;

        return Math.Abs((float)m.HorizontalMetrics.Descender * fontSize / m.UnitsPerEm);
    }

    private bool TryGetFamily(string fontFamily, out SLFontFamily family)
        => _collection.TryGet(fontFamily, out family);
}
