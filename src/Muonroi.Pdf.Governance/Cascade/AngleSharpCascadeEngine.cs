using Muonroi.Core.Abstractions.Guards;
using Muonroi.Pdf.Governance.Parsing;

namespace Muonroi.Pdf.Governance.Cascade;

public sealed class AngleSharpCascadeEngine : ICssCascadeEngine
{
    public AngleSharpCascadeEngine() { }

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
