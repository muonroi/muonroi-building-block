using Muonroi.Pdf.Abstractions.Engine;

namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal abstract class BoxNode
{
    public string Display { get; set; } = "block";
    public string? PageBreakBefore { get; set; }
    public string? PageBreakAfter { get; set; }
    public string? PageBreakInside { get; set; }

    public float Width { get; set; } = -1f;
    public string? WidthRaw { get; set; }
    public float Height { get; set; } = -1f;

    /// <summary>CSS max-width in points. -1f = not set (no upper clamp).</summary>
    public float MaxWidth { get; set; } = -1f;

    /// <summary>CSS min-width in points. -1f = not set (no lower clamp).</summary>
    public float MinWidth { get; set; } = -1f;

    public float MarginTop { get; set; }
    public float MarginRight { get; set; }
    public float MarginBottom { get; set; }
    public float MarginLeft { get; set; }

    public float PaddingTop { get; set; }
    public float PaddingRight { get; set; }
    public float PaddingBottom { get; set; }
    public float PaddingLeft { get; set; }

    public float BorderTop { get; set; }
    public float BorderRight { get; set; }
    public float BorderBottom { get; set; }
    public float BorderLeft { get; set; }

    public IStyledNode? Source { get; set; }

    /// <summary>CSS text-align (inherited). Null = left (default).</summary>
    public string? TextAlign { get; set; }

    /// <summary>CSS float: "left" | "right" | null (null = not floated).</summary>
    public string? FloatValue { get; set; }

    /// <summary>CSS clear: "left" | "right" | "both" | null.</summary>
    public string? ClearValue { get; set; }

    /// <summary>CSS background-color value (e.g. "#CCCCCC"). Null = transparent.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Data URI extracted from CSS background-image: url(data:...). Null = no background image.</summary>
    public string? BackgroundImageSrc { get; set; }

    /// <summary>Parsed CSS linear-gradient background (Phase 14). Null = no gradient.</summary>
    public LinearGradient? BackgroundGradient { get; set; }

    /// <summary>Parsed CSS radial-gradient background (Phase 15). Null = no radial gradient.</summary>
    public RadialGradient? BackgroundRadialGradient { get; set; }

    /// <summary>
    /// True when this box is the origin element that set a <c>transform:</c> value (Phase 15).
    /// Only the origin block carries <c>true</c>; descendants share the <see cref="TransformGroup"/>
    /// but have <c>HasTransform = false</c>. Replaces the Phase 14 <c>RotationDegrees != 0f</c>
    /// sentinel used by the writer to identify the pivot-defining element.
    /// </summary>
    public bool HasTransform { get; set; }

    /// <summary>Shared affine transform context (Phase 15) for a transformed block and its descendants. Null = none.</summary>
    public TransformGroup? TransformGroup { get; set; }

    /// <summary>CSS position: "absolute" | "relative" | null (null = static).</summary>
    public string? Position { get; set; }

    /// <summary>
    /// CSS overflow value: "hidden" | "scroll" | "auto" | null (null = visible/default).
    /// Used by BlockLayoutEngine to determine whether this box establishes a containing block
    /// for absolutely-positioned descendants (CSS 2.1 §10.1 + pragmatic overflow:hidden convention).
    /// </summary>
    public string? Overflow { get; set; }

    /// <summary>Raw CSS 'top' value for percentage resolution at layout time.</summary>
    public string? TopRaw { get; set; }
    /// <summary>Raw CSS 'left' value for percentage resolution at layout time.</summary>
    public string? LeftRaw { get; set; }
    /// <summary>Raw CSS 'right' value for percentage resolution at layout time.</summary>
    public string? RightRaw { get; set; }

    // Flex-ITEM properties (CSS flexbox). Resolved on EVERY box so a child of any type can be a
    // flex item. All nullable: null = the CSS initial value, and an unset value means zero
    // behavioural change for non-flex layouts (these are untouched outside a flex container).
    // Consumed by FlexLayoutEngine (Plan 03).

    /// <summary>CSS <c>flex-grow</c>. Null = CSS initial value 0. Consumed by FlexLayoutEngine (Plan 03); untouched for non-flex layouts.</summary>
    public float? FlexGrow { get; set; }

    /// <summary>CSS <c>flex-shrink</c>. Null = CSS initial value 1. Consumed by FlexLayoutEngine (Plan 03); untouched for non-flex layouts.</summary>
    public float? FlexShrink { get; set; }

    /// <summary>
    /// Raw CSS <c>flex-basis</c> token (<c>auto</c> | length | <c>content</c> | null). Resolved at
    /// layout time against the main axis, like <see cref="WidthRaw"/>. Null = CSS initial value
    /// <c>auto</c>. Consumed by FlexLayoutEngine (Plan 03); untouched for non-flex layouts.
    /// </summary>
    public string? FlexBasisRaw { get; set; }

    /// <summary>CSS <c>order</c>. Null = CSS initial value 0. Consumed by FlexLayoutEngine (Plan 03); untouched for non-flex layouts.</summary>
    public int? Order { get; set; }

    /// <summary>
    /// CSS <c>align-self</c>. Null = CSS initial value <c>auto</c> (inherit the container
    /// <see cref="FlexContainerBox.AlignItems"/>). Consumed by FlexLayoutEngine (Plan 03);
    /// untouched for non-flex layouts.
    /// </summary>
    public string? AlignSelf { get; set; }

    // Grid-ITEM properties (CSS Grid). Resolved on EVERY box so a child of any type can be a grid
    // item. All nullable: null = the CSS initial value, leaving zero behavioural change for non-grid
    // layouts (untouched outside a grid container). Consumed by GridLayoutEngine (Plan 03).
    // align-self is REUSED from the Phase-18 flex-item props above (line ~114).

    /// <summary>
    /// Raw CSS <c>grid-column</c> token (e.g. <c>"2"</c>, <c>"1 / 3"</c>, <c>"span 2"</c>). Null = CSS
    /// initial <c>auto</c>. Consumed by GridLayoutEngine (Plan 03); untouched for non-grid layouts.
    /// </summary>
    public string? GridColumnRaw { get; set; }

    /// <summary>
    /// Raw CSS <c>grid-row</c> token (e.g. <c>"1 / 3"</c>, <c>"span 2"</c>). Null = CSS initial
    /// <c>auto</c>. Consumed by GridLayoutEngine (Plan 03); untouched for non-grid layouts.
    /// </summary>
    public string? GridRowRaw { get; set; }

    /// <summary>
    /// Raw CSS <c>grid-area</c> token: a named area, or the
    /// <c>row-start / col-start / row-end / col-end</c> shorthand. Null = CSS initial <c>auto</c>.
    /// Consumed by GridLayoutEngine (Plan 03); untouched for non-grid layouts.
    /// </summary>
    public string? GridAreaRaw { get; set; }

    /// <summary>
    /// CSS <c>justify-self</c> (inline-axis self alignment within the grid cell). One of
    /// <c>start</c> | <c>end</c> | <c>center</c> | <c>stretch</c>. Null = CSS initial <c>auto</c>
    /// (inherit the container <see cref="GridContainerBox.JustifyItems"/>). Consumed by
    /// GridLayoutEngine (Plan 03); untouched for non-grid layouts.
    /// </summary>
    public string? JustifySelf { get; set; }

    public List<BoxNode> Children { get; } = new();

    /// <summary>
    /// True when this box represents the HTML &lt;body&gt; element (root document body).
    /// Used by BlockLayoutEngine.ResolveWidth to clamp explicit body width to the available
    /// page content area (Fix C2 — CSS 2.1 §10.3.3: body overflowing the page margin area).
    /// </summary>
    public bool IsBodyRoot { get; set; }

    // G18: inherited text properties — resolved in ResolveCssProperties for ALL box types,
    // then propagated from block ancestors to inline children during BuildNode recursion.
    // InlineBox reads these to drive font selection and text-run uppercasing.

    /// <summary>CSS font-weight: true when resolved to bold (≥700 or keyword "bold").
    /// Set by UA stylesheet for h1-h6; overridden by author-level font-weight declarations.</summary>
    public bool Bold { get; set; }

    /// <summary>CSS text-transform: "uppercase" | null (other values ignored for now).</summary>
    public string? TextTransform { get; set; }

    /// <summary>
    /// Phase 12.4b: inherited normalized word-break/overflow-wrap.
    /// "break-all"  — split at any character boundary (word-break: break-all)
    /// "break-word" — split only when a token would otherwise overflow
    ///                (word-break:break-word | overflow-wrap:break-word|anywhere | word-wrap:break-word)
    /// null         — default whitespace-only break.
    /// Resolved on any box that the cascade reaches; propagated to InlineBox descendants
    /// by PropagateInheritedTextProps because production templates declare it on `td` via
    /// class-descendant selectors that AngleSharp.Css fails to inherit through.
    /// </summary>
    public string? WordBreak { get; set; }

    /// <summary>
    /// CSS white-space: "pre-wrap" | "pre-line" | "nowrap" | null. Resolved on any box the
    /// cascade reaches and propagated to InlineBox descendants by PropagateInheritedTextProps —
    /// production templates declare it on `td` via class-descendant selectors that AngleSharp.Css
    /// fails to inherit through (G29). Declared here (not on InlineBox) so block/cell ancestors
    /// can hold the cascaded value and push it down to text-node descendants.
    /// </summary>
    public string? WhiteSpace { get; set; }

    /// <summary>CSS border-radius in points (CSS3). Default: 0f (no rounding).</summary>
    public float BorderRadius { get; set; } = 0f;

    /// <summary>CSS opacity (CSS3). Default: 1f (opaque).</summary>
    public float Opacity { get; set; } = 1f;
}
