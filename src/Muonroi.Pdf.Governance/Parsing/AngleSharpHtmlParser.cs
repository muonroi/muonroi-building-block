using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Pdf.Governance.Parsing;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfInputLimitException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy in netstandard2.0 Pdf.Abstractions.")]
public sealed class AngleSharpHtmlParser : IHtmlParser
{
    public async ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default)
    {
        if ((long)html.Length * 2 > PdfConfigs.PdfLimits.Defaults.MaxHtmlBytes)
            throw new PdfInputLimitException(
                "limit.max-html-bytes",
                "MaxHtmlBytes",
                (long)html.Length * 2,
                PdfConfigs.PdfLimits.Defaults.MaxHtmlBytes);

        IBrowsingContext context = BrowsingContext.New(Configuration.Default.WithCss());
        IDocument document = await context.OpenAsync(req => req.Content(html), ct).ConfigureAwait(false);

        if (document.All.Length > PdfConfigs.PdfLimits.Defaults.MaxElementCount)
            throw new PdfInputLimitException(
                "limit.max-element-count",
                "MaxElementCount",
                document.All.Length,
                PdfConfigs.PdfLimits.Defaults.MaxElementCount);

        int maxDepth = ComputeMaxDepth(document);
        if (maxDepth > PdfConfigs.PdfLimits.Defaults.MaxDomDepth)
            throw new PdfInputLimitException(
                "limit.max-dom-depth",
                "MaxDomDepth",
                maxDepth,
                PdfConfigs.PdfLimits.Defaults.MaxDomDepth);

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
