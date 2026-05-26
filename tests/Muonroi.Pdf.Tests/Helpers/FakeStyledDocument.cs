namespace Muonroi.Pdf.Tests.Helpers;

internal sealed class FakeStyledDocument : IStyledDocument
{
    public FakeStyledDocument(IStyledNode root, IPageRule? pageRule = null)
    {
        Root = root;
        PageRule = pageRule;
    }

    public IStyledNode Root { get; }
    public IPageRule? PageRule { get; }
}
