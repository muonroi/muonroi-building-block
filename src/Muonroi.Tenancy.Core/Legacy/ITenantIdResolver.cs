

namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Resolves a tenant identifier from the current HTTP context.
/// </summary>
public interface ITenantIdResolver
{
    /// <summary>
    /// Resolves the tenant identifier from the specified HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The resolved tenant identifier, or <c>null</c> if not found.</returns>
    Task<string?> ResolveTenantIdAsync(HttpContext context);
}
