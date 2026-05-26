namespace Muonroi.Pdf.Tests.Helpers;

internal sealed class FakePageRule : IPageRule
{
    public PdfMargins Margins { get; set; } = new(20, 20, 20, 20);
    public string? TopMarginBoxHtml { get; set; }
    public string? BottomMarginBoxHtml { get; set; }
    public string? Size { get; set; }
}
