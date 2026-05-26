using System.Text;

namespace Muonroi.Pdf.Governance.Cascade;

internal sealed class AngleSharpStyledDocument : IStyledDocument, IPdfDocumentContext
{
    private readonly int _elementCount;
    private readonly int _maxDepth;
    private readonly long _totalStylesheetBytes;
    private readonly long _sourceHtmlBytes;

    internal AngleSharpStyledDocument(IDocument document, long sourceHtmlBytes)
    {
        AngleSharpDocument = document;
        _elementCount = document.All.Length;
        _maxDepth = ComputeMaxDepth(document);
        _totalStylesheetBytes = ComputeTotalStylesheetBytes(document);
        _sourceHtmlBytes = sourceHtmlBytes;
    }

    internal IDocument AngleSharpDocument { get; }

    int IPdfDocumentContext.ElementCount => _elementCount;
    int IPdfDocumentContext.MaxDepth => _maxDepth;
    long IPdfDocumentContext.TotalStylesheetBytes => _totalStylesheetBytes;
    long IPdfDocumentContext.SourceHtmlBytes => _sourceHtmlBytes;

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

    private static long ComputeTotalStylesheetBytes(IDocument document)
    {
        long total = 0;
        foreach (IStyleSheet sheet in document.StyleSheets)
        {
            string text = sheet.OwnerNode?.TextContent ?? string.Empty;
            total += Encoding.UTF8.GetByteCount(text);
        }
        return total;
    }
}
