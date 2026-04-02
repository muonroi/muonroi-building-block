namespace Muonroi.Auth.Tests;

using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Microsoft.EntityFrameworkCore;

public class DefaultRefreshTokenValidatorTests
{
    private enum TestPermission { None }

    private class TestDbContext : MDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }

    private readonly TestDbContext _dbContext;
    private readonly IMultiLevelCacheService _cacheService = Substitute.For<IMultiLevelCacheService>();
    private readonly IOptions<AuthOptions> _authOptions = Substitute.For<IOptions<AuthOptions>>();
    private readonly IMLog<MDbContext> _logger = Substitute.For<IMLog<MDbContext>>();
    private readonly MTokenInfo _tokenInfo = new();
    private readonly DefaultRefreshTokenValidator<TestDbContext, TestPermission> _validator;

    public DefaultRefreshTokenValidatorTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);

        _authOptions.Value.Returns(new AuthOptions());
        _tokenInfo.UseRsa = false;
        _tokenInfo.SymmetricSecretKey = "super-secret-key-at-least-32-chars-long!!";
        _tokenInfo.Issuer = "test-issuer";
        _tokenInfo.Audience = "test-audience";

        _validator = new DefaultRefreshTokenValidator<TestDbContext, TestPermission>(
            _dbContext,
            _cacheService,
            _authOptions,
            _logger,
            _tokenInfo);
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
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-token";
        var refresh = new MRefreshToken
        {
            Token = token,
            CreatorUserId = userId,
            TokenValidityKey = "key",
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
}
