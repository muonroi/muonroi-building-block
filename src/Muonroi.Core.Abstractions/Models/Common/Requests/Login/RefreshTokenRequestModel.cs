namespace Muonroi.Core.Abstractions.Models.Common.Requests.Login;

/// <summary>
/// Refresh token request model.
/// Claims are regenerated server-side based on current user permissions from database.
/// Client-provided claims have been removed for security reasons (privilege escalation risk).
/// </summary>
public sealed class RefreshTokenRequestModel
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
