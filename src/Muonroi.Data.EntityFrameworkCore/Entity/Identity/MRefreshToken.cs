namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MRefreshTokens")]
public class MRefreshToken : MEntity
{
    public string Token { get; set; } = string.Empty;

    public string TokenValidityKey { get; set; } = string.Empty;

    public DateTime? ExpiredDate { get; set; }

    public DateTime? RevokedDate { get; set; }

    public DateTime LastUsedDate { get; set; }

    public string ReasonRevoked { get; set; } = string.Empty;

    public bool IsRevoked { get; set; }
}
