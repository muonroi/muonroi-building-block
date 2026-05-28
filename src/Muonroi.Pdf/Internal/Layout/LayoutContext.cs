using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class LayoutContext
{
    public float PageWidth { get; set; }
    public float PageHeight { get; set; }
    public float AvailableWidth { get; set; }
    public float CurrentY { get; set; }
    public int CurrentPageIndex { get; set; }
    public int TotalPages { get; set; }
    public ITextMetrics TextMetrics { get; set; } = EstimatedTextMetrics.Instance;
    public PdfMargins PageMargins { get; set; } = PdfMargins.Default10mm;

    public float PageMarginTopPt => (float)(PageMargins.TopMm * Units.MmToPt);
    public float PageMarginBottomPt => (float)(PageMargins.BottomMm * Units.MmToPt);
    public float PageMarginLeftPt => (float)(PageMargins.LeftMm * Units.MmToPt);
    public float PageMarginRightPt => (float)(PageMargins.RightMm * Units.MmToPt);

    public float RemainingHeight => PageHeight - CurrentY;

    /// <summary>
    /// CSS text-align value for the current block context.
    /// Null or "left" = default left alignment.
    /// </summary>
    public string? TextAlign { get; set; }

    // ContainingBlock for CSS position:absolute resolution.
    // Set by BlockLayoutEngine when entering a position:relative box with explicit dimensions.
    // Null = no positioned ancestor (abs-pos falls back to page coordinates).
    public Rect? ContainingBlockRect { get; set; }

    // A2: Content origin X for table cells — absolute X of the cell's content area left edge.
    // Default 0f means "use PageMarginLeftPt" (page-level normal flow).
    // Set by TableLayoutEngine.CellContext to the cell's column X so inline/block content
    // inside cells renders at the correct column position, not at the page left margin.
    public float ContentOriginX { get; set; }

    // Float accumulator — scoped to a BFC; reset to 0f when entering a BFC root.
    /// <summary>X coordinate of the right edge of the current left float.</summary>
    public float LeftFloatRight { get; set; }
    /// <summary>X coordinate of the left edge of the current right float.</summary>
    public float RightFloatLeft { get; set; }
    /// <summary>Y coordinate of the bottom of the current left float.</summary>
    public float LeftFloatBottom { get; set; }
    /// <summary>Y coordinate of the bottom of the current right float.</summary>
    public float RightFloatBottom { get; set; }

    /// <summary>
    /// Placed floats in the current BFC. Populated by BlockLayoutEngine float placement;
    /// queried by FloatPlacementSolver for every subsequent float or line box.
    /// Lifecycle: cleared when entering a BFC root (same reset point as the four old cursor fields).
    /// Per-phase scope: single BFC per RunLayout call — nested BFC stacks deferred to Phase 8.9.
    /// </summary>
    public List<FloatExclusion> Exclusions { get; set; } = new();
}
