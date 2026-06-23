namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// A parsed CSS <c>linear-gradient(...)</c> background (Phase 14). Rendered by the writer as a PDF
/// axial shading (ShadingType 2). <see cref="AngleDegrees"/> follows CSS convention: 0° points to
/// the top of the box, increasing clockwise (90° = right, 180° = bottom).
/// </summary>
internal sealed class LinearGradient
{
    /// <summary>CSS gradient angle in degrees (0 = to top, 90 = to right, 180 = to bottom).</summary>
    public float AngleDegrees { get; init; } = 180f;

    /// <summary>Ordered color stops (at least two when the gradient is renderable).</summary>
    public IReadOnlyList<GradientStop> Stops { get; init; } = System.Array.Empty<GradientStop>();
}

/// <summary>A single gradient color stop. <see cref="Position"/> is a 0..1 fraction, or null (auto).</summary>
internal readonly record struct GradientStop(string Color, float? Position);
