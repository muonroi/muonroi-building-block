namespace Muonroi.Core.Abstractions.Models.Common.Responses.Login;

public sealed class LoginResponseModel
{
    public string Username { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string FullName => Name + " " + Surname;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsPhoneNumberConfirmed { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public bool IsActive { get; set; }
    public bool IsUseThirdPartyLogin { get; set; }
    public string? ExternalLoginProvider { get; set; } = string.Empty;
    public string? ExternalLoginToken { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
}
