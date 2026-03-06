namespace Muonroi.Auth.Jwt;

public class TokenRevocationStore(IMDateTimeService dateTimeService) : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new();

    public void Revoke(string jti, DateTime expires)
    {
        _revoked[jti] = expires;
    }

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
