namespace Muonroi.Auth.Jwt;

/// <summary>
/// Interface for a store that manages revoked JSON Web Tokens (JWTs).
/// </summary>
public interface ITokenRevocationStore
{
    /// <summary>
    /// Revokes a JWT identifier until its expiration time.
    /// </summary>
    /// <param name="jti">The unique JWT identifier to revoke.</param>
    /// <param name="expires">The date and time when the token expires.</param>
    void Revoke(string jti, DateTime expires);

    /// <summary>
    /// Checks if a JWT identifier has been revoked.
    /// </summary>
    /// <param name="jti">The unique JWT identifier to check.</param>
    /// <returns>True if the token is revoked; otherwise, false.</returns>
    bool IsRevoked(string jti);
}
