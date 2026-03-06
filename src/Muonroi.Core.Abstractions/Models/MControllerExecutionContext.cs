namespace Muonroi.Core.Abstractions.Models;

public sealed class MControllerExecutionContext
{
    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public string? TenantId { get; init; }
    public string? TenantTier { get; init; }
    public string? Actor { get; init; }
    public bool IsAuthenticated { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
