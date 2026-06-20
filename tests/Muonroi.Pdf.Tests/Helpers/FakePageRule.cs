namespace Muonroi.Pdf.Tests.Helpers;

internal sealed class FakePageRule : IPageRule
{
    public PdfMargins Margins { get; set; } = new(20, 20, 20, 20);
    public string? TopLeftHtml { get; set; }
    public string? TopCenterHtml { get; set; }
    public string? TopRightHtml { get; set; }
    public string? BottomLeftHtml { get; set; }
    public string? BottomCenterHtml { get; set; }
    public string? BottomRightHtml { get; set; }
    public bool HasTopMarginBoxes =>
        TopLeftHtml is not null || TopCenterHtml is not null || TopRightHtml is not null;
    public bool HasBottomMarginBoxes =>
        BottomLeftHtml is not null || BottomCenterHtml is not null || BottomRightHtml is not null;
    public string? Size { get; set; }
}
