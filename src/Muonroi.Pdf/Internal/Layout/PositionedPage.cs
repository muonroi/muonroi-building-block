namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// A collected link annotation for a single word-run on a page.
/// Rect coordinates use layout space (Y=0 at top); OwnedPdfWriter flips Y for PDF output.
/// </summary>
internal readonly record struct LinkAnnotation(
    string Href,
    float X,
    float Y,
    float Width,
    float Height,
    int PageIndex);

internal sealed class PositionedPage
{
    public List<PositionedElement> Elements { get; } = new();
    public int PageIndex { get; set; }

    /// <summary>Link annotations collected during inline layout for this page.</summary>
    public List<LinkAnnotation> LinkAnnotations { get; } = new();
}
