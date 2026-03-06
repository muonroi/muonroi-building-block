namespace Muonroi.Bff;

/// <summary>
/// Stores refresh tokens on the server to avoid exposing them to the browser.
/// </summary>
public interface ITokenStore
{
    Task StoreRefreshTokenAsync(string subject, string refreshToken);
    Task<string?> GetRefreshTokenAsync(string subject);
    Task RemoveRefreshTokenAsync(string subject);
}
