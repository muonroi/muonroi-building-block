namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// Defines methods for resolving context-related information such as tenant ID, user ID, and username.
/// </summary>
public interface IContextResolver
{
    /// <summary>
    /// Resolves the current tenant identifier.
    /// </summary>
    /// <returns>The tenant identifier, or null if it cannot be resolved.</returns>
    string? ResolveTenantId();

    /// <summary>
    /// Resolves the current user identifier.
    /// </summary>
    /// <returns>The user identifier, or null if it cannot be resolved.</returns>
    string? ResolveUserId();

    /// <summary>
    /// Resolves the current username.
    /// </summary>
    /// <returns>The username, or null if it cannot be resolved.</returns>
    string? ResolveUsername();
}

/// <summary>
/// A null implementation of <see cref="IContextResolver"/> that always returns null.
/// </summary>
public sealed class NullContextResolver : IContextResolver
{
    /// <summary>
    /// Resolves the current tenant identifier. Always returns null.
    /// </summary>
    /// <returns>Null.</returns>
    public string? ResolveTenantId() => null;

    /// <summary>
    /// Resolves the current user identifier. Always returns null.
    /// </summary>
    /// <returns>Null.</returns>
    public string? ResolveUserId() => null;

    /// <summary>
    /// Resolves the current username. Always returns null.
    /// </summary>
    /// <returns>Null.</returns>
    public string? ResolveUsername() => null;
}
