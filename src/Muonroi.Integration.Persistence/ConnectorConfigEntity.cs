namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF entity for a connector configuration instance.
/// </summary>
public sealed class ConnectorConfigEntity
{
    /// <summary>Configuration identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Tenant identifier, if tenant-scoped.</summary>
    public string? TenantId { get; set; }
    /// <summary>Connector type key.</summary>
    public string ConnectorType { get; set; } = string.Empty;
    /// <summary>Friendly name of the configuration.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Connector configuration payload as JSON.</summary>
    public string? ConfigJson { get; set; }
    /// <summary>Referenced credential id, if any.</summary>
    public string? CredentialId { get; set; }
    /// <summary>Configuration version number.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Current status of the configuration.</summary>
    public string Status { get; set; } = "active";
    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>User ID of the BA who owns this connector. Null = legacy/admin-created.</summary>
    public string? OwnerId { get; set; }
}
