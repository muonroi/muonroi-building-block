namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Abstracts WebAuthn credential persistence — decouples Auth from EF Core.
/// Implement this interface in a persistence package (e.g., Data.EntityFrameworkCore)
/// to provide the backing store for WebAuthn credential operations.
/// </summary>
public interface IWebAuthnCredentialStore
{
    /// <summary>
    /// Gets credential IDs for a user in a tenant (used for exclude/allow lists).
    /// </summary>
    Task<List<byte[]>> GetCredentialIdsByUserAsync(string? tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all credentials for a user in a tenant (full data for assertion verification).
    /// </summary>
    Task<List<WebAuthnCredentialInfo>> GetCredentialsByUserAsync(string? tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all credential IDs in a tenant (for uniqueness checks).
    /// </summary>
    Task<List<byte[]>> GetAllCredentialIdsAsync(string? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Saves a new credential.
    /// </summary>
    Task SaveCredentialAsync(WebAuthnCredentialInfo credential, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing credential's mutable fields (SignCount, IsBackedUp, LastUsedAt, UserHandle).
    /// </summary>
    Task UpdateCredentialAsync(WebAuthnCredentialInfo credential, CancellationToken ct = default);
}

/// <summary>
/// Portable credential data transfer object — no EF dependency.
/// </summary>
public sealed class WebAuthnCredentialInfo
{
    /// <summary>Tenant identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>User identifier.</summary>
    public Guid UserId { get; set; }

    /// <summary>Credential identifier bytes.</summary>
    public byte[] CredentialId { get; set; } = [];

    /// <summary>Public key bytes.</summary>
    public byte[] PublicKey { get; set; } = [];

    /// <summary>Signature counter.</summary>
    public uint SignCount { get; set; }

    /// <summary>Authenticator AAGUID.</summary>
    public Guid AaGuid { get; set; }

    /// <summary>Credential type.</summary>
    public string CredType { get; set; } = "public-key";

    /// <summary>Whether credential is backup-eligible.</summary>
    public bool IsBackupEligible { get; set; }

    /// <summary>Whether credential has been backed up.</summary>
    public bool IsBackedUp { get; set; }

    /// <summary>Optional user handle.</summary>
    public string? UserHandle { get; set; }

    /// <summary>Last usage time.</summary>
    public DateTime? LastUsedAt { get; set; }
}
