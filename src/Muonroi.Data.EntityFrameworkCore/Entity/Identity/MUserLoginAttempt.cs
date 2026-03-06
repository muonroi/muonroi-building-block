namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MUserLoginAttempts")]
public sealed class MUserLoginAttempt : MEntity
{
    public const int MaxUserNameOrEmailAddressLength = MaxEmailAddressLength;

    /// <summary>
    /// Maximum length of <see cref="ClientIpAddress"/> property.
    /// </summary>
    public const int MaxClientIpAddressLength = 64;

    /// <summary>
    /// Maximum length of <see cref="ClientName"/> property.
    /// </summary>
    public const int MaxClientNameLength = 128;

    /// <summary>
    /// Maximum length of <see cref="BrowserInfo"/> property.
    /// </summary>
    public const int MaxBrowserInfoLength = 512;

    /// <summary>
    /// User's Id, if <see cref="UserNameOrEmailAddress"/> was a valid username or email address.
    /// </summary>
    public Guid UserGuid { get; set; }

    /// <summary>
    /// User name or email address
    /// </summary>
    [StringLength(MaxUserNameOrEmailAddressLength)]
    public string UserNameOrEmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the client.
    /// </summary>
    [StringLength(MaxClientIpAddressLength)]
    public string ClientIpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Name (generally computer name) of the client.
    /// </summary>
    [StringLength(MaxClientNameLength)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Browser information if this method is called in a web request.
    /// </summary>
    [StringLength(MaxBrowserInfoLength)]
    public string BrowserInfo { get; set; } = string.Empty;

    /// <summary>
    /// Login attempt result.
    /// </summary>
    public MLoginResultType Result { get; set; }

    /// <summary>
    /// Login attempt time.
    /// </summary>
    public int AttemptTime { get; set; }

    /// <summary>
    /// Lockout end date.
    /// </summary>
    public DateTime LockTo { get; set; }
}
