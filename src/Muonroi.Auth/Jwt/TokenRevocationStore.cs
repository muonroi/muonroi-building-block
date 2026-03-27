namespace Muonroi.Auth.Jwt;

/// <summary>
/// An in-memory implementation of the ITokenRevocationStore.
/// </summary>
/// <param name="dateTimeService">Service to retrieve the current date and time.</param>
public class TokenRevocationStore(IMDateTimeService dateTimeService) : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new();

    /// <summary>
    /// Revokes a JWT identifier until its expiration time.
    /// </summary>
    /// <param name="jti">The unique JWT identifier to revoke.</param>
    /// <param name="expires">The date and time when the token expires.</param>
    public void Revoke(string jti, DateTime expires)
    {
        _revoked[jti] = expires;
    }

    /// <summary>
    /// Checks if a JWT identifier has been revoked.
    /// </summary>
    /// <param name="jti">The unique JWT identifier to check.</param>
    /// <returns>True if the token is revoked and not expired; otherwise, false.</returns>
    public bool IsRevoked(string jti)
    {
        if (!_revoked.TryGetValue(jti, out DateTime exp))
        {
            return false;
        }

        if (exp > dateTimeService.UtcNow())
        {
            return true;
        }

        _revoked.TryRemove(jti, out _);

        return false;
    }
}
