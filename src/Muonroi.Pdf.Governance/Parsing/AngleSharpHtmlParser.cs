using System.Collections.Generic;

namespace Muonroi.Pdf.Governance.Parsing;

public sealed class AngleSharpHtmlParser : IHtmlParser
{
    public async ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default)
    {
        if ((long)html.Length * 2 > PdfConfigs.PdfLimits.MaxHtmlBytes)
            throw new PdfInputLimitException(
                "limit.max-html-bytes",
                "MaxHtmlBytes",
                (long)html.Length * 2,
                PdfConfigs.PdfLimits.MaxHtmlBytes);

        IBrowsingContext context = BrowsingContext.New(Configuration.Default.WithCss());
        IDocument document = await context.OpenAsync(req => req.Content(html), ct).ConfigureAwait(false);

        if (document.All.Length > PdfConfigs.PdfLimits.MaxElementCount)
            throw new PdfInputLimitException(
                "limit.max-element-count",
                "MaxElementCount",
                document.All.Length,
                PdfConfigs.PdfLimits.MaxElementCount);

        int maxDepth = ComputeMaxDepth(document);
        if (maxDepth > PdfConfigs.PdfLimits.MaxDomDepth)
            throw new PdfInputLimitException(
                "limit.max-dom-depth",
                "MaxDomDepth",
                maxDepth,
                PdfConfigs.PdfLimits.MaxDomDepth);

        return new AngleSharpParsedDocument(document, (long)html.Length * 2);
    }

    private static int ComputeMaxDepth(IDocument document)
    {
        int maxDepth = 0;
        var stack = new Stack<(INode node, int depth)>();
        if (document.DocumentElement != null)
            stack.Push((document.DocumentElement, 1));

        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            if (depth > maxDepth)
                maxDepth = depth;

            foreach (INode child in node.ChildNodes)
                stack.Push((child, depth + 1));
        }

        return maxDepth;
    }
}
