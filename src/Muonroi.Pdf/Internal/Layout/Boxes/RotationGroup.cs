namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Phase 14: a shared rotation context for a <c>transform: rotate()</c> block and all of its
/// descendant boxes, so the block and its text rotate as a rigid group about a single pivot. The
/// pivot is the rotated block's box center, resolved per page by the writer from the origin
/// element's laid-out rect. Grouping is by reference identity.
/// </summary>
internal sealed class RotationGroup
{
    /// <summary>CSS rotation in degrees (clockwise; 45 = quarter-turn clockwise on screen).</summary>
    public float AngleDegrees { get; init; }
}
