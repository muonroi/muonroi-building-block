using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Pdf.Governance.Parsing;

/// <summary>
/// AngleSharp-backed implementation of <see cref="IHtmlParser"/> that parses an HTML string
/// into a DOM and enforces structural limits (HTML size, element count, DOM depth) before
/// returning an <see cref="AngleSharpParsedDocument"/>.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfInputLimitException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy in netstandard2.0 Pdf.Abstractions.")]
public sealed class AngleSharpHtmlParser : IHtmlParser
{
    /// <summary>
    /// Parses <paramref name="html"/> into an AngleSharp DOM, enforcing the
    /// <see cref="PdfConfigs.PdfLimits.Defaults"/> limits for maximum HTML byte size,
    /// element count, and DOM nesting depth.
    /// </summary>
    /// <param name="html">The raw HTML string to parse.</param>
    /// <param name="ct">Cancellation token forwarded to the AngleSharp browsing-context open call.</param>
    /// <returns>
    /// An <see cref="IParsedDocument"/> (concretely <see cref="AngleSharpParsedDocument"/>)
    /// wrapping the parsed DOM and the source byte size.
    /// </returns>
    /// <exception cref="PdfInputLimitException">
    /// Thrown when the HTML byte size, element count, or DOM depth exceeds the configured limit.
    /// </exception>
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
