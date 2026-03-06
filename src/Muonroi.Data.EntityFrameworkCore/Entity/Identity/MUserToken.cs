namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MUserTokens")]
public sealed class MUserToken : MEntity
{
    /// <summary>
    /// Maximum length of the <see cref="LoginProvider"/> property.
    /// </summary>
    public const int MaxLoginProviderLength = 128;

    public long UserId { get; set; }

    [StringLength(MaxLoginProviderLength)] public string LoginProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the token.
    /// </summary>
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime? ExpireDate { get; set; }

    public MUserToken()
    {
    }

    public MUserToken(long userId, string loginProvider, string name, string value, DateTime? expireDate = null)
    {
        UserId = userId;
        LoginProvider = loginProvider;
        Name = name;
        Value = value;
        ExpireDate = expireDate;
    }
}
