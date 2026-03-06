namespace Muonroi.Core.Abstractions.Models.Common.Requests.Registers;

public class RegisterRequestModel
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public bool IsUseThirdPartyLogin { get; set; }
    public string? ExternalLoginProvider { get; set; } = string.Empty;
    public string? ExternalLoginToken { get; set; } = string.Empty;
}
