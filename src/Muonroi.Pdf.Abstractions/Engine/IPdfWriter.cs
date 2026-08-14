namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Serializes a positioned page list into a PDF byte stream written to an output <see cref="Stream"/>.
/// </summary>
public interface IPdfWriter
{
    /// <summary>
    /// Writes all pages in <paramref name="pages"/> as a valid PDF document to <paramref name="destination"/>.
    /// </summary>
    /// <param name="pages">Laid-out, positioned pages produced by the layout engine.</param>
    /// <param name="options">Render options controlling metadata, compression, and output behaviour.</param>
    /// <param name="destination">Writable stream that receives the raw PDF bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of bytes written to <paramref name="destination"/>.</returns>
    ValueTask<long> WriteAsync(
        IPositionedPageList pages,
        PdfRenderOptions options,
        Stream destination,
        CancellationToken ct = default);
}
