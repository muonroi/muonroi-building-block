using System.Linq;
using System.Text;
using AngleSharp.Css.Dom;
using PdfFontStyle = Muonroi.Pdf.Abstractions.FontStyle;
using PdfFontWeight = Muonroi.Pdf.Abstractions.FontWeight;

namespace Muonroi.Pdf.Governance.Cascade;

internal sealed class AngleSharpStyledDocument : IStyledDocument, IPdfDocumentContext
{
    private readonly int _elementCount;
    private readonly int _maxDepth;
    private readonly long _totalStylesheetBytes;
    private readonly long _sourceHtmlBytes;
    private readonly IWindow? _window;
    private readonly IReadOnlyList<FontFaceDeclaration> _fontFaces;

    internal AngleSharpStyledDocument(IDocument document, long sourceHtmlBytes)
    {
        AngleSharpDocument = document;
        _elementCount = document.All.Length;
        _maxDepth = ComputeMaxDepth(document);
        _totalStylesheetBytes = ComputeTotalStylesheetBytes(document);
        _sourceHtmlBytes = sourceHtmlBytes;
        _window = document.DefaultView;
        Root = new AngleSharpStyledNode(
            document.DocumentElement ?? throw new InvalidOperationException("Document has no root element."),
            _window);
        PageRule = AngleSharpPageRule.TryExtract(document);
        _fontFaces = ExtractFontFaces(document);
    }

    internal IDocument AngleSharpDocument { get; }

    public IStyledNode Root { get; }
    public IPageRule? PageRule { get; }
    public IReadOnlyList<FontFaceDeclaration> FontFaces => _fontFaces;

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

    private static IReadOnlyList<FontFaceDeclaration> ExtractFontFaces(IDocument document)
    {
        var list = new List<FontFaceDeclaration>();
        foreach (ICssStyleSheet sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            ICssRuleList rules = sheet.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i] is not ICssFontFaceRule fontFaceRule)
                    continue;

                string family = fontFaceRule.Family.Trim('\'', '"');
                PdfFontWeight weight = ParseFontWeight(fontFaceRule.Weight);
                PdfFontStyle style = ParseFontStyle(fontFaceRule.Style);
                list.Add(new FontFaceDeclaration(family, weight, style));
            }
        }
        return list.Distinct().ToList();
    }

    private static PdfFontWeight ParseFontWeight(string? weight)
    {
        if (string.IsNullOrEmpty(weight) || weight == "normal")
            return PdfFontWeight.Normal;
        if (weight == "bold")
            return PdfFontWeight.Bold;
        if (int.TryParse(weight, out int value) && Enum.IsDefined(typeof(PdfFontWeight), value))
            return (PdfFontWeight)value;
        return PdfFontWeight.Normal;
    }

    private static PdfFontStyle ParseFontStyle(string? style) =>
        style switch
        {
            "italic" => PdfFontStyle.Italic,
            "oblique" => PdfFontStyle.Oblique,
            _ => PdfFontStyle.Normal
        };
}
