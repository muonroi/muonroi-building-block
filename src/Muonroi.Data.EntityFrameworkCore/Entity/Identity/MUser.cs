namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

[Table("MUsers")]
public class MUser : MEntity
{
    public const int MaxConcurrencyStampLength = 128;

    private string _userName = string.Empty;

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

    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(MaxSurnameLength)]
    public string Surname { get; set; } = string.Empty;

    [NotMapped] public string FullName => Name + " " + Surname;

    [Required]
    [StringLength(MaxPasswordLength)]
    public string Password { get; set; } = string.Empty;

    public string? PasswordResetCode { get; set; }

    [StringLength(MaxPhoneNumberLength)] public string PhoneNumber { get; set; } = string.Empty;

    public bool IsPhoneNumberConfirmed { get; set; }

    [StringLength(MaxSecurityStampLength)]
    public string? SecurityStamp { get; set; } = MSequentialGuidGenerator.Instance.Create().ToString();

    public bool IsTwoFactorEnabled { get; set; } = false;

    public bool IsEmailConfirmed { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [StringLength(MaxUserNameLength)]
    public string NormalizedUserName { get; private set; } = string.Empty;

    [Required]
    [StringLength(MaxEmailAddressLength)]
    public string NormalizedEmailAddress { get; private set; } = string.Empty;

    [StringLength(MaxConcurrencyStampLength)]
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();

    public string? Salt { get; set; }

    public int? ProfilePictureId { get; set; }

    public bool ShouldChangePasswordOnNextLogin { get; set; }
    public DateTime? SignInTokenExpireTimeUtc { get; set; }

    public void SetNewPasswordResetCode()
    {
        PasswordResetCode = Guid.NewGuid().ToString("N").Truncate(10)?.ToUpperInvariant();
    }

    public bool IsUseThirdPartyLogin { get; set; }
    public string? ExternalLoginProvider { get; set; } = string.Empty;
    public string? ExternalLoginToken { get; set; } = string.Empty;
}
