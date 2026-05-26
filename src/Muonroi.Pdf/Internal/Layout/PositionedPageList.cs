namespace Muonroi.Pdf.Internal.Layout;

internal sealed class PositionedPageList : IPositionedPageList
{
    public List<PositionedPage> Pages { get; } = new();
    public int PageCount => Pages.Count;
}
