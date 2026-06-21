using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Muonroi.Auth.Jwt;
using Muonroi.Auth.Keys;
using Xunit;

namespace Muonroi.Auth.Tests;

public class JwtValidationTheoryTests
{
    private readonly JwtService _service;
    private readonly InMemoryRsaKeyStore _keyStore;
    private readonly TokenRevocationStore _revocation;

    public JwtValidationTheoryTests()
    {
        _keyStore = new InMemoryRsaKeyStore();
        _revocation = new TokenRevocationStore(new MDateTimeService());
        _service = new JwtService(_keyStore, _revocation, "issuer", "audience", new MDateTimeService());
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(-120)]
    public void ValidateToken_ExpiredTokens_ThrowsSecurityTokenExpiredException(int expiryOffsetSeconds)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromSeconds(1), DateTime.UtcNow.AddSeconds(expiryOffsetSeconds));
        Thread.Sleep(1500);
        Assert.Throws<SecurityTokenExpiredException>(() => _service.ValidateToken(token));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(120)]
    public void ValidateToken_NotYetValidTokens_ThrowsSecurityTokenNotYetValidException(int notBeforeOffsetSeconds)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(10), DateTime.UtcNow.AddSeconds(notBeforeOffsetSeconds));
        Assert.Throws<SecurityTokenNotYetValidException>(() => _service.ValidateToken(token));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(3600)]
    public void ValidateToken_ValidLifetimes_SuccessfullyValidates(int lifetimeSeconds)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromSeconds(lifetimeSeconds));
        ClaimsPrincipal principal = _service.ValidateToken(token);
        Assert.NotNull(principal);
        Assert.Equal("user1", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user+tag@example.com")]
    [InlineData("user_123")]
    [InlineData("user-456")]
    [InlineData("user.name")]
    [InlineData("123456")]
    public void GenerateToken_SpecialCharactersInUsername_SuccessfullyGeneratesToken(string username)
    {
        string token = _service.GenerateToken(username, TimeSpan.FromMinutes(5));
        ClaimsPrincipal principal = _service.ValidateToken(token);
        Assert.NotNull(principal);
        Assert.Equal(username, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid.token")]
    [InlineData("not.a.jwt.token")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    public void ValidateToken_MalformedToken_ThrowsException(string malformedToken)
    {
        Assert.ThrowsAny<Exception>(() => _service.ValidateToken(malformedToken));
    }

    [Theory]
    [InlineData("wrong-issuer", "audience")]
    [InlineData("different-issuer", "audience")]
    [InlineData("issuer2", "audience")]
    public void ValidateToken_WrongIssuer_ThrowsSecurityTokenInvalidIssuerException(string wrongIssuer, string correctAudience)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        JwtService wrongIssuerService = new(_keyStore, _revocation, wrongIssuer, correctAudience, new MDateTimeService());
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => wrongIssuerService.ValidateToken(token));
    }

    [Theory]
    [InlineData("issuer", "wrong-audience")]
    [InlineData("issuer", "different-audience")]
    [InlineData("issuer", "audience2")]
    public void ValidateToken_WrongAudience_ThrowsSecurityTokenInvalidAudienceException(string correctIssuer, string wrongAudience)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        JwtService wrongAudienceService = new(_keyStore, _revocation, correctIssuer, wrongAudience, new MDateTimeService());
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => wrongAudienceService.ValidateToken(token));
    }
}
