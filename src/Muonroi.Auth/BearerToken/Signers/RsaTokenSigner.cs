namespace Muonroi.Auth.BearerToken.Signers;

public class RsaTokenSigner(RSA rsa) : ITokenSigner
{
    public SigningCredentials GetCredentials()
    {
        SecurityKey key = new RsaSecurityKey(rsa);
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }
}
