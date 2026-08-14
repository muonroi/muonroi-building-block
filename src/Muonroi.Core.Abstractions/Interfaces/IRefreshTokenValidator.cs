namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Validates a refresh token from the HTTP context.
/// </summary>
public interface IRefreshTokenValidator
{
    /// <summary>
    /// Validates the refresh token asynchronously.
    /// </summary>
    /// <param name="httpContext">The current HTTP context containing the refresh token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the authentication info context if valid; otherwise, null.</returns>
    Task<MAuthenticateInfoContext?> ValidateAsync(HttpContext httpContext);
}
