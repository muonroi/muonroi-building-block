namespace Muonroi.BuildingBlock.Test;

public class AuthorizeInternalMoreTests
{
    private static (MTokenInfo Info, MAuthenticateTokenHelper<TestPerm> Helper) CreateHelper()
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

    private static MethodInfo GetPrivate(string name)
    {
        return typeof(AuthorizeInternal)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(4, 10)]
    [InlineData(5, 30)]
    public void UpdateLoginAttemptStatus_SetsLockTime(int attempts, int minutes)
    {
        MUser user = new()
        {
            IsActive = true
        };
        MUserLoginAttempt attempt = new()
        {
            AttemptTime = attempts
        };
        DateTime before = Clock.UtcNow;
        MethodInfo mi = GetPrivate("UpdateLoginAttemptStatus");
        mi.Invoke(null, [user, attempt]);
        Assert.False(user.IsActive);
        double diff = (attempt.LockTo - before).TotalMinutes;
        Assert.InRange(diff, minutes - 0.1, minutes + 0.1);
    }

    [Fact]
    public void UpdateLoginAttemptStatus_SixSetsMax()
    {
        MUser user = new()
        {
            IsActive = true
        };
        MUserLoginAttempt attempt = new()
        {
            AttemptTime = 6
        };
        MethodInfo mi = GetPrivate("UpdateLoginAttemptStatus");
        mi.Invoke(null, [user, attempt]);
        Assert.False(user.IsActive);
        Assert.Equal(DateTime.MaxValue, attempt.LockTo);
    }

    [Fact]
    public void UpdateLoginAttemptStatus_BelowThreshold()
    {
        MUser user = new()
        {
            IsActive = true
        };
        MUserLoginAttempt attempt = new()
        {
            AttemptTime = 2
        };
        MethodInfo mi = GetPrivate("UpdateLoginAttemptStatus");
        mi.Invoke(null, [user, attempt]);
        Assert.True(user.IsActive);
        Assert.Equal(default, attempt.LockTo);
    }

    [Fact]
    public async Task VerifyPrincipalFromExpiredToken_Valid()
    {
        TenantContext.CurrentTenantId = "t_valid";
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        string token = helper.GenerateAuthenticateToken(
            new MUserModel("g", "u", "v", "name", "surname", "phone", "email"), [TestPerm.One]);
        MethodInfo mi = GetPrivate("VerifyPrincipalFromExpiredToken");
        Task<bool> t = (Task<bool>)mi.Invoke(null, [token, info])!;
        bool ok = await t;
        Assert.True(ok);
    }

