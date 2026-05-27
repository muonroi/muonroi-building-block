using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class InlineLayoutEngine
{
    // Separators that mark word boundaries in inline text content.
    private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r', '​' };

    public float Layout(IEnumerable<BoxNode> inlineChildren, LayoutContext context, List<PositionedElement> output, int pageIndex, PositionedPage? page = null)
    {
        var metrics = context.TextMetrics;
        float availWidth = context.AvailableWidth;
        float startX = context.PageMarginLeftPt;
        float lineY = context.CurrentY;
        float totalHeight = 0f;

        // A word token: the text to render, its measured width, and the box it belongs to.
        var pendingWords = new List<(InlineBox Box, string Word, float Width)>();
        float lineX = 0f;

        void CommitLine(bool isLastLine)
        {
            if (pendingWords.Count == 0) return;

            float lineAscender = 0f;
            float lineHeight = 0f;
            InlineBox? dominantBox = null;
            float dominantFontSize = 0f;
            foreach (var (box, _, _) in pendingWords)
            {
                float asc = metrics.GetAscender(box.FontFamily, box.FontSize);
                float lh = metrics.GetLineHeight(box.FontFamily, box.FontSize);
                if (asc > lineAscender) lineAscender = asc;
                if (lh > lineHeight) lineHeight = lh;
                if (box.FontSize > dominantFontSize)
                {
                    dominantFontSize = box.FontSize;
                    dominantBox = box;
                }
            }

            // Scale line height by dominant box's LineHeightFactor
            if (dominantBox != null && dominantBox.LineHeightFactor != 1.0f)
                lineHeight *= dominantBox.LineHeightFactor;

            // Compute total line width for alignment
            float totalLineWidth = 0f;
            int wordCount = pendingWords.Count;
            for (int wi = 0; wi < wordCount; wi++)
            {
                var (box, _, wordWidth) = pendingWords[wi];
                totalLineWidth += wordWidth;
                if (wi < wordCount - 1)
                {
                    float sw = metrics.GetCharWidth(' ', box.FontFamily, box.FontSize, box.Bold, box.Italic);
                    totalLineWidth += sw;
                }
            }

            // Compute x-offset based on text-align
            string? textAlign = context.TextAlign;
            float wordOffsetX = textAlign switch
            {
                "right" => availWidth - totalLineWidth,
                "center" => (availWidth - totalLineWidth) / 2f,
                "justify" when !isLastLine && wordCount > 1 => 0f, // gap distributed below
                _ => 0f // left (default)
            };

            // For justify: compute gap bonus per inter-word space
            float gapBonus = 0f;
            if (textAlign == "justify" && !isLastLine && wordCount > 1)
            {
                // Total space width between words
                float totalSpaceWidth = 0f;
                for (int wi = 0; wi < wordCount - 1; wi++)
                {
                    var (box, _, _) = pendingWords[wi];
                    totalSpaceWidth += metrics.GetCharWidth(' ', box.FontFamily, box.FontSize, box.Bold, box.Italic);
                }
                float extraSpace = availWidth - totalLineWidth;
                gapBonus = extraSpace / (wordCount - 1);
            }

            float currentX = wordOffsetX;
            for (int wi = 0; wi < wordCount; wi++)
            {
                var (box, word, wordWidth) = pendingWords[wi];
                float boxHeight = metrics.GetLineHeight(box.FontFamily, box.FontSize);
                float boxAscender = metrics.GetAscender(box.FontFamily, box.FontSize);

                float yOffset = box.VerticalAlign switch
                {
                    "top" => 0f,
                    "middle" => (lineHeight - boxHeight) / 2f,
                    "bottom" => lineHeight - boxHeight,
                    _ => lineAscender - boxAscender // "baseline" default
                };

                float wordX = startX + currentX;
                float wordY = lineY + yOffset;

                output.Add(new PositionedElement
                {
                    Source = box,
                    RenderedText = word,
                    Position = new Rect(wordX, wordY, wordWidth, boxHeight),
                    PageIndex = pageIndex
                });

                // Collect link annotation if this word has a link href
                if (box.LinkHref != null && page != null)
                {
                    page.LinkAnnotations.Add(new LinkAnnotation(
                        box.LinkHref, wordX, wordY, wordWidth, boxHeight, pageIndex));
                }

                float spaceWidth = metrics.GetCharWidth(' ', box.FontFamily, box.FontSize, box.Bold, box.Italic);
                currentX += wordWidth + spaceWidth + gapBonus;
            }

            lineY += lineHeight;
            totalHeight += lineHeight;
            pendingWords.Clear();
            lineX = 0f;
        }

        foreach (var node in inlineChildren)
        {
            // Check for LineBreakBox in the stream — force commit current line
            if (node is LineBreakBox)
            {
                CommitLine(isLastLine: false);
                continue;
            }

            var boxes = FlattenInline(node);
            foreach (var box in boxes)
            {
                // LineBreakBox may also appear inside flattened inline stream
                if (box is null) continue;

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
                        CommitLine(isLastLine: false);
                        neededWidth = wordWidth;
                    }

                    // Force-break after a word that is wider than the full line (prevents infinite loop)
                    pendingWords.Add((box, word, wordWidth));
                    lineX = neededWidth;

                    if (lineX >= availWidth && pendingWords.Count > 0)
                        CommitLine(isLastLine: false);
                }
            }
        }

        CommitLine(isLastLine: true);
        return totalHeight;
    }

    private static IEnumerable<InlineBox> FlattenInline(BoxNode node)
    {
        if (node is LineBreakBox)
            yield break; // handled at the outer loop level

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
