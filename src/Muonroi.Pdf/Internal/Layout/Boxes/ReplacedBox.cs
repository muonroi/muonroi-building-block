namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal sealed class ReplacedBox : BoxNode
{
    public string? Src { get; set; }
    public float NaturalWidth { get; set; }
    public float NaturalHeight { get; set; }
}
