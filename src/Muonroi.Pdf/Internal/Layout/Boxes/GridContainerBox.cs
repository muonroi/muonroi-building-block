namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// A grid container box (CSS <c>display:grid</c> / <c>inline-grid</c>). Created by
/// <see cref="BoxTreeBuilder"/> ONLY when <c>AllowModernLayout</c> is enabled; with the flag off
/// grid elements fall through to <see cref="BlockBox"/> (the soft-degrade path, preserving the
/// existing golden output). Carries the resolved container-level grid properties; the actual
/// grid layout algorithm runs later in the GridLayoutEngine (Plan 03). Grid-ITEM properties
/// (grid-column/-row/-area, justify-self, align-self) live on <see cref="BoxNode"/> so any child
/// box type can participate as a grid item (GRID-04).
/// </summary>
internal sealed class GridContainerBox : BoxNode
{
    /// <summary>
    /// CSS <c>grid-template-columns</c> as an ordered track list (after <c>repeat()</c>/<c>minmax()</c>
    /// expansion). Empty = no explicit columns (implicit grid via <see cref="AutoColumns"/>).
    /// </summary>
    public List<GridTrack> TemplateColumns { get; set; } = new();

    /// <summary>
    /// CSS <c>grid-template-rows</c> as an ordered track list (after <c>repeat()</c>/<c>minmax()</c>
    /// expansion). Empty = no explicit rows (implicit grid via <see cref="AutoRows"/>).
    /// </summary>
    public List<GridTrack> TemplateRows { get; set; } = new();

    /// <summary>CSS <c>grid-auto-columns</c>: the track template for implicitly-created columns. Null = <c>auto</c> (CSS initial).</summary>
    public GridTrack? AutoColumns { get; set; }

    /// <summary>CSS <c>grid-auto-rows</c>: the track template for implicitly-created rows. Null = <c>auto</c> (CSS initial).</summary>
    public GridTrack? AutoRows { get; set; }

    /// <summary>
    /// CSS <c>grid-auto-flow</c>. One of <c>row</c> | <c>column</c>. Default <c>row</c> (CSS initial).
    /// A trailing <c>dense</c> token is stripped — dense packing is OUT of scope (sparse-only, D-01).
    /// </summary>
    public string AutoFlow { get; set; } = "row";

    /// <summary>CSS <c>row-gap</c> (resolved from <c>gap</c>/<c>row-gap</c>) in points. Default 0.</summary>
    public float RowGap { get; set; }

    /// <summary>CSS <c>column-gap</c> (resolved from <c>gap</c>/<c>column-gap</c>) in points. Default 0.</summary>
    public float ColumnGap { get; set; }

    /// <summary>
    /// CSS <c>justify-items</c> (inline-axis alignment of items within their cell). One of
    /// <c>start</c> | <c>end</c> | <c>center</c> | <c>stretch</c>. Default <c>stretch</c> (CSS initial).
    /// </summary>
    public string JustifyItems { get; set; } = "stretch";

    /// <summary>
    /// CSS <c>align-items</c> (block-axis alignment of items within their cell). One of
    /// <c>start</c> | <c>end</c> | <c>center</c> | <c>stretch</c>. Default <c>stretch</c> (CSS initial).
    /// </summary>
    public string AlignItems { get; set; } = "stretch";

    /// <summary>
    /// CSS <c>justify-content</c> (inline-axis alignment of the track group within the container).
    /// One of <c>start</c> | <c>end</c> | <c>center</c> | <c>space-between</c> | <c>space-around</c> |
    /// <c>space-evenly</c> | <c>stretch</c>. Default <c>start</c> (CSS initial <c>normal</c> treated as start).
    /// </summary>
    public string JustifyContent { get; set; } = "start";

    /// <summary>
    /// CSS <c>align-content</c> (block-axis alignment of the track group within the container).
    /// One of <c>start</c> | <c>end</c> | <c>center</c> | <c>space-between</c> | <c>space-around</c> |
    /// <c>space-evenly</c> | <c>stretch</c>. Default <c>start</c> (CSS initial <c>normal</c> treated as start).
    /// </summary>
    public string AlignContent { get; set; } = "start";

    /// <summary>
    /// CSS <c>grid-template-areas</c> as a rectangular row-major array of area-name tokens
    /// (<c>"."</c> = an empty cell). Empty when unset or when the declared rows are ragged/empty
    /// (rejected to keep downstream cell-rect math in-bounds — T-19-05). The name→cell-rect lookup
    /// is derived at layout time (Plan 03).
    /// </summary>
    public string[][] TemplateAreas { get; set; } = System.Array.Empty<string[]>();

    /// <summary>
    /// True for <c>display:inline-grid</c>. First-cut treats the inline-grid container as an atomic
    /// block-level box for outer layout (D-01 discretion), mirroring <see cref="FlexContainerBox.IsInlineFlex"/>.
    /// </summary>
    public bool IsInlineGrid { get; set; }
}
