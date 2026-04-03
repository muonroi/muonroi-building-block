namespace Muonroi.Auth.BearerToken.Signers;

/// <summary>
/// Token signer using HMAC SHA-256 symmetric keys.
/// </summary>
public class HmacTokenSigner(string signingKey) : ITokenSigner
{
    /// <inheritdoc/>
    public SigningCredentials GetCredentials()
    {
        SecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }
}
