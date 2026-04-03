using Muonroi.Core.Helpers;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Represents a user entity in the system.
/// </summary>
[Table("MUsers")]
public class MUser : MEntity
{
    /// <summary>
    /// The maximum length allowed for the concurrency stamp.
    /// </summary>
    public const int MaxConcurrencyStampLength = 128;

    private string _userName = string.Empty;

    /// <summary>
    /// Gets or sets the user's name.
    /// </summary>
    [Required]
    [StringLength(MaxUserNameLength)]
    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            NormalizedUserName = _userName.ToUpperInvariant();
        }
    }

    private string _emailAddress = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    [Required]
    [StringLength(MaxEmailAddressLength)]
    public string EmailAddress
    {
        get => _emailAddress;
        set
        {
            _emailAddress = value;
            NormalizedEmailAddress = _emailAddress.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Gets or sets the user's first name.
    /// </summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's surname.
    /// </summary>
    [Required]
    [StringLength(MaxSurnameLength)]
    public string Surname { get; set; } = string.Empty;

    /// <summary>
    /// Gets the user's full name.
    /// </summary>
    [NotMapped] public string FullName => Name + " " + Surname;

    /// <summary>
    /// Gets or sets the user's hashed password.
    /// </summary>
    [Required]
    [StringLength(MaxPasswordLength)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password reset code.
    /// </summary>
    public string? PasswordResetCode { get; set; }

    /// <summary>
    /// Gets or sets the user's phone number.
    /// </summary>
    [StringLength(MaxPhoneNumberLength)] public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is confirmed.
    /// </summary>
    public bool IsPhoneNumberConfirmed { get; set; }

    /// <summary>
    /// Gets or sets a random value that must change whenever a user's credentials change (password changed, login removed).
    /// </summary>
    [StringLength(MaxSecurityStampLength)]
    public string? SecurityStamp { get; set; } = MSequentialGuidGenerator.Instance.Create().ToString();

    /// <summary>
    /// Gets or sets a value indicating whether two-factor authentication is enabled for this user.
    /// </summary>
    public bool IsTwoFactorEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the email address is confirmed.
    /// </summary>
    public bool IsEmailConfirmed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets the normalized user name.
    /// </summary>
    [Required]
    [StringLength(MaxUserNameLength)]
    public string NormalizedUserName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized email address.
    /// </summary>
    [Required]
    [StringLength(MaxEmailAddressLength)]
    public string NormalizedEmailAddress { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the concurrency stamp for optimistic concurrency.
    /// </summary>
    [StringLength(MaxConcurrencyStampLength)]
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the salt used for password hashing.
    /// </summary>
    public string? Salt { get; set; }

    /// <summary>
    /// Gets or sets the profile picture ID.
    /// </summary>
    public int? ProfilePictureId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user should change their password on next login.
    /// </summary>
    public bool ShouldChangePasswordOnNextLogin { get; set; }

    /// <summary>
    /// Gets or sets the sign-in token expiration time in UTC.
    /// </summary>
    public DateTime? SignInTokenExpireTimeUtc { get; set; }

    /// <summary>
    /// Sets a new password reset code for the user.
    /// </summary>
    public void SetNewPasswordResetCode()
    {
        PasswordResetCode = Guid.NewGuid().ToString("N").Truncate(10)?.ToUpperInvariant();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user is using third-party login.
    /// </summary>
    public bool IsUseThirdPartyLogin { get; set; }

    /// <summary>
    /// Gets or sets the external login provider.
    /// </summary>
    public string? ExternalLoginProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external login token.
    /// </summary>
    public string? ExternalLoginToken { get; set; } = string.Empty;
}
