namespace Muonroi.Pdf.Internal.Layout;

internal sealed class PositionedPage
{
    public List<PositionedElement> Elements { get; } = new();
    public int PageIndex { get; set; }
}
