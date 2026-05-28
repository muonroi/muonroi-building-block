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
    {
        int maxIterations = exclusions.Count + 1;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            float bandTop = candidateY;
            float bandBottom = candidateY + boxHeight;

            // Collect bounds from exclusions overlapping [bandTop, bandBottom)
            float maxLeft = cb.X;
            float minRight = cb.X + cb.Width;
            float nearestBottom = float.MaxValue;
            bool hasCollision = false;

            foreach (FloatExclusion ex in exclusions)
            {
                // Overlap: ex.Top < bandBottom AND ex.Bottom > bandTop
                if (ex.Top >= bandBottom || ex.Bottom <= bandTop)
                    continue;

                if (ex.Side == FloatSide.Left)
                {
                    if (ex.Right > maxLeft)
                        maxLeft = ex.Right;
                }
                else
                {
                    if (ex.Left < minRight)
                        minRight = ex.Left;
                }
            }

            float availableWidth = minRight - maxLeft;

            // G15b epsilon: w-20 + w-50 + w-30 = 100% but cb.Width * 0.20f + 0.50f
            // accumulates float rounding error; the strict `>= boxWidth` check fails
            // by sub-pt when exact-fit floats split a 100% row. Permit a 0.5pt
            // tolerance so a third float that's geometrically valid (sum ≤ 100%
            // by spec) lands on the same row instead of dropping to the next.
            if (availableWidth >= boxWidth - 0.5f)
            {
                float x = side == FloatSide.Left ? maxLeft : minRight - boxWidth;
                return (x, candidateY, availableWidth);
            }

            // Find the nearest bottom among colliding exclusions to advance candidateY
            foreach (FloatExclusion ex in exclusions)
            {
                if (ex.Top >= bandBottom || ex.Bottom <= bandTop)
                    continue;

                if (ex.Bottom > candidateY && ex.Bottom < nearestBottom)
                {
                    nearestBottom = ex.Bottom;
                    hasCollision = true;
                }
            }

            if (!hasCollision || nearestBottom == float.MaxValue)
                break;

            candidateY = nearestBottom;
        }

        // Degenerate: return best-effort placement at final candidateY
        {
            float bandTop = candidateY;
            float bandBottom = candidateY + boxHeight;
            float maxLeft = cb.X;
            float minRight = cb.X + cb.Width;

            foreach (FloatExclusion ex in exclusions)
            {
                if (ex.Top >= bandBottom || ex.Bottom <= bandTop)
                    continue;

                if (ex.Side == FloatSide.Left && ex.Right > maxLeft)
                    maxLeft = ex.Right;
                else if (ex.Side == FloatSide.Right && ex.Left < minRight)
                    minRight = ex.Left;
            }

            float availableWidth = Math.Max(0f, minRight - maxLeft);
            float x = side == FloatSide.Left ? maxLeft : Math.Max(maxLeft, minRight - boxWidth);
            return (x, candidateY, availableWidth);
        }
    }

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
    {
        float bandTop = lineY;
        float bandBottom = lineY + lineHeight;
        float maxLeft = cb.X;
        float minRight = cb.X + cb.Width;

        foreach (FloatExclusion ex in exclusions)
        {
            if (ex.Top >= bandBottom || ex.Bottom <= bandTop)
                continue;

            if (ex.Side == FloatSide.Left && ex.Right > maxLeft)
                maxLeft = ex.Right;
            else if (ex.Side == FloatSide.Right && ex.Left < minRight)
                minRight = ex.Left;
        }

        return (maxLeft, Math.Max(0f, minRight - maxLeft));
    }

    /// <summary>
    /// Returns the Y below which all exclusions on the given side have ended —
    /// used to implement clear:left / clear:right / clear:both.
    /// </summary>
    public static float ClearY(FloatSide? side, IReadOnlyList<FloatExclusion> exclusions)
    {
        float maxBottom = 0f;

        foreach (FloatExclusion ex in exclusions)
        {
            if (side == null || ex.Side == side.Value)
            {
                if (ex.Bottom > maxBottom)
                    maxBottom = ex.Bottom;
            }
        }

        return maxBottom;
    }
}
