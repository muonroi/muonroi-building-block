namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// A parsed CSS <c>radial-gradient(...)</c> background (Phase 15). Rendered by the writer as a PDF
/// radial shading (ShadingType 3). Supports <see cref="Shape"/> of "circle" or "ellipse" and
/// keyword-based <see cref="PositionX"/>/<see cref="PositionY"/> fractions (D-04/D-05).
/// </summary>
internal sealed class RadialGradient
{
    /// <summary>
    /// Gradient shape: <c>"circle"</c> or <c>"ellipse"</c> (CSS default). Determines whether the
    /// PDF shading uses a concentric-circle <c>/Coords</c> (circle) or a unit-circle dict with an
    /// anisotropic CTM scale in the content stream (ellipse).
    /// </summary>
    public string Shape { get; init; } = "ellipse";

    /// <summary>
    /// Horizontal position of the gradient center as a 0..1 fraction of the box width.
    /// Default 0.5 (center). Resolved from CSS <c>at &lt;position&gt;</c> keyword.
    /// </summary>
    public float PositionX { get; init; } = 0.5f;

    /// <summary>
    /// Vertical position of the gradient center as a 0..1 fraction of the box height.
    /// Default 0.5 (center). 0 = top, 1 = bottom (CSS y-down convention; writer applies PDF y-flip).
    /// </summary>
    public float PositionY { get; init; } = 0.5f;

    /// <summary>Ordered color stops (at least two when the gradient is renderable).</summary>
    public IReadOnlyList<GradientStop> Stops { get; init; } = System.Array.Empty<GradientStop>();
}
