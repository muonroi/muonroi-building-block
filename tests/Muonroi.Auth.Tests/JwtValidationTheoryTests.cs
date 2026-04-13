namespace Muonroi.Auth.Tests;

public class JwtValidationTheoryTests
{
    private readonly InMemoryRsaKeyStore _keyStore;
    private readonly TokenRevocationStore _revocation;
    private readonly JwtService _service;

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

        Action action = () => _service.ValidateToken(token);

        action.Should().Throw<SecurityTokenExpiredException>();
    }

    [Theory]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(120)]
    public void ValidateToken_NotYetValidTokens_ThrowsSecurityTokenNotYetValidException(int notBeforeOffsetSeconds)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(10), DateTime.UtcNow.AddSeconds(notBeforeOffsetSeconds));

        Action action = () => _service.ValidateToken(token);

        action.Should().Throw<SecurityTokenNotYetValidException>();
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

        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.Should().Be("user1");
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

        principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value.Should().Be(username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid.token")]
    [InlineData("not.a.jwt.token")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    public void ValidateToken_MalformedToken_ThrowsException(string malformedToken)
    {
        Action action = () => _service.ValidateToken(malformedToken);

        action.Should().Throw<Exception>();
    }

    [Theory]
    [InlineData("wrong-issuer", "audience")]
    [InlineData("different-issuer", "audience")]
    [InlineData("issuer2", "audience")]
    public void ValidateToken_WrongIssuer_ThrowsSecurityTokenInvalidIssuerException(string wrongIssuer, string correctAudience)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));

        Action action = () => new JwtService(_keyStore, _revocation, wrongIssuer, correctAudience, new MDateTimeService()).ValidateToken(token);

        action.Should().Throw<SecurityTokenInvalidIssuerException>();
    }

    [Theory]
    [InlineData("issuer", "wrong-audience")]
    [InlineData("issuer", "different-audience")]
    [InlineData("issuer", "audience2")]
    public void ValidateToken_WrongAudience_ThrowsSecurityTokenInvalidAudienceException(string correctIssuer, string wrongAudience)
    {
        string token = _service.GenerateToken("user1", TimeSpan.FromMinutes(5));

        Action action = () => new JwtService(_keyStore, _revocation, correctIssuer, wrongAudience, new MDateTimeService()).ValidateToken(token);

        action.Should().Throw<SecurityTokenInvalidAudienceException>();
    }
}
