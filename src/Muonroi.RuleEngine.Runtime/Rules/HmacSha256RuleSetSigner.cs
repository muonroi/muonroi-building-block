namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Signs ruleset artifacts using HMAC-SHA256.
/// </summary>
public sealed class HmacSha256RuleSetSigner(byte[] key) : IRuleSetSigner
{
    /// <summary>Computes a signature for the provided ruleset content.</summary>
    /// <param name="content">Ruleset content.</param>
    /// <returns>Base64-encoded signature.</returns>
    public string Sign(string content)
    {
        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    /// <summary>Verifies a signature for the provided ruleset content.</summary>
    /// <param name="content">Ruleset content.</param>
    /// <param name="signature">Base64-encoded signature to verify.</param>
    /// <returns><c>true</c> when the signature matches.</returns>
    public bool Verify(string content, string signature)
    {
        try
        {
            string expected = Sign(content);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(signature),
                Convert.FromBase64String(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
