using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class InlineLayoutEngine
{
    // Separators that mark word boundaries in inline text content.
    private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r', '​' };

    public float Layout(IEnumerable<BoxNode> inlineChildren, LayoutContext context, List<PositionedElement> output, int pageIndex)
    {
        var metrics = context.TextMetrics;
        float availWidth = context.AvailableWidth;
        float startX = context.PageMarginLeftPt;
        float lineY = context.CurrentY;
        float totalHeight = 0f;

        // A word token: the text to render, its measured width, and the box it belongs to.
        var pendingWords = new List<(InlineBox Box, string Word, float Width)>();
        float lineX = 0f;

        void CommitLine()
        {
            if (pendingWords.Count == 0) return;

            float lineAscender = 0f;
            float lineHeight = 0f;
            foreach (var (box, _, _) in pendingWords)
            {
                float asc = metrics.GetAscender(box.FontFamily, box.FontSize);
                float lh = metrics.GetLineHeight(box.FontFamily, box.FontSize);
                if (asc > lineAscender) lineAscender = asc;
                if (lh > lineHeight) lineHeight = lh;
            }

            float wordOffsetX = 0f;
            foreach (var (box, word, wordWidth) in pendingWords)
            {
                float boxHeight = metrics.GetLineHeight(box.FontFamily, box.FontSize);
                float boxAscender = metrics.GetAscender(box.FontFamily, box.FontSize);

                float yOffset = box.VerticalAlign switch
                {
                    "top" => 0f,
                    "middle" => (lineHeight - boxHeight) / 2f,
                    "bottom" => lineHeight - boxHeight,
                    _ => lineAscender - boxAscender // "baseline" default
                };

                output.Add(new PositionedElement
                {
                    Source = box,
                    Position = new Rect(startX + wordOffsetX, lineY + yOffset, wordWidth, boxHeight),
                    PageIndex = pageIndex
                });

                float spaceWidth = metrics.GetCharWidth(' ', box.FontFamily, box.FontSize, box.Bold, box.Italic);
                wordOffsetX += wordWidth + spaceWidth;
            }

            lineY += lineHeight;
            totalHeight += lineHeight;
            pendingWords.Clear();
            lineX = 0f;
        }

        foreach (var node in inlineChildren)
        {
            var boxes = FlattenInline(node);
            foreach (var box in boxes)
            {
                string text = box.Text ?? string.Empty;
                string[] words = text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) continue;

                float spaceWidth = metrics.GetCharWidth(' ', box.FontFamily, box.FontSize, box.Bold, box.Italic);

                foreach (var word in words)
                {
                    float wordWidth = 0f;
                    foreach (char ch in word)
                        wordWidth += metrics.GetCharWidth(ch, box.FontFamily, box.FontSize, box.Bold, box.Italic);

                    // Would adding this word (plus an inter-word space if not first on line) exceed the line width?
                    float neededWidth = lineX == 0f ? wordWidth : lineX + spaceWidth + wordWidth;

                    if (neededWidth > availWidth && lineX > 0f)
                    {
                        // Commit current line, start fresh
                        CommitLine();
                        neededWidth = wordWidth;
                    }

                    // Force-break after a word that is wider than the full line (prevents infinite loop)
                    pendingWords.Add((box, word, wordWidth));
                    lineX = neededWidth;

                    if (lineX >= availWidth && pendingWords.Count > 0)
                        CommitLine();
                }
            }
        }

        CommitLine();
        return totalHeight;
    }

    private static IEnumerable<InlineBox> FlattenInline(BoxNode node)
    {
        if (node is InlineBox inline)
        {
            yield return inline;
            yield break;
        }
        foreach (var child in node.Children)
        foreach (var box in FlattenInline(child))
            yield return box;
    }
}
