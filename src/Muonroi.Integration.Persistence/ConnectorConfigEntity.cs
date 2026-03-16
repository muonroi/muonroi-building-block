namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF entity for a connector configuration instance.
/// </summary>
public sealed class ConnectorConfigEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? TenantId { get; set; }
    public string ConnectorType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ConfigJson { get; set; }
    public string? CredentialId { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
