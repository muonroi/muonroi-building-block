namespace Muonroi.BuildingBlock.Test;

public class MAuthenticateInfoContextConstructorTests
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

    [Fact]
    public void Ctor_With_Full_Context_Populates_Properties()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_full").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p",
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
        string validity = Guid.NewGuid().ToString();
        MRefreshToken entity = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        db.RefreshTokens.Add(entity);
        db.SaveChanges();
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = true;
        http.Request.Headers.Authorization = token;
        http.Request.Headers.AcceptLanguage = "en-US";
        http.Request.Headers[CustomHeader.CorrelationId] = "cid";

        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                [ClaimConstants.ApiKey] = "api"
            })
            .Build();

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.True(ctx.IsAuthenticated);
        Assert.Equal("cid", ctx.CorrelationId);
        Assert.Equal(user.EntityId.ToString(), ctx.CurrentUserGuid);
        Assert.Equal(user.UserName, ctx.CurrentUsername);
        Assert.Equal(validity, ctx.TokenValidityKey);
        Assert.NotNull(ctx.CurrentUser);
    }

    [Fact]
    public void Ctor_Null_HttpContextAccessor_Throws()
    {
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                [ClaimConstants.ApiKey] = "api"
            })
            .Build();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_null1").Options;
        using TestDbContext db = new(options);

        Assert.Throws<NullReferenceException>(() => new MAuthenticateInfoContext(null!, setting, config, db));
    }

    [Fact]
    public void Ctor_NotAuthenticated_Uses_ApiKey_And_Defaults()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = false;
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                [ClaimConstants.ApiKey] = "api123"
            })
            .Build();
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_notauth").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.False(ctx.IsAuthenticated);
        Assert.Equal("api123", ctx.ApiKey);
        Assert.Equal("vi-VN", ctx.Language);
        Assert.Equal(string.Empty, ctx.CurrentUserGuid);
        Assert.Null(ctx.CurrentUser);
        Assert.Equal(ctx.Language, setting[ResourceSettingKeys.Lang]);
    }

    [Fact]
    public void Ctor_Null_Configuration_Throws()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = false;
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_null_config").Options;
        using TestDbContext db = new(opts);

        Assert.Throws<NullReferenceException>(() => new MAuthenticateInfoContext(accessor, setting, null!, db));
    }

    [Fact]
    public void Ctor_Missing_ApiKey_Sets_Empty_String()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = false;
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false"
            })
            .Build();
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_missing_api").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);
        Assert.Equal(string.Empty, ctx.ApiKey);
    }

    [Fact]
    public void Ctor_Null_ResourceSetting_Throws()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = false;
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                [ClaimConstants.ApiKey] = "api"
            })
            .Build();
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_null_setting").Options;
        using TestDbContext db = new(opts);

        Assert.Throws<NullReferenceException>(() => new MAuthenticateInfoContext(accessor, null!, config, db));
    }

    [Fact]
    public void Ctor_Authenticated_Invalid_Token_Sets_NoUser()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = true;
        http.Request.Headers.Authorization = "invalid";
        http.Request.Headers[CustomHeader.CorrelationId] = "cid2";
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                [ClaimConstants.ApiKey] = "api"
            })
            .Build();
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_invalid_token").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.True(ctx.IsAuthenticated);
        Assert.Equal(string.Empty, ctx.CurrentUserGuid);
        Assert.Null(ctx.CurrentUser);
        Assert.Equal("api", ctx.ApiKey);
    }

    [Fact]
    public void Ctor_User_NotActive_Marks_NotAuthenticated()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctor_inactive").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p",
            IsActive = false
        };
        db.Users.Add(user);
        db.SaveChanges();
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        string token = helper.GenerateAuthenticateToken(
            new MUserModel(user.EntityId.ToString(), user.UserName, Guid.NewGuid().ToString(), user.Name,
                user.Surname, user.PhoneNumber, user.EmailAddress), [TestPerm.One]);

        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = true;
        http.Request.Headers.Authorization = token;
        http.Request.Headers[CustomHeader.CorrelationId] = "cid";
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                [ClaimConstants.ApiKey] = "api"
            })
            .Build();

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.False(ctx.IsAuthenticated);
        Assert.Equal(string.Empty, ctx.ApiKey);
        Assert.Null(ctx.CurrentUser);
    }
}

