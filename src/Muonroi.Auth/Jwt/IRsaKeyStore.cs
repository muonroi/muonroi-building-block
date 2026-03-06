namespace Muonroi.Auth.Jwt;

public interface IRsaKeyStore
{
    SigningCredentials GetCurrentSigningCredentials();
    void RotateKeys();
    SecurityKey? GetKey(string kid);
    JsonWebKeySet GetJsonWebKeySet();
}
