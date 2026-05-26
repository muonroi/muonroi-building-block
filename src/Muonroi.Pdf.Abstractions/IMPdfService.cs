namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Primary HTML/CSS to PDF rendering service.
/// </summary>
/// <remarks>
/// Implementations are singleton-safe but resolve tenant context per call via <c>ITenantContext</c>.
/// All internal caches are tenant-scoped to prevent cross-tenant data leakage.
/// </remarks>
public interface IMPdfService
{
    /// <summary>
    /// Renders HTML to PDF, writing directly to <paramref name="destination"/>.
    /// Recommended overload — avoids materialising the full document in memory.
    /// </summary>
    /// <param name="html">Source HTML.</param>
    /// <param name="destination">Caller-owned writable stream. The engine writes the PDF here.</param>
    /// <param name="options">Per-call options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Render metadata (page count, byte count, timing, template hash, policy id).</returns>
    Task<PdfRenderResult> RenderAsync(
        string html,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders multiple HTML fragments as a single PDF document, in order.
    /// Each fragment starts on a new page.
    /// </summary>
    /// <param name="htmlPages">HTML fragments. Empty collection yields a 0-page result.</param>
    /// <param name="destination">Caller-owned writable stream.</param>
    /// <param name="options">Per-call options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PdfRenderResult> RenderMultiPageAsync(
        IReadOnlyList<string> htmlPages,
        Stream destination,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload that buffers the output and returns the bytes.
    /// Prefer the <see cref="Stream"/> overload for production paths.
    /// </summary>
    Task<(byte[] Bytes, PdfRenderResult Metadata)> RenderToBytesAsync(
        string html,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default);
}
