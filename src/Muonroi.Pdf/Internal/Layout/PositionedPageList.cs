namespace Muonroi.Pdf.Internal.Layout;

internal sealed class PositionedPageList : IPositionedPageList
{
    public List<PositionedPage> Pages { get; } = new();
    public int PageCount => Pages.Count;
    public IReadOnlyList<EmbeddedFontInfo> EmbeddedFonts { get; internal set; } = [];
    public IReadOnlyDictionary<string, DecodedImage> Images { get; internal set; } = new Dictionary<string, DecodedImage>();
}
