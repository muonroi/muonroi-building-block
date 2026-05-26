namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal sealed class TableCellBox : BoxNode
{
    public int Colspan { get; set; } = 1;
    public int Rowspan { get; set; } = 1;
}
