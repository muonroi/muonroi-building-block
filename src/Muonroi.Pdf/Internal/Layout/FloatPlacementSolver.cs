namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// Minimal containing-block info needed by the float solver.
/// Matches WeasyPrint's cb.content_box_x / cb.width parameters.
/// </summary>
internal readonly record struct ContainingBlock(float X, float Width);

/// <summary>
/// Clean-room derivation of WeasyPrint's avoid_collisions algorithm (float.py ~lines 116-189).
/// Operates on an immutable snapshot of FloatExclusion; never mutates the list.
/// All coordinate arithmetic is in points.
/// </summary>
internal static class FloatPlacementSolver
{
    /// <summary>
    /// Computes the final (x, y, availableWidth) for a float box to be placed,
    /// advancing candidateY until the float fits horizontally.
    /// Returns the resolved placement; caller appends a new FloatExclusion to the list.
    /// </summary>
    /// <param name="candidateY">Starting Y (top of current line / normal-flow cursor).</param>
    /// <param name="boxWidth">Used width of the float (including margins).</param>
    /// <param name="boxHeight">Used height of the float (pre-computed).</param>
    /// <param name="side">Left or Right float.</param>
    /// <param name="cb">Containing block X and width for this BFC.</param>
    /// <param name="exclusions">Current list of placed floats in this BFC.</param>
    /// <returns>Resolved (x, y, availableWidth) for the float's content-box left edge.</returns>
    public static (float X, float Y, float AvailableWidth) AvoidCollisions(
        float candidateY,
        float boxWidth,
        float boxHeight,
        FloatSide side,
        ContainingBlock cb,
        IReadOnlyList<FloatExclusion> exclusions)
        => throw new NotImplementedException();

    /// <summary>
    /// Returns (startX, availableWidth) for a line box at the given Y with the given height.
    /// Equivalent to WeasyPrint inline.py's call to avoid_collisions for linebox width.
    /// </summary>
    /// <param name="lineY">Top Y of the line box.</param>
    /// <param name="lineHeight">Height of the line box (typically one line-height unit).</param>
    /// <param name="cb">Containing block.</param>
    /// <param name="exclusions">Current exclusion list.</param>
    /// <returns>(startX, availableWidth) — the usable horizontal band for this line.</returns>
    public static (float StartX, float AvailableWidth) AvailableWidthAtY(
        float lineY,
        float lineHeight,
        ContainingBlock cb,
        IReadOnlyList<FloatExclusion> exclusions)
        => throw new NotImplementedException();

    /// <summary>
    /// Returns the Y below which all exclusions on the given side have ended —
    /// used to implement clear:left / clear:right / clear:both.
    /// </summary>
    public static float ClearY(FloatSide? side, IReadOnlyList<FloatExclusion> exclusions)
        => throw new NotImplementedException();
}
