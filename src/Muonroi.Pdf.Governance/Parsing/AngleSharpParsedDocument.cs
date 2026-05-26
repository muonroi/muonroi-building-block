namespace Muonroi.Pdf.Governance.Parsing;

internal sealed class AngleSharpParsedDocument : IParsedDocument
{
    internal IDocument Document { get; }
    internal long SourceHtmlBytes { get; }

    internal AngleSharpParsedDocument(IDocument document, long sourceHtmlBytes)
    {
        Document = document;
        SourceHtmlBytes = sourceHtmlBytes;
    }
}
