namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Context for generating a UI engine manifest.
/// </summary>
public sealed class UiEngineManifestContext
{
    /// <summary>
    /// The UI engine manifest.
    /// </summary>
    public required MUiEngineManifest Manifest { get; init; }
    /// <summary>
    /// The tenant tier.
    /// </summary>
    public string TenantTier { get; init; } = "Free";
    /// <summary>
    /// The tenant ID.
    /// </summary>
    public string? TenantId { get; init; }
    /// <summary>
    /// The user ID.
    /// </summary>
    public Guid UserId { get; init; }
    /// <summary>
    /// The user's permissions.
    /// </summary>
    public IReadOnlyList<string> UserPermissions { get; init; } = [];
    /// <summary>
    /// The service provider.
    /// </summary>
    public required IServiceProvider Services { get; init; }
}
