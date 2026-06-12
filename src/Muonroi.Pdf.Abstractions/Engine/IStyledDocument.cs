namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>Opaque handle to a CSS-cascaded document. Implementation holds computed styles internally.</summary>
public interface IStyledDocument
{
    IStyledNode Root { get; }
    IPageRule? PageRule { get; }
    IReadOnlyList<FontFaceDeclaration> FontFaces { get; }
}
