namespace Muonroi.Core.Abstractions.Models.Common.Responses.Login;

/// <summary>
/// Represents the response model for a token refresh operation.
/// </summary>
public sealed class RefreshTokenResponseModel
{
    /// <summary>
    /// Gets or sets the new access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new refresh token.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
