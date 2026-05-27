namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Marker box for a forced line break (&lt;br&gt;). Zero size. When encountered in an inline
/// stream, InlineLayoutEngine commits the pending line immediately and starts a new line.
/// </summary>
internal sealed class LineBreakBox : BoxNode
{
    // No additional properties. Acts as a forced-line-commit signal.
}
