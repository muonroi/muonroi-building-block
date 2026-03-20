namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Stores authentication tokens issued to a user.
/// </summary>
[Table("MUserTokens")]
public sealed class MUserToken : MEntity
{
    /// <summary>
    /// Maximum length of the <see cref="LoginProvider"/> property.
    /// </summary>
    public const int MaxLoginProviderLength = 128;

    /// <summary>Gets or sets the user identifier associated with the token.</summary>
    public long UserId { get; set; }

    /// <summary>Gets or sets the login provider that issued the token.</summary>
    [StringLength(MaxLoginProviderLength)] public string LoginProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the token.
    /// </summary>
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the token value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional expiration time of the token.</summary>
    public DateTime? ExpireDate { get; set; }

    /// <summary>Initializes a new instance of the <see cref="MUserToken"/> class.</summary>
    public MUserToken()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MUserToken"/> class.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="loginProvider">The login provider name.</param>
    /// <param name="name">The token name.</param>
    /// <param name="value">The token value.</param>
    /// <param name="expireDate">The expiration time, if any.</param>
    public MUserToken(long userId, string loginProvider, string name, string value, DateTime? expireDate = null)
    {
        UserId = userId;
        LoginProvider = loginProvider;
        Name = name;
        Value = value;
        ExpireDate = expireDate;
    }
}
