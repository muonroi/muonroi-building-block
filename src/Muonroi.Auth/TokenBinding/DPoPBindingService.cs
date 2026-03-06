namespace Muonroi.Auth.TokenBinding;

public class DPoPBindingService
{
    private static readonly ConcurrentDictionary<string, bool> ReplayCache = new();

    public static string CreateAccessToken(JsonWebKey jwk, SigningCredentials credentials)
    {
        string jkt = ComputeJkt(jwk);
        JwtSecurityTokenHandler handler = new();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity(),
            Expires = DateTime.UtcNow.AddMinutes(5), // MBB001-exempt: static-class boundary — cannot inject IMDateTimeService
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["cnf"] = new Dictionary<string, string> { ["jkt"] = jkt }
            }
        };
        JwtSecurityToken token = handler.CreateJwtSecurityToken(descriptor);
        return handler.WriteToken(token);
    }

    public static bool ValidateProof(string proofJwt, string httpMethod, string httpUri, string expectedJkt)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken token = handler.ReadJwtToken(proofJwt);
        if (!token.Header.TryGetValue("jwk", out object? jwkObj))
        {
            return false;
        }

        JsonWebKey jwk = new(jwkObj.ToString());
        string jkt = ComputeJkt(jwk);
        if (!string.Equals(jkt, expectedJkt, StringComparison.Ordinal))
        {
            return false;
        }

        string? htm = token.Payload["htm"]?.ToString();
        string? htu = token.Payload["htu"]?.ToString();
        if (!string.Equals(htm, httpMethod, StringComparison.OrdinalIgnoreCase) || htu != httpUri)
        {
            return false;
        }

        string? jti = token.Payload["jti"]?.ToString();
        return !string.IsNullOrEmpty(jti) && ReplayCache.TryAdd(jti, true);
    }

    public static string ComputeJkt(JsonWebKey jwk)
    {
        SortedDictionary<string, object?> dict = new()
        {
            ["crv"] = jwk.Crv,
            ["e"] = jwk.E,
            ["kty"] = jwk.Kty,
            ["n"] = jwk.N,
            ["x"] = jwk.X,
            ["y"] = jwk.Y
        };
        string json = JsonSerializer.Serialize(dict); // MBB002-exempt: static-class boundary — cannot inject IMJsonSerializeService
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Base64UrlEncoder.Encode(hash);
    }
}