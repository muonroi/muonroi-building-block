namespace Muonroi.Pdf.Tests.Helpers;

internal sealed class FakeStyledDocument(IStyledNode root, IPageRule? pageRule = null,
    IReadOnlyList<FontFaceDeclaration>? fontFaces = null) : IStyledDocument
{
    public IStyledNode Root { get; } = root;
    public IPageRule? PageRule { get; } = pageRule;
    public IReadOnlyList<FontFaceDeclaration> FontFaces { get; } = fontFaces ?? Array.Empty<FontFaceDeclaration>();
}
