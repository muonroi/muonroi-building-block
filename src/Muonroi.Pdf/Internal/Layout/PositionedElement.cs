namespace Muonroi.Pdf.Internal.Layout;

internal sealed class PositionedElement
{
    public Rect Position { get; set; }
    public BoxNode Source { get; set; } = null!;
    public int PageIndex { get; set; }

    /// <summary>
    /// The word/segment text that should be rendered at this position.
    /// For inline boxes that were word-split by InlineLayoutEngine, this is the individual word,
    /// NOT the full source box text. The writer MUST use this field (when non-null) instead of
    /// InlineBox.Text to avoid drawing the full line at every word position.
    /// </summary>
    public string? RenderedText { get; set; }
}
