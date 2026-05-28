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

    // Float accumulator — scoped to a BFC; reset to 0f when entering a BFC root.
    /// <summary>X coordinate of the right edge of the current left float.</summary>
    public float LeftFloatRight { get; set; }
    /// <summary>X coordinate of the left edge of the current right float.</summary>
    public float RightFloatLeft { get; set; }
    /// <summary>Y coordinate of the bottom of the current left float.</summary>
    public float LeftFloatBottom { get; set; }
    /// <summary>Y coordinate of the bottom of the current right float.</summary>
    public float RightFloatBottom { get; set; }
}
