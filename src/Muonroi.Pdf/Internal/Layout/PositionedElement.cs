using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Layout;

internal sealed class PositionedElement
{
    public Rect Position { get; set; }
    public BoxNode Source { get; set; } = null!;
    public int PageIndex { get; set; }
}
