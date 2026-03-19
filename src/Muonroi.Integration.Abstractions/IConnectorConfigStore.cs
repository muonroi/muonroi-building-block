namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Store for CRUD operations on connector configurations.
/// </summary>
public interface IConnectorConfigStore
{
    Task<ConnectorConfigDto?> GetByIdAsync(string id, string? tenantId, CancellationToken ct);
    Task<IReadOnlyList<ConnectorConfigDto>> ListAsync(string? tenantId, CancellationToken ct);
    Task<ConnectorConfigDto> SaveAsync(ConnectorConfigDto config, CancellationToken ct);
    Task DeleteAsync(string id, string? tenantId, CancellationToken ct);
}

/// <summary>
/// DTO for connector configuration.
/// </summary>
public sealed class ConnectorConfigDto
{
    public string Id { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public required string ConnectorType { get; init; }
    public required string Name { get; init; }
    public string? ConfigJson { get; init; }
    public string? CredentialId { get; init; }
    public string Status { get; init; } = "active";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
