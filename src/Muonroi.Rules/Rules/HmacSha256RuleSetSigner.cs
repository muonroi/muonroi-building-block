using System.Security.Cryptography;
using System.Text;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Signs ruleset artifacts using HMAC-SHA256.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HmacSha256RuleSetSigner"/> class.
/// </remarks>
/// <param name="key">The key used for signing.</param>
public sealed class HmacSha256RuleSetSigner(byte[] key) : IRuleSetSigner
{
    /// <summary>
    /// Signs the specified content.
    /// </summary>
    /// <param name="content">The content to sign.</param>
    /// <returns>The Base64 encoded signature.</returns>
    public string Sign(string content)
    {
        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies the signature of the specified content.
    /// </summary>
    /// <param name="content">The content to verify.</param>
    /// <param name="signature">The Base64 encoded signature to check.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
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
