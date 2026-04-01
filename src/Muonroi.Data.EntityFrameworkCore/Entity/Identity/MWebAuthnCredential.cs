using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Stores WebAuthn credential metadata for a user.
/// </summary>
[Table("MWebAuthnCredentials")]
public class MWebAuthnCredential : MEntity, ITenantScoped
{
    /// <summary>
    /// Gets or sets the unique identifier of the tenant this object belongs to.
    /// </summary>
    [StringLength(128)]
    public string? TenantId { get; set; }

    /// <summary>Gets or sets the user identifier.</summary>
    public Guid UserId { get; set; }
    /// <summary>Gets or sets the credential identifier bytes.</summary>
    public byte[] CredentialId { get; set; } = [];
    /// <summary>Gets or sets the public key bytes.</summary>
    public byte[] PublicKey { get; set; } = [];
    /// <summary>Gets or sets the signature counter.</summary>
    public uint SignCount { get; set; }
    /// <summary>Gets or sets the authenticator AAGUID.</summary>
    public Guid AaGuid { get; set; }

    /// <summary>Gets or sets the credential type.</summary>
    [Required]
    [StringLength(32)]
    public string CredType { get; set; } = "public-key";

    /// <summary>Gets or sets a value indicating whether the credential is backup-eligible.</summary>
    public bool IsBackupEligible { get; set; }
    /// <summary>Gets or sets a value indicating whether the credential has been backed up.</summary>
    public bool IsBackedUp { get; set; }

    /// <summary>Gets or sets the optional user handle.</summary>
    [StringLength(512)]
    public string? UserHandle { get; set; }

    /// <summary>Gets or sets the last usage time.</summary>
    public DateTime? LastUsedAt { get; set; }
}
