namespace Muonroi.Auth.Tests;

public class TokenBindingTests
{
    private static (JsonWebKey jwk, RsaSecurityKey key) CreateJwk()
    {
        RSA rsa = RSA.Create(2048);
        RsaSecurityKey key = new(rsa);
        JsonWebKey jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        return (jwk, key);
    }

    private static string CreateProofJwt(RsaSecurityKey key, JsonWebKey jwk, string htm, string htu, string jti)
    {
        JwtHeader header = new(new SigningCredentials(key, SecurityAlgorithms.RsaSha256))
        {
            ["typ"] = "dpop+jwt"
        };
        string jwkJson = JsonSerializer.Serialize(new { jwk.Kty, jwk.E, jwk.N, jwk.Crv, jwk.X, jwk.Y });
        header["jwk"] = JsonSerializer.Deserialize<JsonElement>(jwkJson);
        JwtPayload payload = new()
        {
            { "htu", htu },
            { "htm", htm },
            { "jti", jti },
            { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    [Fact]
    public void ValidateProof_Succeeds_WithMatchingJkt()
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        byte[] secret = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        string access = DPoPBindingService.CreateAccessToken(jwk, new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256));
        JwtSecurityToken accessToken = new JwtSecurityTokenHandler().ReadJwtToken(access);
        string? expectedJkt = ((JsonElement)accessToken.Payload["cnf"]).GetProperty("jkt").GetString();
        string proof = CreateProofJwt(key, jwk, "GET", "https://api.example.com", Guid.NewGuid().ToString());

        bool result = DPoPBindingService.ValidateProof(proof, "GET", "https://api.example.com", expectedJkt!);

        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateProof_Fails_WithMismatchedJkt()
    {
        (JsonWebKey jwk1, _) = CreateJwk();
        (JsonWebKey jwk2, RsaSecurityKey key2) = CreateJwk();
        byte[] secret = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        string access = DPoPBindingService.CreateAccessToken(jwk1, new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256));
        JwtSecurityToken accessToken = new JwtSecurityTokenHandler().ReadJwtToken(access);
        string? expectedJkt = ((JsonElement)accessToken.Payload["cnf"]).GetProperty("jkt").GetString();
        string proof = CreateProofJwt(key2, jwk2, "GET", "https://api.example.com", Guid.NewGuid().ToString());

        bool result = DPoPBindingService.ValidateProof(proof, "GET", "https://api.example.com", expectedJkt!);

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateProof_Fails_OnReplay()
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);
        string proof = CreateProofJwt(key, jwk, "GET", "https://api.example.com", Guid.NewGuid().ToString());

        bool first = DPoPBindingService.ValidateProof(proof, "GET", "https://api.example.com", expectedJkt);
        bool second = DPoPBindingService.ValidateProof(proof, "GET", "https://api.example.com", expectedJkt);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }
}
