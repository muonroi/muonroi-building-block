namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MWebAuthnCredentials")]
public class MWebAuthnCredential : MEntity
{
    public Guid UserId { get; set; }
    public byte[] CredentialId { get; set; } = [];
    public byte[] PublicKey { get; set; } = [];
    public uint SignCount { get; set; }
    public Guid AaGuid { get; set; }

    [Required]
    [StringLength(32)]
    public string CredType { get; set; } = "public-key";

    public bool IsBackupEligible { get; set; }
    public bool IsBackedUp { get; set; }

    [StringLength(512)]
    public string? UserHandle { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
