namespace Muonroi.Pdf.Tests.Helpers;

internal sealed class FakeStyledDocument : IStyledDocument
{
    public FakeStyledDocument(IStyledNode root, IPageRule? pageRule = null,
        IReadOnlyList<FontFaceDeclaration>? fontFaces = null)
    {
        Root = root;
        PageRule = pageRule;
        FontFaces = fontFaces ?? Array.Empty<FontFaceDeclaration>();
    }

    public IStyledNode Root { get; }
    public IPageRule? PageRule { get; }
    public IReadOnlyList<FontFaceDeclaration> FontFaces { get; }
}
