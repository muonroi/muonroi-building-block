namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal sealed class InlineBox : BoxNode
{
    public string? Text { get; set; }
    public string FontFamily { get; set; } = "serif";
    public float FontSize { get; set; } = 12f;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string? Color { get; set; }
    public string VerticalAlign { get; set; } = "baseline";
}
