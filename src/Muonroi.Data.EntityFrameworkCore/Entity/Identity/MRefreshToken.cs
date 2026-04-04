namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Stores refresh token details for a user session.
/// </summary>
[Table("MRefreshTokens")]
public class MRefreshToken : MEntity
{
    /// <summary>Gets or sets the refresh token value.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets the validity key used to correlate access tokens.</summary>
    public string TokenValidityKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the expiration time of the refresh token.</summary>
    public DateTime? ExpiredDate { get; set; }

    /// <summary>Gets or sets the time the token was revoked.</summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>Gets or sets the last time the token was used.</summary>
    public DateTime LastUsedDate { get; set; }

    /// <summary>Gets or sets the reason the token was revoked.</summary>
    public string ReasonRevoked { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the token is revoked.</summary>
    public bool IsRevoked { get; set; }
}
