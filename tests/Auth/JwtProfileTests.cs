using Muonroi.Auth.Jwt;
using Muonroi.Auth.Keys;

namespace Muonroi.Auth.Tests;

public class JwtProfileTests
{
    private readonly JwtService _service;
    private readonly InMemoryRsaKeyStore _keyStore;
    private readonly TokenRevocationStore _revocation;

    public JwtProfileTests()
    {
        _keyStore = new InMemoryRsaKeyStore();
        _revocation = new TokenRevocationStore(new MDateTimeService());
        _service = new JwtService(_keyStore, _revocation, "issuer", "audience", new MDateTimeService());
    }

    [Fact]
    public void GenerateAndValidateToken()
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        System.Security.Claims.ClaimsPrincipal principal = _service.ValidateToken(token);
        Assert.Equal("user1", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Fact]
    public void AudienceAndIssuerAreValidated()
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        JwtService badAudienceService = new(_keyStore, _revocation, "issuer", "other", new MDateTimeService());
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => badAudienceService.ValidateToken(token));
        JwtService badIssuerService = new(_keyStore, _revocation, "other", "audience", new MDateTimeService());
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => badIssuerService.ValidateToken(token));
    }

    [Fact]
    public void LifetimeAndNotBeforeAreValidated()
    {
        string expired = _service.GenerateToken("user1", TimeSpan.FromSeconds(1), DateTime.UtcNow.AddSeconds(-2));
        Assert.Throws<SecurityTokenExpiredException>(() => _service.ValidateToken(expired));

        string future = _service.GenerateToken("user1", TimeSpan.FromMinutes(5), DateTime.UtcNow.AddMinutes(1));
        Assert.Throws<SecurityTokenNotYetValidException>(() => _service.ValidateToken(future));
    }

    [Fact]
    public void JwksAndKeyRotationWork()
    {
        string token1 = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        string kid1 = new JwtSecurityTokenHandler().ReadJwtToken(token1).Header.Kid;

        JsonWebKeySetController jwksController = new(_service);
        JsonWebKeySet jwks1 = jwksController.Get();
        Assert.Contains(jwks1.Keys, k => k.Kid == kid1);

        _service.RotateKeys();
        string token2 = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        string kid2 = new JwtSecurityTokenHandler().ReadJwtToken(token2).Header.Kid;
        Assert.NotEqual(kid1, kid2);

        JsonWebKeySet jwks2 = jwksController.Get();
        Assert.Contains(jwks2.Keys, k => k.Kid == kid2);

        _service.ValidateToken(token1);
    }

    [Fact]
    public void RevokedTokenIsRejected()
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        _service.RevokeToken(token);
        Assert.Throws<SecurityTokenException>(() => _service.ValidateToken(token));
    }
}