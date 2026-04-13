namespace Muonroi.Auth.Tests;

public class JwtProfileTests
{
    private readonly InMemoryRsaKeyStore _keyStore;
    private readonly TokenRevocationStore _revocation;
    private readonly JwtService _service;

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

        ClaimsPrincipal principal = _service.ValidateToken(token);

        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.Should().Be("user1");
    }

    [Fact]
    public void AudienceAndIssuerAreValidated()
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));

        Action badAudience = () => new JwtService(_keyStore, _revocation, "issuer", "other", new MDateTimeService()).ValidateToken(token);
        Action badIssuer = () => new JwtService(_keyStore, _revocation, "other", "audience", new MDateTimeService()).ValidateToken(token);

        badAudience.Should().Throw<SecurityTokenInvalidAudienceException>();
        badIssuer.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public void LifetimeAndNotBeforeAreValidated()
    {
        string expired = _service.GenerateToken("user1", TimeSpan.FromSeconds(1), DateTime.UtcNow.AddSeconds(-2));
        string future = _service.GenerateToken("user1", TimeSpan.FromMinutes(5), DateTime.UtcNow.AddMinutes(1));

        Action expiredAction = () => _service.ValidateToken(expired);
        Action futureAction = () => _service.ValidateToken(future);

        expiredAction.Should().Throw<SecurityTokenExpiredException>();
        futureAction.Should().Throw<SecurityTokenNotYetValidException>();
    }

    [Fact]
    public void JwksAndKeyRotationWork()
    {
        string token1 = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        string? kid1 = new JwtSecurityTokenHandler().ReadJwtToken(token1).Header.Kid;

        JsonWebKeySet jwks1 = new JsonWebKeySetController(_service).Get();
        jwks1.Keys.Should().Contain(k => k.Kid == kid1);

        _service.RotateKeys();
        string token2 = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        string? kid2 = new JwtSecurityTokenHandler().ReadJwtToken(token2).Header.Kid;

        kid2.Should().NotBe(kid1);

        JsonWebKeySet jwks2 = new JsonWebKeySetController(_service).Get();
        jwks2.Keys.Should().Contain(k => k.Kid == kid2);
        _service.ValidateToken(token1);
    }

    [Fact]
    public void RevokedTokenIsRejected()
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));
        _service.RevokeToken(token);

        Action action = () => _service.ValidateToken(token);

        action.Should().Throw<SecurityTokenException>();
    }
}
