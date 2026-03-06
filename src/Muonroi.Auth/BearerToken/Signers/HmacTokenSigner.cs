namespace Muonroi.Auth.BearerToken.Signers;

public class HmacTokenSigner(string signingKey) : ITokenSigner
{
    public SigningCredentials GetCredentials()
    {
        SecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }
}
