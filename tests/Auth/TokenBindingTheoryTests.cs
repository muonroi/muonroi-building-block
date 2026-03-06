using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Muonroi.Auth.TokenBinding;
using Xunit;

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
        JwtSecurityTokenHandler handler = new();
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
        JwtSecurityToken token = new(header, payload);
        return handler.WriteToken(token);
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
        string jti = Guid.NewGuid().ToString();
        string proof = CreateProofJwt(key, jwk, httpMethod, "https://api.example.com", jti);
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        bool result = DPoPBindingService.ValidateProof(proof, httpMethod, "https://api.example.com", expectedJkt);

        Assert.True(result);
    }

    [Theory]
    [InlineData("https://api.example.com")]
    [InlineData("https://api.example.com/resource")]
    [InlineData("https://api.example.com/v1/users")]
    [InlineData("https://example.com")]
    public void ValidateProof_DifferentUrls_ValidatesCorrectly(string url)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string jti = Guid.NewGuid().ToString();
        string proof = CreateProofJwt(key, jwk, "GET", url, jti);
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        bool result = DPoPBindingService.ValidateProof(proof, "GET", url, expectedJkt);

        Assert.True(result);
    }

    [Theory]
    [InlineData("GET", "POST")]
    [InlineData("POST", "GET")]
    [InlineData("PUT", "DELETE")]
    [InlineData("DELETE", "PATCH")]
    public void ValidateProof_MismatchedHttpMethod_FailsValidation(string proofMethod, string requestMethod)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string jti = Guid.NewGuid().ToString();
        string proof = CreateProofJwt(key, jwk, proofMethod, "https://api.example.com", jti);
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        bool result = DPoPBindingService.ValidateProof(proof, requestMethod, "https://api.example.com", expectedJkt);

        Assert.False(result);
    }

    [Theory]
    [InlineData("https://api.example.com", "https://other.example.com")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v2")]
    [InlineData("https://example.com", "https://different.com")]
    public void ValidateProof_MismatchedUrl_FailsValidation(string proofUrl, string requestUrl)
    {
        (JsonWebKey jwk, RsaSecurityKey key) = CreateJwk();
        string jti = Guid.NewGuid().ToString();
        string proof = CreateProofJwt(key, jwk, "GET", proofUrl, jti);
        string expectedJkt = DPoPBindingService.ComputeJkt(jwk);

        bool result = DPoPBindingService.ValidateProof(proof, "GET", requestUrl, expectedJkt);

        Assert.False(result);
    }

    [Fact]
    public void CreateAccessToken_WithValidJwk_CreatesToken()
    {
        (JsonWebKey jwk, RsaSecurityKey _) = CreateJwk();
        byte[] secret = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        SigningCredentials credentials = new(new SymmetricSecurityKey(secret), SecurityAlgorithms.HmacSha256);

        string token = DPoPBindingService.CreateAccessToken(jwk, credentials);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void ComputeJkt_WithValidJwk_ReturnsNonEmptyString()
    {
        (JsonWebKey jwk, RsaSecurityKey _) = CreateJwk();

        string jkt = DPoPBindingService.ComputeJkt(jwk);

        Assert.NotNull(jkt);
        Assert.NotEmpty(jkt);
    }

    [Fact]
    public void ComputeJkt_SameJwk_ReturnsSameJkt()
    {
        (JsonWebKey jwk, RsaSecurityKey _) = CreateJwk();

        string jkt1 = DPoPBindingService.ComputeJkt(jwk);
        string jkt2 = DPoPBindingService.ComputeJkt(jwk);

        Assert.Equal(jkt1, jkt2);
    }

    [Fact]
    public void ComputeJkt_DifferentJwks_ReturnsDifferentJkts()
    {
        (JsonWebKey jwk1, RsaSecurityKey _) = CreateJwk();
        (JsonWebKey jwk2, RsaSecurityKey _) = CreateJwk();

        string jkt1 = DPoPBindingService.ComputeJkt(jwk1);
        string jkt2 = DPoPBindingService.ComputeJkt(jwk2);

        Assert.NotEqual(jkt1, jkt2);
    }
}