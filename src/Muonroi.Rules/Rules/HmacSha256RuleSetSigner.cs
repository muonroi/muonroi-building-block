using System.Security.Cryptography;
using System.Text;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Signs ruleset artifacts using HMAC-SHA256.
/// </summary>
public sealed class HmacSha256RuleSetSigner(byte[] key) : IRuleSetSigner
{
    public string Sign(string content)
    {
        using HMACSHA256 hmac = new(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

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
