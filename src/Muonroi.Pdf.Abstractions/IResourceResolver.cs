namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Resolves external resource references (<c>&lt;img src&gt;</c>, <c>background-image: url(...)</c>) to bytes.
/// </summary>
/// <remarks>
/// Bytes-only contract — the engine never receives a path. This blocks
/// <c>url(file:///etc/passwd)</c> style escapes.
/// Built-in implementations refuse non-data: schemes by default; opt in to file:// or other
/// schemes via the policy.
/// </remarks>
public interface IResourceResolver
{
    /// <summary>
    /// Attempts to fetch an external resource. Implementations MUST honor the policy's
    /// <c>AllowedSchemes</c>, <c>MaxEmbeddedResourceBytes</c>, and <c>AllowedFileRoots</c>.
    /// </summary>
    /// <param name="uri">Resource URI (data:, file:, or scheme allowed by policy).</param>
    /// <param name="contentTypeHint">Optional MIME hint (e.g. <c>image/png</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bytes and resolved content type, or null if forbidden / not found.</returns>
    ValueTask<ResourceResult?> ResolveAsync(Uri uri, string? contentTypeHint = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bytes and content metadata returned by an <see cref="IResourceResolver"/>.
/// </summary>
public sealed record ResourceResult(ReadOnlyMemory<byte> Bytes, string ContentType);
