namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AuthServiceLoginTests
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

    [Fact]
    public async Task Login_With_Wrong_Password_Fails()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_fail").Options;
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
        _ = await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "wrong"
        }, info, helper, cache, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Login_Exceed_Attempts_Locks_Account()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_lock").Options;
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
        _ = await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        for (int i = 0; i < 3; i++)
        {
            _ = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "wrong" }, info, helper, cache,
                CancellationToken.None);
        }

        MUser dbUser = await db.Users.FirstAsync();
        Assert.False(dbUser.IsActive);

        MResponse<LoginResponseModel> attempt = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "correct" }, info, helper,
            cache, CancellationToken.None);
        Assert.False(attempt.IsOk);
    }

    [Fact]
    public async Task RefreshToken_Expired_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("refresh_expired").Options;
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
    public async Task Login_Success_Returns_Tokens()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_success_case").Options;
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

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.False(string.IsNullOrEmpty(result.Result!.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Result.RefreshToken));
    }

    [Fact]
    public async Task Login_User_Not_Found_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_user_not_found").Options;
        using TestDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Login_Invalid_Input_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_invalid_input").Options;
        using TestDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = string.Empty,
            Password = string.Empty
        }, info, helper, cache, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Login_Locked_User_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_locked_user").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("correct", out string? salt);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = pwd,
            Salt = salt,
            IsActive = false
        };
        await db.Users.AddAsync(user);
        MUserLoginAttempt attempt = new()
        {
            UserGuid = user.EntityId,
            LockTo = DateTime.MaxValue,
            AttemptTime = 6
        };
        await db.MUserLoginAttempts.AddAsync(attempt);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Login_With_Null_Repository_Does_Not_Throw()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_null_repo").Options;
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
        _ = await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = new(db, ctx, null!);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task Login_Expired_Lock_Resets_Attempts_Even_When_Failing()
    {
        Clock.Provider = new UtcClockProvider();
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_expired_lock").Options;
        await using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("correct", out string? salt);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = pwd,
            Salt = salt,
            IsActive = false
        };
        await db.Users.AddAsync(user);
        MUserLoginAttempt attempt = new()
        {
            UserGuid = user.EntityId,
            AttemptTime = 3,
            LockTo = Clock.UtcNow.AddMinutes(-10)
        };
        await db.MUserLoginAttempts.AddAsync(attempt);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "wrong"
        }, info, helper, cache, CancellationToken.None);

        MUserLoginAttempt updatedAttempt = await db.MUserLoginAttempts.FirstAsync();
        Assert.False(result.IsOk);
        Assert.Equal(1, updatedAttempt.AttemptTime);
        Assert.True((await db.Users.FirstAsync()).IsActive);
    }

    [Fact]
    public async Task Login_Db_Error_Throws_Exception()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        const string dbName = "login_db_error";

        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using (TestDbContext seed = new(seedOptions))
        {
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
            _ = await seed.Users.AddAsync(user);
            _ = await seed.SaveChangesAsync();
        }

        DbContextOptions<FailingDbContext> options = new DbContextOptionsBuilder<FailingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        using FailingDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, FailingDbContext> svc =
            new(db, ctx, new AuthenticateRepository<FailingDbContext, TestPerm>(helper, db, ctx, new TestLicenseGuard(), info, cache));

        await Assert.ThrowsAsync<Exception>(() => svc.LoginAsync(new LoginRequestModel
        {
            Username = "u",
            Password = "correct"
        }, info, helper, cache, CancellationToken.None));
    }

    private class FailingDbContext(DbContextOptions<FailingDbContext> options) : MDbContext(options, new FakeMediator())
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new Exception("fail");
        }
    }
}


