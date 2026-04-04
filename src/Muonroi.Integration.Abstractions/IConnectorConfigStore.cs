namespace Muonroi.Integration.Abstractions;

/// <summary>
/// Store for CRUD operations on connector configurations.
/// </summary>
public interface IConnectorConfigStore
{
    /// <summary>Gets a connector configuration by id.</summary>
    /// <param name="id">Configuration identifier.</param>
    /// <param name="tenantId">Tenant identifier, or null for global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ConnectorConfigDto?> GetByIdAsync(string id, string? tenantId, CancellationToken ct);
    /// <summary>Lists connector configurations for a tenant.</summary>
    /// <param name="tenantId">Tenant identifier, or null for global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<ConnectorConfigDto>> ListAsync(string? tenantId, CancellationToken ct);
    /// <summary>Saves a connector configuration.</summary>
    /// <param name="config">Configuration to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ConnectorConfigDto> SaveAsync(ConnectorConfigDto config, CancellationToken ct);
    /// <summary>Deletes a connector configuration by id.</summary>
    /// <param name="id">Configuration identifier.</param>
    /// <param name="tenantId">Tenant identifier, or null for global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string id, string? tenantId, CancellationToken ct);
}

/// <summary>
/// DTO for connector configuration.
/// </summary>
public sealed class ConnectorConfigDto
{
    /// <summary>Configuration identifier.</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>Tenant identifier, if tenant-scoped.</summary>
    public string? TenantId { get; init; }
    /// <summary>Connector type key.</summary>
    public required string ConnectorType { get; init; }
    /// <summary>Friendly name for the connector configuration.</summary>
    public required string Name { get; init; }
    /// <summary>Connector configuration payload as JSON.</summary>
    public string? ConfigJson { get; init; }
    /// <summary>Referenced credential id, if any.</summary>
    public string? CredentialId { get; init; }
    /// <summary>Current status of the configuration.</summary>
    public string Status { get; init; } = "active";
    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; init; }
}
