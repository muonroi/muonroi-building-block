namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Applies CSS cascade rules to a parsed HTML document and produces a styled document
/// whose nodes carry fully-resolved computed style values.
/// </summary>
public interface ICssCascadeEngine
{
    /// <summary>
    /// Cascades all stylesheets (embedded, linked, and optional user stylesheet) against
    /// <paramref name="doc"/> and returns an <see cref="IStyledDocument"/> whose nodes expose
    /// computed CSS property values via <see cref="IComputedStyle"/>.
    /// </summary>
    /// <param name="doc">The parsed HTML document to style.</param>
    /// <param name="userStyleSheet">
    /// Optional CSS text injected as a user-agent stylesheet at the lowest cascade origin.
    /// Pass <see langword="null"/> to skip.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A styled document ready for layout and rendering.</returns>
    ValueTask<IStyledDocument> CascadeAsync(IParsedDocument doc, string? userStyleSheet, CancellationToken ct = default);
}
