namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Phase 15: a shared affine transform context for a <c>transform:</c> block and all of its
/// descendant boxes, so the block and its text transform as a rigid group about a single pivot.
/// The pivot is the origin element's box center, resolved per page by the writer from the
/// laid-out rect. Grouping is by reference identity.
/// <para>
/// <see cref="Matrix"/> is a pivot-composed 2×3 CSS-space affine matrix <c>[a,b,c,d,e,f]</c>
/// such that <c>x' = a*x + c*y + e</c>, <c>y' = b*x + d*y + f</c>. The pivot composition
/// <c>T(px,py)*M_css*T(-px,-py)</c> is performed at parse time by
/// <c>BoxTreeBuilder.TryParseTransformMatrix</c> using the layout-space box center. The writer
/// applies the PDF y-up flip when emitting the <c>cm</c> operator.
/// </para>
/// </summary>
internal sealed class TransformGroup
{
    /// <summary>
    /// Pivot-composed 2×3 affine matrix in CSS/layout coordinates.
    /// Indices: [0]=a, [1]=b, [2]=c, [3]=d, [4]=e, [5]=f.
    /// Length is always 6 when non-null. Use pattern matching (<c>is { Length: 6 } m</c>) to
    /// access — never the null-forgiving <c>!</c> operator (MSTD0002).
    /// </summary>
    public double[] Matrix { get; init; } = [];
}
