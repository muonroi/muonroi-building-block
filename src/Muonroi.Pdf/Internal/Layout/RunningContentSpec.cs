using Muonroi.Pdf.Abstractions.Engine;

namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// Phase 13: full-HTML running header/footer specification. Carries the CSS-cascaded fragment
/// documents for the left/center/right columns of the header and footer, plus the reserved band
/// heights and separator-line flags. Built by <c>MPdfService</c> (which has the parser + cascade
/// engine) and threaded into <see cref="LayoutEngine"/> so the fragments are laid out with the
/// SAME text metrics as the body and stamped per page BEFORE glyph collection runs — that ordering
/// is what lets page-number digits (counter substitution) get embedded into the font subset.
/// </summary>
internal sealed class RunningContentSpec
{
    public IStyledDocument? HeaderLeft { get; init; }
    public IStyledDocument? HeaderCenter { get; init; }
    public IStyledDocument? HeaderRight { get; init; }

    public IStyledDocument? FooterLeft { get; init; }
    public IStyledDocument? FooterCenter { get; init; }
    public IStyledDocument? FooterRight { get; init; }

    /// <summary>Reserved header band height in points (from <c>PdfHeaderFooter.HeightMm</c>).</summary>
    public float HeaderHeightPt { get; init; }

    /// <summary>Reserved footer band height in points (from <c>PdfHeaderFooter.HeightMm</c>).</summary>
    public float FooterHeightPt { get; init; }

    /// <summary>Draw a separator rule between the header band and the body.</summary>
    public bool HeaderShowLine { get; init; }

    /// <summary>Draw a separator rule between the body and the footer band.</summary>
    public bool FooterShowLine { get; init; }

    /// <summary>Separator-rule color (CSS hex, e.g. "#888888"). Null = engine default.</summary>
    public string? LineColor { get; init; }

    public bool HasHeader => HeaderLeft is not null || HeaderCenter is not null || HeaderRight is not null;
    public bool HasFooter => FooterLeft is not null || FooterCenter is not null || FooterRight is not null;
}

/// <summary>
/// Header/footer columns after layout: positioned elements (already X-offset into their column
/// third, Y in band-local space starting at 0) plus the resolved band heights. Produced inside
/// <see cref="LayoutEngine"/> and consumed by <see cref="PaginationEngine"/>, which clones the
/// elements onto every page (footer translated to the bottom band) and substitutes page counters.
/// </summary>
internal sealed class RenderedRunningContent
{
    public List<PositionedElement> HeaderElements { get; } = new();
    public List<PositionedElement> FooterElements { get; } = new();

    /// <summary>Resolved header band height = max(spec.HeaderHeightPt, measured content height).</summary>
    public float HeaderBandPt { get; set; }

    /// <summary>Resolved footer band height = max(spec.FooterHeightPt, measured content height).</summary>
    public float FooterBandPt { get; set; }

    public bool HeaderShowLine { get; set; }
    public bool FooterShowLine { get; set; }
    public string? LineColor { get; set; }

    /// <summary>Left edge of the content area in points (page left margin) — separator-rule X.</summary>
    public float ContentLeftPt { get; set; }

    /// <summary>Content area width in points (pageWidth − left − right margin) — separator-rule width.</summary>
    public float ContentWidthPt { get; set; }

    public bool HasHeader => HeaderElements.Count > 0 || HeaderBandPt > 0f;
    public bool HasFooter => FooterElements.Count > 0 || FooterBandPt > 0f;
}