    [Fact]
    public async Task VerifyPrincipalFromExpiredToken_Invalid()
    {
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateHelper();
        MethodInfo mi = GetPrivate("VerifyPrincipalFromExpiredToken");
        Task<bool> t = (Task<bool>)mi.Invoke(null, ["badtoken", info])!;
        bool ok = await t;
        Assert.False(ok);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_ContextNull_Throws()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctx_null_key").Options;
        using TestDbContext db = new(options);
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            db.ResolveTokenValidityKey("token", null!, cache, null));
    }

    [Fact]
    public async Task ResolveTokenValidityKey_FromCache()
    {
        TenantContext.CurrentTenantId = "t_cache";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("token_cache").Options;
        using TestDbContext db = new(options);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MUser user = new()
        {
            UserName = "u"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), "u", validity, "name", "surname", "phone", "email");
        List<Claim> extras = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extras);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        await cache.SetAsync($"token_validity:{validity}", refresh, 5);
        DefaultHttpContext ctx = new();
        MRefreshToken? result = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            null,
            tokenInfo: info);
        Assert.NotNull(result);
        Assert.Equal(refresh.Token, result!.Token);
    }

    [Fact]
    public void ExtractClaimsFromToken_Empty_ReturnsEmpty()
    {
        MethodInfo mi = GetPrivate("ExtractClaimsFromToken");
        List<Claim> claims = (List<Claim>)mi.Invoke(null, [""])!;
        Assert.Empty(claims);
    }

    [Fact]
    public async Task ResolveRefreshToken_Revoked_ReturnsError()
    {
        TenantContext.CurrentTenantId = "t_rev";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("refresh_revoked").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("p", out string? salt);
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = new(db, ctx,
            new AuthenticateRepository<TestDbContext, TestPerm>(helper, db, ctx, new TestLicenseGuard(), info,
                new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache())));
        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "p" }, info, helper,
            new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache()),
            CancellationToken.None);
        MRefreshToken token = await db.RefreshTokens
            .SingleAsync(t => t.Token == login.Result!.RefreshToken);
        token.IsRevoked = true;
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync();
        RefreshTokenRequestModel req = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> result = new();
        result = await db.ResolveRefreshToken(req, result, info, helper, user,
            new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache()), "en",
            null, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveRefreshToken_NotFound_ReturnsError()
    {
        TenantContext.CurrentTenantId = "t_notfound";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("refresh_notfound").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("p", out string? salt);
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = new(db, ctx,
            new AuthenticateRepository<TestDbContext, TestPerm>(helper, db, ctx, new TestLicenseGuard(), info,
                new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache())));
        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "p" }, info, helper,
            new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache()),
            CancellationToken.None);
        RefreshTokenRequestModel req = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = "missing"
        };
        MResponse<RefreshTokenResponseModel> result = new();
        result = await db.ResolveRefreshToken(req, result, info, helper, user,
            new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache()), "en",
            null, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveRefreshToken_Token_Ttl_Expired_Returns_Error()
    {
        TenantContext.CurrentTenantId = "t_ttl";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("refresh_ttl_2").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("p", out string? salt);
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = new(db, ctx,
            new AuthenticateRepository<TestDbContext, TestPerm>(helper, db, ctx, new TestLicenseGuard(), info,
                new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache())));
        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "p" }, info, helper,
            new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache()),
            CancellationToken.None);
        MRefreshToken token = await db.RefreshTokens
            .SingleAsync(t => t.Token == login.Result!.RefreshToken);
        token.CreationTime = Clock.UtcNow.AddMinutes(-(info.RefreshTokenTtl + 1));
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync();
        RefreshTokenRequestModel req = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> result = new();
        result = await db.ResolveRefreshToken(req, result, info, helper, user,
            new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache()), "en",
            null, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveLoginAsync_ExpiredLock_ResetsAttempt()
    {
        TenantContext.CurrentTenantId = "t_unlock";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("login_unlock").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("p", out string? salt);
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
            LockTo = Clock.UtcNow.AddMinutes(-1)
        };
        await db.MUserLoginAttempts.AddAsync(attempt);
        await db.SaveChangesAsync();

        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        LoginRequestModel req = new() { Username = "u", Password = "p" };
        MResponse<LoginResponseModel> result = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        result = await db.ResolveLoginAsync(req, result, user, info, helper, cache, "en",
            null, CancellationToken.None);

        MUserLoginAttempt updated = await db.MUserLoginAttempts.FirstAsync();
        Assert.True(result.IsOk);
        Assert.True(user.IsActive);
        Assert.Equal(0, updated.AttemptTime);
        Assert.Equal(DateTime.MinValue, updated.LockTo);
    }

    [Fact]
    public async Task ResolveTokenValidity_EmptyKey_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("validity_empty").Options;
        using TestDbContext db = new(options);
        MResponse<string> result = await db.ResolveTokenValidity(string.Empty, "en", CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveTokenValidity_DbException_ReturnsError()
    {
        TenantContext.CurrentTenantId = "t_fail";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("validity_exception").Options;
        TestDbContext db = new(options);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = "k",
            CreatorUserId = Guid.NewGuid(),
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();
        await db.DisposeAsync();

        MResponse<string> result = await db.ResolveTokenValidity("k", "en", CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveRefreshToken_Includes_Extra_Claims()
    {
        TenantContext.CurrentTenantId = "t_extra";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("refresh_extra_claim").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("p", out string? salt);
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

        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = new(db, ctx,
            new AuthenticateRepository<TestDbContext, TestPerm>(helper, db, ctx, new TestLicenseGuard(), info,
                new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache())));
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());

        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "p" }, info, helper, cache,
            CancellationToken.None);
        RefreshTokenRequestModel req = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> result = new();
        List<Claim> extraClaims = [new Claim("extra", "1")];
        result = await db.ResolveRefreshToken(req, result, info, helper, user, cache,
            ctx.Language, extraClaims, CancellationToken.None);

        MethodInfo extract = GetPrivate("ExtractClaimsFromToken");
        List<Claim> claims = (List<Claim>)extract.Invoke(null, [result.Result!.AccessToken])!;
        Assert.Contains(claims, c => c is { Type: "extra", Value: "1" });
    }

    [Fact]
    public async Task ResolveRefreshToken_TtlExpired_ReturnsError()
    {
        TenantContext.CurrentTenantId = "t_ttl";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("refresh_ttl_3").Options;
        using TestDbContext db = new(options);
        string pwd = MPasswordHelper.HashPassword("p", out string? salt);
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

        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = new(db, ctx,
            new AuthenticateRepository<TestDbContext, TestPerm>(helper, db, ctx, new TestLicenseGuard(), info,
                new MultiLevelCacheService(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache())));
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());

        MResponse<LoginResponseModel> login = await svc.LoginAsync(new LoginRequestModel { Username = "u", Password = "p" }, info, helper, cache,
            CancellationToken.None);
        MRefreshToken token = await db.RefreshTokens
            .SingleAsync(t => t.Token == login.Result!.RefreshToken);
        token.CreationTime = Clock.UtcNow.AddMinutes(-(info.RefreshTokenTtl + 1));
        db.RefreshTokens.Update(token);
        await db.SaveChangesAsync();

        RefreshTokenRequestModel req = new()
        {
            AccessToken = login.Result!.AccessToken,
            RefreshToken = login.Result.RefreshToken
        };
        MResponse<RefreshTokenResponseModel> result = new();
        result = await db.ResolveRefreshToken(req, result, info, helper, user, cache,
            ctx.Language, null, CancellationToken.None);
        Assert.False(result.IsOk);
    }
}



