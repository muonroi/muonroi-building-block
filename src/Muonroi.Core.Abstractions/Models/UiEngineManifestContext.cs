namespace Muonroi.Core.Abstractions.Models;

public sealed class UiEngineManifestContext
{
    public required MUiEngineManifest Manifest { get; init; }
    public string TenantTier { get; init; } = "Free";
    public string? TenantId { get; init; }
    public Guid UserId { get; init; }
    public IReadOnlyList<string> UserPermissions { get; init; } = [];
    public required IServiceProvider Services { get; init; }
}
