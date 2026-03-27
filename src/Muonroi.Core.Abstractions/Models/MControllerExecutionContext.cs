namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Execution context for a controller.
/// </summary>
public sealed class MControllerExecutionContext
{
    /// <summary>
    /// The user ID.
    /// </summary>
    public Guid? UserId { get; init; }
    /// <summary>
    /// The username.
    /// </summary>
    public string? Username { get; init; }
    /// <summary>
    /// The tenant ID.
    /// </summary>
    public string? TenantId { get; init; }
    /// <summary>
    /// The tenant tier.
    /// </summary>
    public string? TenantTier { get; init; }
    /// <summary>
    /// The actor.
    /// </summary>
    public string? Actor { get; init; }
    /// <summary>
    /// Whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }
    /// <summary>
    /// The user's permissions.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
