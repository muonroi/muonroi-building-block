namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AuthenticateRepositoryTests
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

    private static AuthenticateRepository<TDbContext, TestPerm> CreateRepo<TDbContext>(TDbContext db,
        MAuthenticateInfoContext ctx, IMultiLevelCacheService cache, MTokenInfo info,
        MAuthenticateTokenHelper<TestPerm> helper)
        where TDbContext : MDbContext
    {
        return CreateRepo(db, ctx, cache, info, helper, new TestLicenseGuard(), new RulesEngineService(new InMemoryRuleSetStore()));
    }

    private static AuthenticateRepository<TDbContext, TestPerm> CreateRepo<TDbContext>(TDbContext db,
        MAuthenticateInfoContext ctx, IMultiLevelCacheService cache, MTokenInfo info,
        MAuthenticateTokenHelper<TestPerm> helper, ILicenseGuard guard, RulesEngineService engine)
        where TDbContext : MDbContext
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        return new AuthenticateRepository<TDbContext, TestPerm>(helper, db, ctx, guard, info, cache);
    }

    [Fact]
    public async Task RefreshToken_Success_Returns_New_Tokens()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_refresh_success").Options;
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
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> login = await repo.Login(new LoginRequestModel { Username = "u", Password = "correct" },
            CancellationToken.None);
        RefreshTokenRequestModel model = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> refresh = await repo.RefreshToken(model, CancellationToken.None);

        Assert.True(refresh.IsOk);
        Assert.NotEqual(login.Result.AccessToken, refresh.Result!.AccessToken);
        Assert.NotEqual(login.Result.RefreshToken, refresh.Result.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Expired_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_refresh_expired").Options;
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
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        MResponse<LoginResponseModel> login = await repo.Login(new LoginRequestModel { Username = "u", Password = "correct" },
            CancellationToken.None);
        MRefreshToken token = await db.RefreshTokens.FirstAsync();
        token.ExpiredDate = Clock.UtcNow.AddMinutes(-1);
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync();

        RefreshTokenRequestModel model = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> refresh = await repo.RefreshToken(model, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task RefreshToken_NotFound_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_refresh_notfound").Options;
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
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        MResponse<LoginResponseModel> login = await repo.Login(new LoginRequestModel { Username = "u", Password = "correct" },
            CancellationToken.None);

        RefreshTokenRequestModel model = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = "invalid"
        };
        MResponse<RefreshTokenResponseModel> refresh = await repo.RefreshToken(model, CancellationToken.None);

        Assert.False(refresh.IsOk);
    }

    [Fact]
    public async Task RefreshToken_Db_Error_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        const string dbName = "repo_refresh_db_error";
        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(dbName).Options;
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
            await seed.SaveChangesAsync();
        }

        DbContextOptions<FaultyDbContext> options = new DbContextOptionsBuilder<FaultyDbContext>().UseInMemoryDatabase(dbName).Options;
        using FaultyDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = "1",
            CurrentUsername = "u",
            Language = "en"
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<FaultyDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        await Assert.ThrowsAnyAsync<Exception>(() =>
        {
            RefreshTokenRequestModel model = new()
            {
                AccessToken = "a",
                RefreshToken = "r"
            };
            return repo.RefreshToken(model,
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task ValidateTokenValidity_Returns_Token()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_validity_success").Options;
        using TestDbContext db = new(options);
        MRefreshToken refresh = new()
        {
            Token = "t",
            TokenValidityKey = "k",
            CreatorUserId = Guid.NewGuid(),
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(5)
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        MResponse<string> result = await repo.ValidateTokenValidity("k", CancellationToken.None);
        Assert.True(result.IsOk);
        Assert.Equal("t", result.Result);
    }

    [Fact]
    public async Task ValidateTokenValidity_Invalid_Key()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_validity_invalid").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        MResponse<string> result = await repo.ValidateTokenValidity("missing", CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ValidateTokenValidity_Empty_Key()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_validity_empty").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUsername = "u",
            Language = "en"
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        MResponse<string> result = await repo.ValidateTokenValidity(string.Empty, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    private class FailingQueryDbContext(DbContextOptions<FailingQueryDbContext> options)
        : MDbContext(options, new FakeMediator())
    {
        public static new DbSet<MRefreshToken> RefreshTokens => throw new NullReferenceException();
    }

    [Fact]
    public async Task ValidateTokenValidity_Db_Error_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<FailingQueryDbContext> options = new DbContextOptionsBuilder<FailingQueryDbContext>().UseInMemoryDatabase("repo_validity_db_error")
            .Options;
        using FailingQueryDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<FailingQueryDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);
        MResponse<string> result = await repo.ValidateTokenValidity("k", CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_login_valid").Options;
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
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await repo.Login(new LoginRequestModel { Username = "u", Password = "correct" },
            CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.False(string.IsNullOrEmpty(result.Result?.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Result?.RefreshToken));
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsErrorFromRules()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_login_invalid").Options;
        using TestDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper);

        MResponse<LoginResponseModel> result = await repo.Login(new LoginRequestModel { Username = string.Empty, Password = string.Empty },
            CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Login_WhenRuleEngineNotLicensed_UsesLegacyPath_AndDoesNotInvokeRuleWorkflow()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("repo_login_free_mode").Options;
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
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        ILicenseGuard guard = new DenyRuleEngineGuard();
        RulesEngineService engine = new(new InMemoryRuleSetStore(), null, guard);
        AuthenticateRepository<TestDbContext, TestPerm> repo = CreateRepo(db, ctx, cache, info, helper, guard, engine);

        MResponse<LoginResponseModel> result = await repo.Login(new LoginRequestModel { Username = "u", Password = "correct" },
            CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.NotNull(result.Result);
        Assert.False(string.IsNullOrWhiteSpace(result.Result!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Result.RefreshToken));
    }

    private sealed class DenyRuleEngineGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();
        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.RuleEngine, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
            {
                throw new InvalidOperationException("rule-engine feature not licensed");
            }
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken()
        {
            return "test";
        }

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("k", encryptedData);
        }
    }
}


