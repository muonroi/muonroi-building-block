namespace Muonroi.AspNetCore.Tests.Services;

/// <summary>
/// Regression coverage for the refresh-token identity recovery. The refresh-token
/// endpoint is [AllowAnonymous] and is, by design, called with an already-EXPIRED
/// access token, so the request context carries no authenticated user. The identity
/// must therefore be recovered from the expired token itself — its signature, issuer
/// and audience are still validated; only the lifetime is ignored.
/// </summary>
public class RefreshTokenExpiredIdentityTests
{
    private const string Secret = "test-secret-key-min-32-characters-long!!";
    private const string Issuer = "https://test.local";
    private const string Audience = "https://test.local";

    private static MTokenInfo TokenInfo() => new()
    {
        SymmetricSecretKey = Secret,
        Issuer = Issuer,
        Audience = Audience,
        UseRsa = false,
        ExpiryMinutes = 60
    };

    private static string MintToken(Guid userId, string secret, string issuer, string audience, bool expired)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secret));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimConstants.UserIdentifier, userId.ToString())],
            notBefore: expired ? now.AddHours(-2) : now,
            expires: expired ? now.AddHours(-1) : now.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void RecoversUserId_FromExpiredButOtherwiseValidToken()
    {
        Guid userId = Guid.NewGuid();
        string token = MintToken(userId, Secret, Issuer, Audience, expired: true);

        bool ok = AuthorizeInternal.TryGetUserIdFromExpiredToken(token, TokenInfo(), out Guid resolved);

        Assert.True(ok);
        Assert.Equal(userId, resolved);
    }

    [Fact]
    public void RecoversUserId_WhenTokenCarriesBearerPrefix()
    {
        Guid userId = Guid.NewGuid();
        string token = "Bearer " + MintToken(userId, Secret, Issuer, Audience, expired: true);

        bool ok = AuthorizeInternal.TryGetUserIdFromExpiredToken(token, TokenInfo(), out Guid resolved);

        Assert.True(ok);
        Assert.Equal(userId, resolved);
    }

    [Fact]
    public void Rejects_TokenSignedWithDifferentKey()
    {
        Guid userId = Guid.NewGuid();
        string token = MintToken(userId, "another-secret-key-min-32-characters!!!", Issuer, Audience, expired: true);

        bool ok = AuthorizeInternal.TryGetUserIdFromExpiredToken(token, TokenInfo(), out Guid resolved);

        Assert.False(ok);
        Assert.Equal(Guid.Empty, resolved);
    }

    [Fact]
    public void Rejects_TokenWithWrongIssuer()
    {
        Guid userId = Guid.NewGuid();
        string token = MintToken(userId, Secret, "https://evil.local", Audience, expired: true);

        bool ok = AuthorizeInternal.TryGetUserIdFromExpiredToken(token, TokenInfo(), out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-jwt")]
    public void Rejects_MissingOrMalformedToken(string? token)
    {
        bool ok = AuthorizeInternal.TryGetUserIdFromExpiredToken(token, TokenInfo(), out Guid resolved);

        Assert.False(ok);
        Assert.Equal(Guid.Empty, resolved);
    }
}
