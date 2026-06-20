using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>Resolved @page rule: margins, margin-box HTML, and page size descriptor.</summary>
public interface IPageRule
{
    /// <summary>Physical page margins derived from the CSS <c>@page</c> margin properties.</summary>
    PdfMargins Margins { get; }

    /// <summary>
    /// HTML content for the top margin box (<c>@top-center</c> or similar margin-box at-rules),
    /// or <see langword="null"/> if no top margin box was declared.
    /// </summary>
    string? TopMarginBoxHtml { get; }

    /// <summary>
    /// HTML content for the bottom margin box (<c>@bottom-center</c> or similar margin-box at-rules),
    /// or <see langword="null"/> if no bottom margin box was declared.
    /// </summary>
    string? BottomMarginBoxHtml { get; }

    /// <summary>
    /// Raw CSS <c>size</c> descriptor value (e.g. <c>"A4"</c>, <c>"letter landscape"</c>),
    /// or <see langword="null"/> if the property was not set (engine uses its configured default).
    /// </summary>
    string? Size { get; }
}
