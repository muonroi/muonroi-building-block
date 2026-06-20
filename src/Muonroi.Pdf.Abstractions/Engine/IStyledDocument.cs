namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>Opaque handle to a CSS-cascaded document. Implementation holds computed styles internally.</summary>
public interface IStyledDocument
{
    /// <summary>Root node of the styled DOM tree (typically the <c>&lt;html&gt;</c> element).</summary>
    IStyledNode Root { get; }

    /// <summary>
    /// Resolved <c>@page</c> rule for the document, or <see langword="null"/> if no
    /// <c>@page</c> rule was declared in any stylesheet.
    /// </summary>
    IPageRule? PageRule { get; }

    /// <summary>All <c>@font-face</c> declarations collected during cascade, in source order.</summary>
    IReadOnlyList<FontFaceDeclaration> FontFaces { get; }
}
