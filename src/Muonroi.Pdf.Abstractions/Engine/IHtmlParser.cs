namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Parses raw HTML text into a DOM representation that the CSS cascade engine can process.
/// </summary>
public interface IHtmlParser
{
    /// <summary>
    /// Parses the given HTML string and returns an opaque parsed-document handle.
    /// </summary>
    /// <param name="html">Full HTML document text to parse.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A parsed document ready to be passed to <see cref="ICssCascadeEngine.CascadeAsync"/>.</returns>
    ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default);
}
