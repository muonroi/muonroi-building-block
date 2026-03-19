namespace Muonroi.Integration.Persistence;

/// <summary>
/// EF entity for encrypted connector credentials.
/// </summary>
public sealed class ConnectorCredentialEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// AES-256-GCM encrypted JSON of credential key-value pairs.
    /// </summary>
    public string EncryptedValues { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
