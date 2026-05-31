namespace Muonroi.Auth.Tests;

using Muonroi.Data.EntityFrameworkCore.Auth;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class DefaultRefreshTokenValidatorTests
{
    private enum TestPermission { None }

    private class TestDbContext : MDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }

    private readonly TestDbContext _dbContext;
    private readonly IMultiLevelCacheService _cacheService = Substitute.For<IMultiLevelCacheService>();
    private readonly IMLog<MDbContext> _logger = Substitute.For<IMLog<MDbContext>>();
    private readonly MTokenInfo _tokenInfo = new();
    private readonly DefaultRefreshTokenValidator<TestDbContext, TestPermission> _validator;

    public DefaultRefreshTokenValidatorTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);

        _tokenInfo.UseRsa = false;
        _tokenInfo.SymmetricSecretKey = "super-secret-key-at-least-32-chars-long!!";
        _tokenInfo.Issuer = "test-issuer";
        _tokenInfo.Audience = "test-audience";

        var configuration = new ConfigurationBuilder().Build();
        var resourceSetting = new ResourceSetting();

        _validator = new DefaultRefreshTokenValidator<TestDbContext, TestPermission>(
            _dbContext,
            _cacheService,
            resourceSetting,
            configuration,
            _tokenInfo,
            _logger);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnNull_WhenNoTokenInContext()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = await _validator.ValidateAsync(httpContext);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ShouldReturnInfo_WhenTokenIsValid()
    {
        // Arrange — the validator verifies the JWT signature + claims, so mint a real signed token
        // (issuer/audience/key matching _tokenInfo) carrying the user_identifier + token_validity_key claims.
        var userId = Guid.NewGuid();
        const string validityKey = "key";
        string token = CreateSignedJwt(userId, validityKey);

        var refresh = new MRefreshToken
        {
            Token = token,
            CreatorUserId = userId,
            TokenValidityKey = validityKey,
            ExpiredDate = DateTime.UtcNow.AddDays(1)
        };
        await _dbContext.RefreshTokens.AddAsync(refresh);
        await _dbContext.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {token}";

        // Act
        var result = await _validator.ValidateAsync(httpContext);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentUserGuid.Should().Be(userId.ToString());
    }

    private string CreateSignedJwt(Guid userId, string validityKey)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_tokenInfo.SymmetricSecretKey));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken jwt = new(
            issuer: _tokenInfo.Issuer,
            audience: _tokenInfo.Audience,
            claims:
            [
                new Claim("user_identifier", userId.ToString()),
                new Claim("token_validity_key", validityKey)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
