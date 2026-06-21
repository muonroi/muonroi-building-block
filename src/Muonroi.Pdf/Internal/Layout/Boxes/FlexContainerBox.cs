namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// A flex container box (CSS <c>display:flex</c> / <c>inline-flex</c>). Created by
/// <see cref="BoxTreeBuilder"/> ONLY when <c>AllowModernLayout</c> is enabled; with the flag off
/// flex elements fall through to <see cref="BlockBox"/> (the soft-degrade path, preserving the
/// existing golden output). Carries the resolved container-level flex properties; the actual
/// flex layout algorithm is performed later by the FlexLayoutEngine (Plan 03). Flex-ITEM
/// properties (flex-grow/shrink/basis/order/align-self) live on <see cref="BoxNode"/> so any child
/// box type can participate as a flex item (FLEX-05).
/// </summary>
internal sealed class FlexContainerBox : BoxNode
{
    /// <summary>
    /// CSS <c>flex-direction</c>. One of: <c>row</c> | <c>row-reverse</c> | <c>column</c> |
    /// <c>column-reverse</c>. Default <c>row</c> (CSS initial value).
    /// </summary>
    public string FlexDirection { get; set; } = "row";

    /// <summary>
    /// CSS <c>flex-wrap</c>. One of: <c>nowrap</c> | <c>wrap</c> | <c>wrap-reverse</c>.
    /// Default <c>nowrap</c> (CSS initial value).
    /// </summary>
    public string FlexWrap { get; set; } = "nowrap";

    /// <summary>
    /// CSS <c>justify-content</c> (main-axis alignment). One of: <c>flex-start</c> |
    /// <c>flex-end</c> | <c>center</c> | <c>space-between</c> | <c>space-around</c> |
    /// <c>space-evenly</c>. Default <c>flex-start</c> (CSS initial value).
    /// </summary>
    public string JustifyContent { get; set; } = "flex-start";

    /// <summary>
    /// CSS <c>align-items</c> (cross-axis alignment of items). One of: <c>flex-start</c> |
    /// <c>flex-end</c> | <c>center</c> | <c>stretch</c> | <c>baseline</c>. Default
    /// <c>stretch</c> (CSS initial value).
    /// </summary>
    public string AlignItems { get; set; } = "stretch";

    /// <summary>
    /// CSS <c>align-content</c> (cross-axis alignment of lines, multi-line only). One of:
    /// <c>flex-start</c> | <c>flex-end</c> | <c>center</c> | <c>space-between</c> |
    /// <c>space-around</c> | <c>stretch</c>. Default <c>stretch</c> (CSS initial value).
    /// </summary>
    public string AlignContent { get; set; } = "stretch";

    /// <summary>CSS <c>row-gap</c> (resolved from <c>gap</c>/<c>row-gap</c>) in points. Default 0.</summary>
    public float RowGap { get; set; }

    /// <summary>CSS <c>column-gap</c> (resolved from <c>gap</c>/<c>column-gap</c>) in points. Default 0.</summary>
    public float ColumnGap { get; set; }

    /// <summary>
    /// True for <c>display:inline-flex</c>. First-cut treats the inline-flex container as an atomic
    /// block-level box for outer layout (D-05 discretion); the inner flex layout is identical.
    /// </summary>
    public bool IsInlineFlex { get; set; }
}
