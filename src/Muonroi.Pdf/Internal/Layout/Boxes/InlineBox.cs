namespace Muonroi.Pdf.Internal.Layout.Boxes;

internal sealed class InlineBox : BoxNode
{
    public string? Text { get; set; }
    public string FontFamily { get; set; } = "serif";
    public float FontSize { get; set; } = 12f;
    // Bold and TextTransform are inherited from BoxNode (G18 — block→inline propagation).
    // Do NOT redeclare them here — BoxNode.Bold and BoxNode.TextTransform are the canonical fields.
    public bool Italic { get; set; }
    public string? Color { get; set; }
    public string VerticalAlign { get; set; } = "baseline";

    /// <summary>CSS line-height multiplier. 1.0 = normal.</summary>
    public float LineHeightFactor { get; set; } = 1.0f;

    /// <summary>CSS text-decoration: "underline" | "line-through" | "none" | null.</summary>
    public string? TextDecoration { get; set; }

    /// <summary>Non-null if this inline box is inside an &lt;a&gt; element.</summary>
    public string? LinkHref { get; set; }

    /// <summary>CSS white-space: "pre-wrap" | "pre-line" | "nowrap" | null.</summary>
    public string? WhiteSpace { get; set; }

    /// <summary>
    /// Normalized word-break/overflow-wrap behavior:
    /// "break-all"   — split at any character boundary (CSS word-break: break-all)
    /// "break-word"  — split only when a single token would otherwise overflow
    ///                  (CSS word-break: break-word, overflow-wrap: break-word|anywhere, word-wrap: break-word)
    /// null/"normal" — break only at whitespace (default)
    /// </summary>
    public string? WordBreak { get; set; }
}
