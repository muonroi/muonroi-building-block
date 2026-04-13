using Microsoft.EntityFrameworkCore;
using Muonroi.AspNetCore.Services;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Models.Common.Requests.Login;
using Muonroi.Core.Abstractions.Models.Common.Requests.Registers;
using Muonroi.Core.Abstractions.Models.Common.Responses.Login;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Core.Abstractions.Helpers;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Services;

public class AuthServiceTests
{
    private readonly TestDbContext _dbContext;
    private readonly IAuthenticateInfoContext _authContext;
    private readonly IAuthenticateRepository _authRepository;
    private readonly IMDateTimeService _dateTimeService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthService<TestPerm, TestDbContext> _service;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);
        _authContext = Substitute.For<IAuthenticateInfoContext>();
        _authRepository = Substitute.For<IAuthenticateRepository>();
        _dateTimeService = Substitute.For<IMDateTimeService>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        
        // Setup default mock behaviors
        _passwordHasher.HashPassword(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x => { x[1] = "salt"; return "hashed"; });
        
        _authRepository.Login(Arg.Any<LoginRequestModel>(), Arg.Any<CancellationToken>())
            .Returns(new MResponse<LoginResponseModel> { Result = new LoginResponseModel() });

        _service = new AuthService<TestPerm, TestDbContext>(
            _dbContext,
            _authContext,
            _authRepository,
            _dateTimeService,
            _passwordHasher);
            
        // Enable cross-tenant access for unit tests to bypass global query filters
        Muonroi.Tenancy.Core.TenantContext.AllowCrossTenantAccess = true;
    }

    [Fact]
    public async Task LogoutAsync_InvalidUserGuid_ReturnsError()
    {
        _authContext.CurrentUserGuid.Returns("invalid-guid");

        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAsync_ValidUserGuid_RevokesToken()
    {
        var userGuid = Guid.NewGuid();
        var tokenValidityKey = "valid-token-key";
        _authContext.CurrentUserGuid.Returns(userGuid.ToString());
        _authContext.TokenValidityKey.Returns(tokenValidityKey);
        _dateTimeService.UtcNow().Returns(DateTime.UtcNow);

        var token = new MRefreshToken
        {
            TokenValidityKey = tokenValidityKey,
            CreatorUserId = userGuid,
            IsRevoked = false,
            IsDeleted = false
        };
        _dbContext.Set<MRefreshToken>().Add(token);
        await _dbContext.SaveChangesAsync();

        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        var updatedToken = await _dbContext.Set<MRefreshToken>().FirstAsync();
        Assert.True(updatedToken.IsRevoked);
        Assert.Equal("Logout", updatedToken.ReasonRevoked);
    }

    [Fact]
    public async Task LogoutAsync_NoTokensFound_ReturnsError()
    {
        var userGuid = Guid.NewGuid();
        _authContext.CurrentUserGuid.Returns(userGuid.ToString());

        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAllAsync_InvalidUserGuid_ReturnsError()
    {
        _authContext.CurrentUserGuid.Returns("invalid-guid");

        var result = await _service.LogoutAllAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAllAsync_ValidUserGuid_RevokesAllTokens()
    {
        var userGuid = Guid.NewGuid();
        _authContext.CurrentUserGuid.Returns(userGuid.ToString());
        _dateTimeService.UtcNow().Returns(DateTime.UtcNow);

        var tokens = new[]
        {
            new MRefreshToken { CreatorUserId = userGuid, IsRevoked = false, TokenValidityKey = "1" },
            new MRefreshToken { CreatorUserId = userGuid, IsRevoked = false, TokenValidityKey = "2" }
        };
        _dbContext.Set<MRefreshToken>().AddRange(tokens);
        await _dbContext.SaveChangesAsync();

        var result = await _service.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        var updatedTokens = await _dbContext.Set<MRefreshToken>().ToListAsync();
        Assert.All(updatedTokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task RegisterAsync_UserExists_ReturnsError()
    {
        var username = "existing_user";
        _dbContext.Set<MUser>().Add(new MUser { UserName = username });
        await _dbContext.SaveChangesAsync();

        var request = new RegisterRequestModel { UserName = username };

        var result = await _service.RegisterAsync(request, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task RegisterAsync_NewUser_Success()
    {
        var request = new RegisterRequestModel
        {
            UserName = "new_user",
            Email = "new@a.com",
            Password = "Password123!",
            Name = "New",
            Surname = "User"
        };

        var result = await _service.RegisterAsync(request, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.NotNull(await _dbContext.Set<MUser>().FirstOrDefaultAsync(u => u.UserName == request.UserName));
    }

    [Fact]
    public async Task LoginAsync_CallsRepository()
    {
        var request = new LoginRequestModel { Username = "u", Password = "p" };
        var tokenInfo = new MTokenInfo();
        var signer = Substitute.For<ITokenSigner>();
        var tokenHelper = new MAuthenticateTokenHelper<TestPerm>(
            tokenInfo,
            signer,
            _dateTimeService,
            null);
        var cacheService = Substitute.For<IMultiLevelCacheService>();

        await _service.LoginAsync(request, tokenInfo, tokenHelper, cacheService, CancellationToken.None);

        await _authRepository.Received(1).Login(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_UserNotFound_ReturnsError()
    {
        _authContext.CurrentUserGuid.Returns(Guid.NewGuid().ToString());
        var request = new RefreshTokenRequestModel();
        var tokenInfo = new MTokenInfo();
        var signer = Substitute.For<ITokenSigner>();
        var tokenHelper = new MAuthenticateTokenHelper<TestPerm>(
            tokenInfo,
            signer,
            _dateTimeService,
            null);
        var cacheService = Substitute.For<IMultiLevelCacheService>();

        var result = await _service.RefreshTokenAsync(request, tokenInfo, tokenHelper, cacheService, CancellationToken.None);

        Assert.False(result.IsOk);
    }
}
