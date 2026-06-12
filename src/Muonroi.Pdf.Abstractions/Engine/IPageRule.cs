using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>Resolved @page rule: margins, margin-box HTML, and page size descriptor.</summary>
public interface IPageRule
{
    PdfMargins Margins { get; }
    string? TopMarginBoxHtml { get; }
    string? BottomMarginBoxHtml { get; }
    string? Size { get; }
}
