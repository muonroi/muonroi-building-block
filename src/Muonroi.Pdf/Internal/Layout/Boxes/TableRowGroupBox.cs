namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal enum TableRowGroupType { Header, Body, Footer }

internal sealed class TableRowGroupBox : BoxNode
{
    public TableRowGroupType GroupType { get; set; } = TableRowGroupType.Body;
}
