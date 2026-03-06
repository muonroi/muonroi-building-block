namespace Muonroi.Auth.Tests;

public class TokenBindingTheoryTests
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

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void ValidateProof_DifferentHttpMethods_ValidatesCorrectly(string httpMethod)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string proof = CreateProofJwt(key, jwk, httpMethod, "https://api.example.com", Guid.NewGuid().ToString());
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        DPoPBindingService.ValidateProof(proof, httpMethod, "https://api.example.com", expectedJkt).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://api.example.com")]
    [InlineData("https://api.example.com/resource")]
    [InlineData("https://api.example.com/v1/users")]
    [InlineData("https://example.com")]
    public void ValidateProof_DifferentUrls_ValidatesCorrectly(string url)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string proof = CreateProofJwt(key, jwk, "GET", url, Guid.NewGuid().ToString());
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        DPoPBindingService.ValidateProof(proof, "GET", url, expectedJkt).Should().BeTrue();
    }

    [Theory]
    [InlineData("GET", "POST")]
    [InlineData("POST", "GET")]
    [InlineData("PUT", "DELETE")]
    [InlineData("DELETE", "PATCH")]
    public void ValidateProof_MismatchedHttpMethod_FailsValidation(string proofMethod, string requestMethod)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string proof = CreateProofJwt(key, jwk, proofMethod, "https://api.example.com", Guid.NewGuid().ToString());
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        DPoPBindingService.ValidateProof(proof, requestMethod, "https://api.example.com", expectedJkt).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://api.example.com", "https://other.example.com")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v2")]
    [InlineData("https://example.com", "https://different.com")]
    public void ValidateProof_MismatchedUrl_FailsValidation(string proofUrl, string requestUrl)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string proof = CreateProofJwt(key, jwk, "GET", proofUrl, Guid.NewGuid().ToString());
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        DPoPBindingService.ValidateProof(proof, "GET", requestUrl, expectedJkt).Should().BeFalse();
    }

    [Fact]
    public void CreateAccessToken_WithValidJwk_CreatesToken()
    {
        (JsonWebKey jwk, _) = CreateJwk();
        byte[] secret = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");

        string token = DPoPBindingService.CreateAccessToken(jwk, new SigningCredentials(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256));

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ComputeJkt_WithValidJwk_ReturnsNonEmptyString()
    {
        (JsonWebKey jwk, _) = CreateJwk();

        string jkt = DPoPBindingService.ComputeJkt(jwk);

        jkt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ComputeJkt_SameJwk_ReturnsSameJkt()
    {
        (JsonWebKey jwk, _) = CreateJwk();

        string jkt1 = DPoPBindingService.ComputeJkt(jwk);
        string jkt2 = DPoPBindingService.ComputeJkt(jwk);

        jkt1.Should().Be(jkt2);
    }

    [Fact]
    public void ComputeJkt_DifferentJwks_ReturnsDifferentJkts()
    {
        (JsonWebKey jwk1, _) = CreateJwk();
        (JsonWebKey jwk2, _) = CreateJwk();

        string jkt1 = DPoPBindingService.ComputeJkt(jwk1);
        string jkt2 = DPoPBindingService.ComputeJkt(jwk2);

        jkt1.Should().NotBe(jkt2);
    }
}
