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
}
