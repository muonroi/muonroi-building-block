namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AuthServiceRegisterLogoutTests
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

    private static RegisterRequestModel CreateValidRegisterRequest()
    {
        RegisterRequestModel model = new()
        {
            UserName = "u",
            Password = "Passw0rd!",
            Email = "u@a.com",
            Name = "n",
            Surname = "s",
            PhoneNumber = "1",
            IsActive = true,
            IsTwoFactorEnabled = false,
            IsUseThirdPartyLogin = false
        };
        return model;
    }

    private static AuthService<TestPerm, TestDbContext> CreateRegisterService(TestDbContext db,
        out MAuthenticateInfoContext ctx)
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        ctx = new MAuthenticateInfoContext(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        return CreateService(db, ctx, cache, info, helper);
    }

    [Fact]
    public async Task Login_Success_Returns_Tokens()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_success").Options;
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
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_user_nf").Options;
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
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("login_invalid").Options;
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
    public async Task Register_Success_Returns_Token_And_Creates_User()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("register_success").Options;
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

        RegisterRequestModel request = new()
        {
            UserName = "u",
            Password = "pass123",
            Email = "u@a.com",
            Name = "n",
            Surname = "s",
            PhoneNumber = "1",
            IsActive = true,
            IsTwoFactorEnabled = false,
            IsUseThirdPartyLogin = false
        };

        MResponse<LoginResponseModel> result = await svc.RegisterAsync(request, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.False(string.IsNullOrEmpty(result.Result!.AccessToken));
    }

    [Fact]
    public async Task Register_User_Already_Exists_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("register_exists").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await db.Users.AddAsync(user);
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

        RegisterRequestModel request = new()
        {
            UserName = "u",
            Password = "pass123",
            Email = "u@a.com",
            Name = "n",
            Surname = "s"
        };
        MResponse<LoginResponseModel> result = await svc.RegisterAsync(request, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Register_MissingUserName_Returns_InvalidUserName_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("register_missing_username")
            .Options;
        using TestDbContext db = new(options);
        AuthService<TestPerm, TestDbContext> svc = CreateRegisterService(db, out _);
        RegisterRequestModel request = CreateValidRegisterRequest();
        request.UserName = string.Empty;

        MResponse<LoginResponseModel> result = await svc.RegisterAsync(request, CancellationToken.None);

        MErrorResult error = Assert.Single(result.ErrorMessages);
        Assert.Equal(nameof(SystemEnum.InvalidUserName), error.ErrorCode);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_MissingPassword_Returns_InvalidPassword_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("register_missing_password")
            .Options;
        using TestDbContext db = new(options);
        AuthService<TestPerm, TestDbContext> svc = CreateRegisterService(db, out _);
        RegisterRequestModel request = CreateValidRegisterRequest();
        request.Password = string.Empty;

        MResponse<LoginResponseModel> result = await svc.RegisterAsync(request, CancellationToken.None);

        MErrorResult error = Assert.Single(result.ErrorMessages);
        Assert.Equal(nameof(SystemEnum.InvalidPassword), error.ErrorCode);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_WeakPassword_Returns_InvalidPasswordStrength_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("register_weak_password")
            .Options;
        using TestDbContext db = new(options);
        AuthService<TestPerm, TestDbContext> svc = CreateRegisterService(db, out _);
        RegisterRequestModel request = CreateValidRegisterRequest();
        request.Password = "123";

        MResponse<LoginResponseModel> result = await svc.RegisterAsync(request, CancellationToken.None);

        MErrorResult error = Assert.Single(result.ErrorMessages);
        Assert.Equal(nameof(SystemEnum.InvalidPasswordStrength), error.ErrorCode);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns_InvalidEmailAddress_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("register_invalid_email")
            .Options;
        using TestDbContext db = new(options);
        AuthService<TestPerm, TestDbContext> svc = CreateRegisterService(db, out _);
        RegisterRequestModel request = CreateValidRegisterRequest();
        request.Email = "invalid-email";

        MResponse<LoginResponseModel> result = await svc.RegisterAsync(request, CancellationToken.None);

        MErrorResult error = Assert.Single(result.ErrorMessages);
        Assert.Equal(nameof(SystemEnum.InvalidEmailAddress), error.ErrorCode);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_Db_Error_Throws_Exception()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<FaultyDbContext> options = new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase("register_dberror")
            .Options;
        using FaultyDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, FaultyDbContext> svc = CreateFaultyService(db, ctx, cache, info, helper);

        RegisterRequestModel request = new()
        {
            UserName = "u",
            Password = "pass123",
            Email = "u@a.com",
            Name = "n",
            Surname = "s"
        };

        await Assert.ThrowsAsync<Exception>(() => svc.RegisterAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Logout_Success_Revokes_Token()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("logout_success").Options;
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

        MAuthenticateInfoContext ctxLogin = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svcLogin = CreateService(db, ctxLogin, cache, info, helper);
        MResponse<LoginResponseModel> login = await svcLogin.LoginAsync(new LoginRequestModel { Username = "u", Password = "correct" }, info,
            helper, cache, CancellationToken.None);
        string access = login.Result!.AccessToken;
        JwtSecurityToken jwt = new(access);
        string tokenValidity = jwt.Claims.First(c => c.Type == ClaimConstants.TokenValidityKey).Value;

        using TestDbContext db2 = new(options);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            TokenValidityKey = tokenValidity,
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = CreateService(db2, ctx, cache, info, helper);

        MResponse<object> result = await svc.LogoutAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        MRefreshToken token = await db2.RefreshTokens.AsNoTracking().FirstAsync();
        Assert.True(token.IsRevoked);
        Assert.Equal("Logout", token.ReasonRevoked);
    }

    [Fact]
    public async Task Logout_Token_Not_Found_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("logout_token_nf").Options;
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

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            TokenValidityKey = Guid.NewGuid().ToString(),
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<object> result = await svc.LogoutAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Logout_User_Not_Found_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("logout_user_nf").Options;
        using TestDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = Guid.NewGuid().ToString(),
            CurrentUsername = "u",
            TokenValidityKey = Guid.NewGuid().ToString(),
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<object> result = await svc.LogoutAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAll_Revokes_All_Tokens()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("logoutall_success").Options;
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

        MAuthenticateInfoContext ctxLogin = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svcLogin = CreateService(db, ctxLogin, cache, info, helper);
        for (int i = 0; i < 2; i++)
        {
            _ = await svcLogin.LoginAsync(new LoginRequestModel { Username = "u", Password = "correct" }, info, helper,
                cache, CancellationToken.None);
        }

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            Language = "en"
        };
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.All(db.RefreshTokens, t => Assert.True(t.IsRevoked));
        Assert.Equal(2, await db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task LogoutAll_No_Tokens_Returns_OK()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("logoutall_none").Options;
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

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task LogoutAll_Invalid_UserGuid_Returns_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("logoutall_invalid_id").Options;
        using TestDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = "not-a-guid",
            CurrentUsername = "u",
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> svc = CreateService(db, ctx, cache, info, helper);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task Logout_Db_Error_Throws_Exception()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        const string dbName = "logout_dberror";

        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using TestDbContext seed = new(seedOptions);
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

        MAuthenticateInfoContext loginCtx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> loginSvc = CreateService(seed, loginCtx, cache, info, helper);
        MResponse<LoginResponseModel> login = await loginSvc.LoginAsync(new LoginRequestModel { Username = "u", Password = "correct" }, info,
            helper, cache, CancellationToken.None);
        string access = login.Result!.AccessToken;
        JwtSecurityToken jwt = new(access);
        string tokenValidity = jwt.Claims.First(c => c.Type == ClaimConstants.TokenValidityKey).Value;

        DbContextOptions<FaultyDbContext> faultyOptions = new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using FaultyDbContext db = new(faultyOptions);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            CurrentUsername = "u",
            TokenValidityKey = tokenValidity,
            Language = "en"
        };
        AuthService<TestPerm, FaultyDbContext> svc = CreateFaultyService(db, ctx, cache, info, helper);

        await Assert.ThrowsAsync<Exception>(() => svc.LogoutAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LogoutAll_Db_Error_Throws_Exception()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        const string dbName = "logoutall_dberror";

        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using TestDbContext seed = new(seedOptions);
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

        MAuthenticateInfoContext loginCtx = new(false)
        {
            Language = "en"
        };
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        AuthService<TestPerm, TestDbContext> loginSvc = CreateService(seed, loginCtx, cache, info, helper);
        for (int i = 0; i < 2; i++)
        {
            _ = await loginSvc.LoginAsync(new LoginRequestModel { Username = "u", Password = "correct" }, info, helper,
                cache, CancellationToken.None);
        }

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

        await Assert.ThrowsAsync<Exception>(() => svc.LogoutAllAsync(CancellationToken.None));
    }
}

