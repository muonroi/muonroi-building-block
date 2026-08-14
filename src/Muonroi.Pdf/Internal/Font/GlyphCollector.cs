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

                // Collect all non-surrogate codepoints from the inline text.
                // We do NOT gate on fontCollection.TryGet(inlineBox.FontFamily) here:
                // FontCollection indexes fonts by their internal TTF name-table value, which
                // is unrelated to the CSS @font-face family name. Attempting a lookup by CSS
                // family name always misses and produces an empty codepoint set, which in turn
                // causes the subsetter to produce an empty cp→newGid map → blank PDF output.
                // The subsetter itself determines which codepoints it can map; codepoints with
                // no glyph in the font simply get no entry in the map (they are emitted as
                // .notdef / 0x0000), so over-collecting here is harmless.
                //
                // G22: apply the same text-transform that InlineLayoutEngine applies at emit time
                // so the font subsetter sees the post-transform codepoints. InlineBox.Text remains
                // the source-of-truth lowercase string; we transform only for collection purposes.
                string collectionText = inlineBox.TextTransform == "uppercase"
                    ? inlineBox.Text.ToUpperInvariant()
                    : inlineBox.Text;

                foreach (char ch in collectionText)
                {
                    if (!char.IsSurrogate(ch))
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
