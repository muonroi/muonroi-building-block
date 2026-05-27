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

    public List<BoxNode> Children { get; } = new();
}
