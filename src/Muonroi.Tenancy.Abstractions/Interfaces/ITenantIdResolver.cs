using Microsoft.AspNetCore.Http;

namespace Muonroi.Tenancy.Abstractions.Interfaces;

/// <summary>
/// Defines the contract for resolving the tenant identifier from the current HTTP context.
/// </summary>
public interface ITenantIdResolver
{
    /// <summary>
    /// Resolves the tenant identifier from the specified HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the resolved tenant identifier, or null if it cannot be resolved.</returns>
    Task<string?> ResolveTenantIdAsync(HttpContext context);
}
