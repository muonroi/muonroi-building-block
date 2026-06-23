using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Resolved <c>@page</c> rule: margins, page-size descriptor, and the six margin-box slots
/// (<c>@top-left/@top-center/@top-right</c> and <c>@bottom-left/@bottom-center/@bottom-right</c>).
/// Each slot is a plain-text fragment with <c>counter(page)</c>/<c>counter(pages)</c> left verbatim
/// for downstream page-number substitution; <see langword="null"/> when the box was not declared.
/// </summary>
public interface IPageRule
{
    /// <summary>Physical page margins derived from the CSS <c>@page</c> margin properties.</summary>
    PdfMargins Margins { get; }

    /// <summary>Content fragment for the <c>@top-left</c> margin box, or <see langword="null"/>.</summary>
    string? TopLeftHtml { get; }

    /// <summary>Content fragment for the <c>@top-center</c> margin box, or <see langword="null"/>.</summary>
    string? TopCenterHtml { get; }

    /// <summary>Content fragment for the <c>@top-right</c> margin box, or <see langword="null"/>.</summary>
    string? TopRightHtml { get; }

    /// <summary>Content fragment for the <c>@bottom-left</c> margin box, or <see langword="null"/>.</summary>
    string? BottomLeftHtml { get; }

    /// <summary>Content fragment for the <c>@bottom-center</c> margin box, or <see langword="null"/>.</summary>
    string? BottomCenterHtml { get; }

    /// <summary>Content fragment for the <c>@bottom-right</c> margin box, or <see langword="null"/>.</summary>
    string? BottomRightHtml { get; }

    /// <summary>True when any of the three top margin boxes declared content.</summary>
    bool HasTopMarginBoxes { get; }

    /// <summary>True when any of the three bottom margin boxes declared content.</summary>
    bool HasBottomMarginBoxes { get; }

    /// <summary>
    /// Raw CSS <c>size</c> descriptor value (e.g. <c>"A4"</c>, <c>"letter landscape"</c>),
    /// or <see langword="null"/> if the property was not set (engine uses its configured default).
    /// </summary>
    string? Size { get; }
}
