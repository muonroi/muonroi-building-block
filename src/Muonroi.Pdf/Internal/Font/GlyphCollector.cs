using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using SLFont = SixLabors.Fonts.Font;
using SLFontFamily = SixLabors.Fonts.FontFamily;
using SLFontStyle = SixLabors.Fonts.FontStyle;

namespace Muonroi.Pdf.Internal.Font;

internal sealed class GlyphCollector
{
    internal IReadOnlyDictionary<string, IReadOnlySet<int>> Collect(
        PositionedPageList pageList,
        FontCollection fontCollection)
    {
        Dictionary<string, HashSet<int>> result = new(StringComparer.Ordinal);

        foreach (PositionedPage page in pageList.Pages)
        {
            foreach (PositionedElement element in page.Elements)
            {
                if (element.Source is not InlineBox inlineBox)
                    continue;

                if (string.IsNullOrEmpty(inlineBox.Text) || string.IsNullOrEmpty(inlineBox.FontFamily))
                    continue;

                if (!result.ContainsKey(inlineBox.FontFamily))
                    result[inlineBox.FontFamily] = new HashSet<int>();

                SLFontStyle sfStyle = inlineBox.Bold && inlineBox.Italic ? SLFontStyle.BoldItalic
                    : inlineBox.Bold ? SLFontStyle.Bold
                    : inlineBox.Italic ? SLFontStyle.Italic
                    : SLFontStyle.Regular;

                if (!fontCollection.TryGet(inlineBox.FontFamily, out SLFontFamily ff))
                    continue;

                float fontSize = inlineBox.FontSize > 0 ? inlineBox.FontSize : 12f;
                SLFont font = ff.CreateFont(fontSize, sfStyle);

                foreach (char ch in inlineBox.Text)
                {
                    if (char.IsSurrogate(ch))
                        continue;

                    if (font.TryGetGlyphs(new CodePoint(ch), out _))
                        result[inlineBox.FontFamily].Add((int)ch);
                }
            }
        }

        return result.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<int>)kvp.Value,
            StringComparer.Ordinal);
    }
}
