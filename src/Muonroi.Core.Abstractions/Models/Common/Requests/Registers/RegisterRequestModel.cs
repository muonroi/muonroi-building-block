namespace Muonroi.Core.Abstractions.Models.Common.Requests.Registers;

/// <summary>
/// Represents the request model for user registration.
/// </summary>
public class RegisterRequestModel
{
    /// <summary>
    /// Gets or sets the username for the new account.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for the new account.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address for the new account.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the phone number for the user.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's first name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's surname.
    /// </summary>
    public string Surname { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether two-factor authentication is enabled.
    /// </summary>
    public bool IsTwoFactorEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is registering via a third-party login.
    /// </summary>
    public bool IsUseThirdPartyLogin { get; set; }

    /// <summary>
    /// Gets or sets the name of the external login provider, if any.
    /// </summary>
    public string? ExternalLoginProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external login token, if any.
    /// </summary>
    public string? ExternalLoginToken { get; set; } = string.Empty;
}
