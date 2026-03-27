namespace Muonroi.Bff;

/// <summary>
/// Stores refresh tokens on the server to avoid exposing them to the browser.
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Stores a refresh token for the specified subject.
    /// </summary>
    /// <param name="subject">The subject identifier (e.g., user ID).</param>
    /// <param name="refreshToken">The refresh token to store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StoreRefreshTokenAsync(string subject, string refreshToken);

    /// <summary>
    /// Retrieves the refresh token for the specified subject.
    /// </summary>
    /// <param name="subject">The subject identifier (e.g., user ID).</param>
    /// <returns>The stored refresh token, or null if not found.</returns>
    Task<string?> GetRefreshTokenAsync(string subject);

    /// <summary>
    /// Removes the refresh token for the specified subject.
    /// </summary>
    /// <param name="subject">The subject identifier (e.g., user ID).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveRefreshTokenAsync(string subject);
}
