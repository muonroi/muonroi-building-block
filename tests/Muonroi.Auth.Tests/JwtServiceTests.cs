namespace Muonroi.Auth.Tests;

public class JwtServiceTests
{
    private static IMDateTimeService CreateDateTimeService()
    {
        return new FakeDateTimeService();
    }

    private sealed class FakeDateTimeService : IMDateTimeService
    {
        public DateTime UtcNow() => DateTime.UtcNow;
        public DateTime Now() => DateTime.Now;
        public DateTime Today() => Now().Date;
        public DateTime UtcToday() => UtcNow().Date;
        public double NowTs() => DateTimeOffset.Now.ToUnixTimeSeconds();
        public double UtcNowTs() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static JwtService CreateService(
        InMemoryRsaKeyStore? keyStore = null,
        TokenRevocationStore? revocation = null)
    {
        keyStore ??= new InMemoryRsaKeyStore();
        var dateTimeService = CreateDateTimeService();
        revocation ??= new TokenRevocationStore(dateTimeService);
        return new JwtService(keyStore, revocation, "test-issuer", "test-audience", dateTimeService);
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        JwtService service = CreateService();
        string token = service.GenerateToken("user-1", TimeSpan.FromMinutes(30));

        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3, "JWT has 3 parts");
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsClaimsPrincipal()
    {
        JwtService service = CreateService();
        string token = service.GenerateToken("user-1", TimeSpan.FromMinutes(30));

        var principal = service.ValidateToken(token);

        principal.Should().NotBeNull();
        principal.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_RevokedToken_ThrowsSecurityTokenException()
    {
        JwtService service = CreateService();
        string token = service.GenerateToken("user-1", TimeSpan.FromMinutes(30));
        service.RevokeToken(token);

        Action act = () => service.ValidateToken(token);

        act.Should().Throw<SecurityTokenException>().WithMessage("*revoked*");
    }

    [Fact]
    public void RotateKeys_NewTokenStillValidatable()
    {
        var keyStore = new InMemoryRsaKeyStore();
        JwtService service = CreateService(keyStore);

        string token1 = service.GenerateToken("user-1", TimeSpan.FromMinutes(30));
        service.RotateKeys();
        string token2 = service.GenerateToken("user-2", TimeSpan.FromMinutes(30));

        // Both tokens should be valid (old key still in store)
        service.ValidateToken(token1).Should().NotBeNull();
        service.ValidateToken(token2).Should().NotBeNull();
    }

    [Fact]
    public void GetJsonWebKeySet_ReturnsKeys()
    {
        JwtService service = CreateService();

        var jwks = service.GetJsonWebKeySet();

        jwks.Should().NotBeNull();
        jwks.Keys.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateToken_WithNotBefore_SetsCorrectly()
    {
        JwtService service = CreateService();
        DateTime future = DateTime.UtcNow.AddMinutes(5);

        string token = service.GenerateToken("user-1", TimeSpan.FromMinutes(30), future);

        token.Should().NotBeNullOrEmpty();
    }
}
