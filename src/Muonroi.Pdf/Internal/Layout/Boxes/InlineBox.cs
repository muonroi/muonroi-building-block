namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal sealed class InlineBox : BoxNode
{
    public string? Text { get; set; }
    public string FontFamily { get; set; } = "serif";
    public float FontSize { get; set; } = 12f;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string? Color { get; set; }
    public string VerticalAlign { get; set; } = "baseline";

    /// <summary>CSS line-height multiplier. 1.0 = normal.</summary>
    public float LineHeightFactor { get; set; } = 1.0f;

    /// <summary>CSS text-decoration: "underline" | "line-through" | "none" | null.</summary>
    public string? TextDecoration { get; set; }

    /// <summary>Non-null if this inline box is inside an &lt;a&gt; element.</summary>
    public string? LinkHref { get; set; }

    /// <summary>CSS text-transform: "uppercase" | null.</summary>
    public string? TextTransform { get; set; }

    /// <summary>CSS white-space: "pre-wrap" | "pre-line" | "nowrap" | null.</summary>
    public string? WhiteSpace { get; set; }
}
