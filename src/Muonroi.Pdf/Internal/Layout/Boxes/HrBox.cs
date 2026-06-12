namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Block-level horizontal rule marker box (&lt;hr&gt;). BlockLayoutEngine reserves
/// MarginTop + Thickness + MarginBottom of vertical space and emits a PositionedElement
/// so OwnedPdfWriter can draw the filled rectangle.
/// </summary>
internal sealed class HrBox : BoxNode
{
    /// <summary>Rule line thickness in points. Default 1pt.</summary>
    public float Thickness { get; set; } = 1f;

    /// <summary>Rule color as "r g b" string, or null for default gray "0.5 0.5 0.5".</summary>
    public string? Color { get; set; }

    /// <summary>Top margin in points. Default 4pt (browser default ~4px).</summary>
    public float MarginTopHr { get; set; } = 4f;

    /// <summary>Bottom margin in points. Default 4pt.</summary>
    public float MarginBottomHr { get; set; } = 4f;
}
