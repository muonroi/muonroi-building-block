namespace Muonroi.Auth.Jwt;

public interface ITokenRevocationStore
{
    void Revoke(string jti, DateTime expires);
    bool IsRevoked(string jti);
}
