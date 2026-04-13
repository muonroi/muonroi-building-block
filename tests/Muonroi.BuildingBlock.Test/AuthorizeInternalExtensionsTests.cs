namespace Muonroi.BuildingBlock.Test;

public class AuthorizeInternalExtensionsTests
{
    private static T? InvokeGetClaimValue<T>(List<Claim> claims, string? type)
    {
        MethodInfo method = typeof(AuthorizeInternal)
            .GetMethod("GetClaimValue", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T));
        return (T?)method.Invoke(null, [claims, type!]);
    }

    private static List<Claim> InvokeExtractClaims(string token)
    {
        MethodInfo method = typeof(AuthorizeInternal)
            .GetMethod("ExtractClaimsFromToken", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (List<Claim>)method.Invoke(null, [token])!;
    }

    private static (MTokenInfo Info, MAuthenticateTokenHelper<TestPerm> Helper) CreateTokenHelper(
        int expiryMinutes = 60)
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = expiryMinutes,
            RefreshTokenTtl = 5,
            RefreshTokenEim = 5,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        return (info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)));
    }

    [Fact]
    public void GetClaimValue_Returns_Value_When_Exists()
    {
        List<Claim> claims = [new(ClaimConstants.Username, "admin")];
        string? value = InvokeGetClaimValue<string>(claims, ClaimConstants.Username);
        Assert.Equal("admin", value);
    }

    [Fact]
    public void GetClaimValue_Returns_Default_When_Missing()
    {
        List<Claim> claims = [new(ClaimConstants.Username, "admin")];
        string? value = InvokeGetClaimValue<string>(claims, "missing");
        Assert.Null(value);
    }

    [Fact]
    public void GetClaimValue_Returns_Default_When_Value_Empty()
    {
        List<Claim> claims = [new(ClaimConstants.Username, "")];
        string? value = InvokeGetClaimValue<string>(claims, ClaimConstants.Username);
        Assert.Null(value);
    }

    [Fact]
    public void GetClaimValue_Returns_Default_For_Empty_Key()
    {
        List<Claim> claims = [new(ClaimConstants.Username, "value")];
        string? value1 = InvokeGetClaimValue<string>(claims, "");
        string? value2 = InvokeGetClaimValue<string>(claims, null!);
        Assert.Null(value1);
        Assert.Null(value2);
    }

    [Fact]
    public void ExtractClaimsFromToken_Returns_Claims_For_Valid_Token()
    {
        TenantContext.CurrentTenantId = "t1";
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string token = helper.GenerateAuthenticateToken(
            new MUserModel("guid", "user", "val", "name", "surname", "phone", "email"), [TestPerm.One]);
        List<Claim> claims = InvokeExtractClaims("Bearer " + token);
        Assert.Contains(claims, c => c is { Type: ClaimConstants.UserIdentifier, Value: "guid" });
    }

    [Fact]
    public void ExtractClaimsFromToken_Returns_Empty_For_Invalid_Token()
    {
        List<Claim> claims = InvokeExtractClaims("invalid");
        Assert.Empty(claims);
    }

    [Fact]
    public void ExtractClaimsFromToken_Allows_Expired_Token()
    {
        TenantContext.CurrentTenantId = "t1";
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes("testkey123456789012345678901234567890"));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken jwt = new(
            "issuer",
            "audience",
            [new Claim(ClaimConstants.UserIdentifier, "guid")],
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5),
            creds);
        string token = new JwtSecurityTokenHandler().WriteToken(jwt);
        List<Claim> claims = InvokeExtractClaims(token);
        Assert.NotEmpty(claims);
    }

    [Fact]
    public void PermissionDelegate_Returns_String()
    {
        static string Del(TestPerm p)
        {
            return p.ToString();
        }

        string result = Del(TestPerm.One);
        Assert.Equal("One", result);
    }

    [Fact]
    public void PermissionDelegate_Returns_Numeric_For_Invalid()
    {
        static string Del(TestPerm p)
        {
            return p.ToString();
        }

        string result = Del((TestPerm)999);
        Assert.Equal("999", result);
    }

    [Fact]
    public async Task ResolveTokenFromHttpContext_Success()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("ctx_success").Options;
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();

        DefaultHttpContext ctx = new();
        ctx.Request.Headers.Authorization = token;
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());

        MRefreshToken? result = await db.ResolveTokenFromHttpContext(ctx, cache, null, tokenInfo: info);
        Assert.NotNull(result);
        Assert.Equal(validity, ctx.Items[nameof(MAuthenticateInfoContext.TokenValidityKey)]);
    }

    [Fact]
    public async Task ResolveTokenFromHttpContext_Missing_Header_Returns_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("ctx_missing").Options;
        using TestDbContext db = new(options);
        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenFromHttpContext(ctx, cache, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveTokenFromHttpContext_Invalid_Token()
    {
        TenantContext.CurrentTenantId = "t1";
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("ctx_invalid").Options;
        using TestDbContext db = new(options);
        DefaultHttpContext ctx = new();
        ctx.Request.Headers.Authorization = "Bearer invalid";
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenFromHttpContext(ctx, cache, null, tokenInfo: info);
        Assert.Null(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolveTokenFromHttpContext_Null_Context_Throws()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("ctx_null").Options;
        using TestDbContext db = new(options);
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
            await db.ResolveTokenFromHttpContext(null!, cache, null));
    }

    [Fact]
    public async Task ResolveTokenValidity_Returns_Token()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_success").Options;
        using TestDbContext db = new(options);
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
        MResponse<string> result = await db.ResolveTokenValidity("k", "en", CancellationToken.None);
        Assert.True(result.IsOk);
        Assert.Equal("r", result.Result);
    }

    [Fact]
    public async Task ResolveTokenValidity_Invalid_Key()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_invalid").Options;
        using TestDbContext db = new(options);
        MResponse<string> result = await db.ResolveTokenValidity("missing", "en", CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveTokenValidity_Empty_Key_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_empty").Options;
        using TestDbContext db = new(options);
        MResponse<string> result = await db.ResolveTokenValidity(string.Empty, "en", CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task ResolveTokenValidity_Key_Expired()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_expired").Options;
        using TestDbContext db = new(options);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = "k",
            CreatorUserId = Guid.NewGuid(),
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(-5),
            IsRevoked = true
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();
        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey("Bearer invalidtoken", ctx, cache, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_Returns_RefreshToken_For_Valid_Token()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_key_success").Options;
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();

        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            null,
            tokenInfo: info);
        Assert.NotNull(result);
        Assert.Equal(validity, ctx.Items[nameof(MAuthenticateInfoContext.TokenValidityKey)]);
        Assert.Equal(refresh.TokenValidityKey, result!.TokenValidityKey);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_ForgedSignature_Returns_Unauthorized()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_key_forged").Options;
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

        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        MTokenInfo forgedInfo = new()
        {
            SymmetricSecretKey = "anotherkey12345678901234567890123456",
            Issuer = info.Issuer,
            Audience = info.Audience,
            ExpiryMinutes = info.ExpiryMinutes,
            RefreshTokenTtl = info.RefreshTokenTtl,
            RefreshTokenEim = info.RefreshTokenEim,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        MAuthenticateTokenHelper<TestPerm> forgedHelper = new(
            forgedInfo,
            new HmacTokenSigner(forgedInfo.SymmetricSecretKey));

        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string forgedToken = forgedHelper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

        MRefreshToken token = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        await db.RefreshTokens.AddAsync(token);
        await db.SaveChangesAsync();

        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey(
            "Bearer " + forgedToken,
            ctx,
            cache,
            null,
            tokenInfo: info);

        Assert.Null(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_Null_Token_Returns_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_key_null").Options;
        using TestDbContext db = new(options);
        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey(null!, ctx, cache, null);
        Assert.Null(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_Invalid_UserGuid_Returns_Unauthorized()
    {
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new("notguid", "u", validity, "name", "surname", "phone", "email");
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_key_bad_user").Options;
        using TestDbContext db = new(options);
        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            null,
            tokenInfo: info);
        Assert.Null(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_RevokedToken_Returns_Forbidden()
    {
        TenantContext.CurrentTenantId = "t_revoked";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_key_revoked").Options;
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10),
            IsRevoked = true
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();

        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            null,
            tokenInfo: info);
        Assert.Null(result);
        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_MissingValidity_Returns_Unauthorized()
    {
        TenantContext.CurrentTenantId = "t_missing";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("validity_key_missing").Options;
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
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, Guid.NewGuid().ToString(), user.Name,
            user.Surname, user.PhoneNumber, user.EmailAddress);
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One]);

        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        MRefreshToken? result = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            null,
            tokenInfo: info);
        Assert.Null(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_LogsWarning_WhenUserIdentifierInvalid()
    {
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new("notguid", "u", validity, "name", "surname", "phone", "email");
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("key_bad_user_warn").Options;
        using TestDbContext db = new(options);
        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        Mock<ILogger> logger = new();

        _ = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            logger.Object,
            tokenInfo: info);

        logger.Verify(l => l.Warning(It.Is<string>(s => s.Contains("Invalid user identifier in token"))), Times.Once);
    }

    [Fact]
    public async Task ResolveTokenValidityKey_LogsWarning_WhenRefreshTokenNotFound()
    {
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(Guid.NewGuid().ToString(), "u", validity, "name", "surname", "phone", "email");
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("key_refresh_null_warn").Options;
        using TestDbContext db = new(options);
        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        Mock<ILogger> logger = new();

        _ = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            logger.Object,
            tokenInfo: info);

        logger.Verify(l =>
                l.Warning(
                    It.Is<string>(msg => msg.Contains("Refresh token not found for user")),
                    It.IsAny<Guid>()),
            Times.AtLeastOnce
        );
    }


    [Theory]
    [InlineData(false, true, "Attempt using revoked token for user")]
    [InlineData(true, false, "Refresh token not found for user")]
    [InlineData(true, true, "Refresh token not found for user")]
    public async Task ResolveTokenValidityKey_LogsWarning_WhenTokenRevokedOrDeleted(
        bool isDeleted, bool isRevoked, string expectedMessage)
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = Guid.NewGuid().ToString(),
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);
        MRefreshToken refresh = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10),
            IsRevoked = isRevoked,
            IsDeleted = isDeleted
        };
        await db.RefreshTokens.AddAsync(refresh);
        await db.SaveChangesAsync();

        DefaultHttpContext ctx = new();
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        Mock<ILogger> logger = new();

        _ = await db.ResolveTokenValidityKey(
            "Bearer " + token,
            ctx,
            cache,
            logger.Object,
            tokenInfo: info);

        logger.Verify(l =>
                l.Warning(
                    It.Is<string>(msg => msg.Contains(expectedMessage)),
                    It.IsAny<Guid>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ResolveTokenValidity_WhenExceptionThrown_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("resolve_token_exception_test").Options;

        TestDbContext db = new(options);
        db.Dispose();

        MResponse<string> result = await db.ResolveTokenValidity("any-key",
            "en",
            CancellationToken.None
        );

        Assert.False(result.IsOk);
        Assert.Contains(result.ErrorMessages, e => e.ErrorCode == nameof(SystemEnum.InvalidCredentials));
    }

    [Fact]
    public async Task ResolveTokenValidity_WhenQueryFails_ThrowsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("Filename=:memory:").Options;
        using TestDbContext db = new(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();

        db.Database.GetDbConnection().Dispose();

        MResponse<string> result = await db.ResolveTokenValidity("test-key",
            "en",
            CancellationToken.None
        );

        Assert.False(result.IsOk);
        Assert.Contains(result.ErrorMessages, e => e.ErrorCode == nameof(SystemEnum.InvalidCredentials));
    }
}
