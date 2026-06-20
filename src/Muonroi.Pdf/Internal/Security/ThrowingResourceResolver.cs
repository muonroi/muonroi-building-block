using System.Diagnostics.CodeAnalysis;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Exceptions;

namespace Muonroi.Pdf.Internal.Security;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfSecurityException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy.")]

/// <summary>
/// Safe-by-default <see cref="IResourceResolver"/>. Blocks disallowed URI schemes
/// (file://, javascript:) by throwing <see cref="PdfSecurityException"/>, and returns
/// null for all other schemes (resource not fetched). Callers that need external
/// resolution supply their own resolver via <c>PdfRenderOptions.ResourceResolver</c>.
/// </summary>
internal sealed class ThrowingResourceResolver : IResourceResolver
{
    public ValueTask<ResourceResult?> ResolveAsync(
        Uri uri,
        string? contentTypeHint = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
            throw new PdfSecurityException("SEC-06", $"file:// URI scheme is not permitted: {uri}");

        if (string.Equals(uri.Scheme, "javascript", StringComparison.OrdinalIgnoreCase))
            throw new PdfSecurityException("SEC-06", $"javascript: URI scheme is not permitted: {uri}");

        // All other schemes (http, https, ftp, data, ...) are simply unavailable by default.
        return new ValueTask<ResourceResult?>((ResourceResult?)null);
    }
}
