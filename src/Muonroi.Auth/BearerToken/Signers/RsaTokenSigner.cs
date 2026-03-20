namespace Muonroi.Auth.BearerToken.Signers;

/// <summary>
/// Token signer using RSA SHA-256 keys.
/// </summary>
public class RsaTokenSigner(RSA rsa) : ITokenSigner
{
    /// <inheritdoc/>
    public SigningCredentials GetCredentials()
    {
        SecurityKey key = new RsaSecurityKey(rsa);
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }
}
