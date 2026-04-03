namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF entity for encrypted connector credentials.
/// </summary>
public sealed class ConnectorCredentialEntity
{
    /// <summary>Credential identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Tenant identifier, if tenant-scoped.</summary>
    public string? TenantId { get; set; }
    /// <summary>Display name for the credential.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// AES-256-GCM encrypted JSON of credential key-value pairs.
    /// </summary>
    public string EncryptedValues { get; set; } = string.Empty;
    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
