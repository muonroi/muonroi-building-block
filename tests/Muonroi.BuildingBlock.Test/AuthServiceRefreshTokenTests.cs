namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AuthServiceRefreshTokenTests
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
        AuthenticateRepository<TestDbContext, TestPerm> repo = new(helper, db, ctx, new TestLicenseGuard(), info, cache);
        return new AuthService<TestPerm, TestDbContext>(db, ctx, repo);
    }

    private static AuthService<TestPerm, FaultyDbContext> CreateFaultyService(FaultyDbContext db,
        MAuthenticateInfoContext ctx, IMultiLevelCacheService cache, MTokenInfo info,
        MAuthenticateTokenHelper<TestPerm> helper)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        AuthenticateRepository<FaultyDbContext, TestPerm> repo = new(helper, db, ctx, new TestLicenseGuard(), info, cache);
        return new AuthService<TestPerm, FaultyDbContext>(db, ctx, repo);
    }

    [Fact]
    public async Task RefreshToken_Success_Returns_New_Tokens_And_Revokes_Old()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("refresh_success").Options;
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
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();

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

        RefreshTokenRequestModel model = new()
        {
            AccessToken = oldAccess,
            RefreshToken = oldRefresh
        };
        MResponse<RefreshTokenResponseModel> refresh = await svc.RefreshTokenAsync(model, info, helper, cache, CancellationToken.None);

        Assert.True(refresh.IsOk);
        Assert.NotEqual(oldAccess, refresh.Result!.AccessToken);
        Assert.NotEqual(oldRefresh, refresh.Result.RefreshToken);

        MRefreshToken oldToken = await db.RefreshTokens.FirstAsync(t => t.Token == oldRefresh);
        Assert.True(oldToken.IsRevoked);
        Assert.Equal("RefreshToken", oldToken.ReasonRevoked);
    }

    [Fact]
    public async Task RefreshToken_User_Not_Found_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("refresh_user_not_found")
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
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = Guid.NewGuid().ToString(),
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

        RefreshTokenRequestModel model = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> refresh = await svc.RefreshTokenAsync(model, info, helper, cache, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task RefreshToken_Not_Used_For_Eim_Period_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("refresh_eim").Options;
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
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();

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
        token.LastUsedDate = Clock.UtcNow.AddMinutes(-(info.RefreshTokenEim + 1));
        token.CreationTime = Clock.UtcNow.AddMinutes(-2); // still within TTL
        token.ExpiredDate = Clock.UtcNow.AddMinutes(3);
        _ = db.RefreshTokens.Update(token);
        _ = await db.SaveChangesAsync();

        RefreshTokenRequestModel model = new()
        {
            AccessToken = login.Result.AccessToken,
            RefreshToken = refreshToken
        };
        MResponse<RefreshTokenResponseModel> refresh = await svc.RefreshTokenAsync(model, info, helper, cache, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task RefreshToken_Expired_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("refresh_expired_token").Options;
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
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();

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
        _ = db.RefreshTokens.Update(token);
        _ = await db.SaveChangesAsync();

        RefreshTokenRequestModel model = new()
        {
            AccessToken = login.Result.AccessToken,
            RefreshToken = refreshToken
        };
        MResponse<RefreshTokenResponseModel> refresh = await svc.RefreshTokenAsync(model, info, helper, cache, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task RefreshToken_Db_Error_Throws_Exception()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        const string dbName = "refresh_dberror";

        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
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
        using TestDbContext seed = new(seedOptions);
        _ = await seed.Users.AddAsync(user);
        _ = await seed.SaveChangesAsync();

        MAuthenticateInfoContext loginCtx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> loginSvc = CreateService(seed, loginCtx, cache, info, helper);

        MResponse<LoginResponseModel> login = await loginSvc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        DbContextOptions<FaultyDbContext> faultyOptions = new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using FaultyDbContext db = new(faultyOptions);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, FaultyDbContext> svc = CreateFaultyService(db, ctx, cache, info, helper);

        await Assert.ThrowsAsync<Exception>(() =>
        {
            RefreshTokenRequestModel model = new()
            {
                AccessToken = login.Result!.AccessToken,
                RefreshToken = login.Result.RefreshToken
            };
            return svc.RefreshTokenAsync(model, info, helper, cache, CancellationToken.None);
        });
    }
}

