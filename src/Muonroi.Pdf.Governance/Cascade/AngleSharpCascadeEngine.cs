namespace Muonroi.Pdf.Governance.Cascade;

/// <summary>
/// AngleSharp-backed implementation of <see cref="ICssCascadeEngine"/> that applies
/// an optional user stylesheet to a parsed HTML document and returns a styled document
/// ready for box-tree construction.
/// </summary>
public sealed class AngleSharpCascadeEngine : ICssCascadeEngine
{
    /// <summary>
    /// Initializes a new instance of <see cref="AngleSharpCascadeEngine"/>.
    /// </summary>
    public AngleSharpCascadeEngine() { }

    /// <summary>
    /// Applies <paramref name="userStyleSheet"/> (if non-empty) to the parsed document
    /// by injecting a <c>&lt;style&gt;</c> element into <c>&lt;head&gt;</c>, then wraps
    /// the result in an <see cref="AngleSharpStyledDocument"/> for downstream rendering.
    /// </summary>
    /// <param name="doc">
    /// The parsed HTML document. Must be an <see cref="AngleSharpParsedDocument"/>
    /// produced by <see cref="AngleSharpHtmlParser"/>; any other implementation throws.
    /// </param>
    /// <param name="userStyleSheet">
    /// An optional CSS string to inject as an author-origin stylesheet. Pass <c>null</c>
    /// or empty to skip injection.
    /// </param>
    /// <param name="ct">Cancellation token (unused; reserved for interface conformance).</param>
    /// <returns>
    /// A <see cref="IStyledDocument"/> wrapping the AngleSharp DOM with the cascade applied.
    /// </returns>
    public ValueTask<IStyledDocument> CascadeAsync(IParsedDocument doc, string? userStyleSheet, CancellationToken ct = default)
    {
        MGuard.Against(doc is not AngleSharpParsedDocument, "Expected AngleSharpParsedDocument produced by AngleSharpHtmlParser");
        AngleSharpParsedDocument parsedDoc = (AngleSharpParsedDocument)doc;

        if (!string.IsNullOrEmpty(userStyleSheet))
        {
            IElement style = parsedDoc.Document.CreateElement("style");
            style.TextContent = userStyleSheet;
            parsedDoc.Document.Head?.Append(style);
        }

        return ValueTask.FromResult<IStyledDocument>(
            new AngleSharpStyledDocument(parsedDoc.Document, parsedDoc.SourceHtmlBytes));
    }
}
