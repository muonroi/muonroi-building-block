namespace Muonroi.Core.Abstractions.Models.Common.Responses.Login;

public sealed class RefreshTokenResponseModel
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
