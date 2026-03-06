using Microsoft.EntityFrameworkCore;

namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AuthorizeInternalResolveRefreshTokenTests
{
    private static (MTokenInfo Info, MAuthenticateTokenHelper<TestPerm> Helper) CreateTokenHelper()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            RefreshTokenTtl = 5,
            RefreshTokenEim = 5,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        return (info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)));
    }

    private static AuthService<TestPerm, TestDbContext> CreateService(TestDbContext db, MAuthenticateInfoContext ctx,
        IMultiLevelCacheService cache, MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        RulesEngineService engine = new(new InMemoryRuleSetStore());
        AuthenticateRepository<TestDbContext, TestPerm> repo = new(helper, db, ctx, new TestLicenseGuard(), info, cache);
        return new AuthService<TestPerm, TestDbContext>(db, ctx, repo);
    }

    [Fact]
    public async Task ResolveRefreshToken_Success_Returns_New_Tokens()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("resolve_refresh_success")
            .Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("correct", out string? salt);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = pwd,
            Salt = salt
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);
        string oldAccess = login.Result!.AccessToken;
        string oldRefresh = login.Result.RefreshToken;

        RefreshTokenRequestModel request = new()
        {
            AccessToken = oldAccess,
            RefreshToken = oldRefresh
        };
        MResponse<RefreshTokenResponseModel> result = new();
        MResponse<RefreshTokenResponseModel> refresh = await db.ResolveRefreshToken(request, result, info, helper, user, cache,
            ctx.Language, null, CancellationToken.None);

        Assert.True(refresh.IsOk);
        Assert.NotEqual(oldAccess, refresh.Result!.AccessToken);
        Assert.NotEqual(oldRefresh, refresh.Result.RefreshToken);

        MRefreshToken oldToken = await db.RefreshTokens.FirstAsync(t => t.Token == oldRefresh);
        Assert.True(oldToken.IsRevoked);
    }

    [Fact]
    public async Task ResolveRefreshToken_InvalidToken_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("resolve_refresh_invalid")
            .Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();

        RefreshTokenRequestModel request = new()
        {
            AccessToken = "Bearer invalid",
            RefreshToken = "r"
        };
        MResponse<RefreshTokenResponseModel> result = new();
        MResponse<RefreshTokenResponseModel> refresh = await db.ResolveRefreshToken(request, result, info, helper, user, cache,
            "en", null, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task ResolveRefreshToken_ExpiredToken_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("resolve_refresh_expired")
            .Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("correct", out string? salt);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = pwd,
            Salt = salt
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        string refreshToken = login.Result!.RefreshToken;
        MRefreshToken token = await db.RefreshTokens.FirstAsync();
        token.ExpiredDate = Clock.UtcNow.AddMinutes(-1);
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync();

        RefreshTokenRequestModel request = new()
        {
            AccessToken = login.Result.AccessToken,
            RefreshToken = refreshToken
        };
        MResponse<RefreshTokenResponseModel> result = new();
        MResponse<RefreshTokenResponseModel> refresh = await db.ResolveRefreshToken(request, result, info, helper, user, cache,
            ctx.Language, null, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task ResolveRefreshToken_NoToken_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("resolve_refresh_missing")
            .Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("correct", out string? salt);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = pwd,
            Salt = salt
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        RefreshTokenRequestModel request = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = string.Empty
        };
        MResponse<RefreshTokenResponseModel> result = new();
        MResponse<RefreshTokenResponseModel> refresh = await db.ResolveRefreshToken(request, result, info, helper, user, cache,
            ctx.Language, null, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }
}


