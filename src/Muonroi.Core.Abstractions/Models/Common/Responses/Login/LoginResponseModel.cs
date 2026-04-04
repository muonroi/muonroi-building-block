namespace Muonroi.Core.Abstractions.Models.Common.Responses.Login;

/// <summary>
/// Represents the response model for a login operation.
/// </summary>
public sealed class LoginResponseModel
{
    /// <summary>
    /// Gets or sets the username of the authenticated user.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token generated for the user.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the refresh token used to obtain new access tokens.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first name of the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the surname of the user.
    /// </summary>
    public string Surname { get; set; } = string.Empty;

    /// <summary>
    /// Gets the full name of the user.
    /// </summary>
    public string FullName => Name + " " + Surname;

    /// <summary>
    /// Gets or sets the phone number of the user.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is confirmed.
    /// </summary>
    public bool IsPhoneNumberConfirmed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the email address is confirmed.
    /// </summary>
    public bool IsEmailConfirmed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user used a third-party login.
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

    /// <summary>
    /// Gets or sets the list of permissions assigned to the user.
    /// </summary>
    public List<string> Permissions { get; set; } = [];
}
