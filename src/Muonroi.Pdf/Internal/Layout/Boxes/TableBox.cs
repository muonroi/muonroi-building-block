namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal sealed class TableBox : BoxNode
{
    public string TableLayout { get; set; } = "auto";
    public float BorderSpacing { get; set; }
}
