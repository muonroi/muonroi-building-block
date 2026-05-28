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

    /// <summary>Raw CSS 'top' value for percentage resolution at layout time.</summary>
    public string? TopRaw { get; set; }
    /// <summary>Raw CSS 'left' value for percentage resolution at layout time.</summary>
    public string? LeftRaw { get; set; }
    /// <summary>Raw CSS 'right' value for percentage resolution at layout time.</summary>
    public string? RightRaw { get; set; }

    public List<BoxNode> Children { get; } = new();
}
