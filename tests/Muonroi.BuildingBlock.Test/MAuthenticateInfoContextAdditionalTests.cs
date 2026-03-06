namespace Muonroi.BuildingBlock.Test;

public class MAuthenticateInfoContextAdditionalTests
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
    public void Ctor_Item_NotBool_Defaults_False()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = "yes";
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
            .UseInMemoryDatabase("item_not_bool").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.False(ctx.IsAuthenticated);
    }

    [Fact]
    public void Ctor_Multi_Language_Header_Parses_First()
    {
        DefaultHttpContext http = new();
        http.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] = false;
        http.Request.Headers.AcceptLanguage = "en-US,en;q=0.8,vi-VN;q=0.7";
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
            .UseInMemoryDatabase("lang_multi").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.Equal("en-US", ctx.Language);
        Assert.Equal("en-US", setting[ResourceSettingKeys.Lang]);
    }

    [Fact]
    public void Ctor_User_Not_Found_Marks_NotAuthenticated()
    {
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        string token = helper.GenerateAuthenticateToken(
            new MUserModel(Guid.NewGuid().ToString(), "u", Guid.NewGuid().ToString(), "name", "surname", "phone",
                "email"), [TestPerm.One]);

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
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("user_not_found").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.False(ctx.IsAuthenticated);
        Assert.Null(ctx.CurrentUser);
        Assert.Equal(string.Empty, ctx.TokenValidityKey);
    }

    [Fact]
    public void Ctor_No_RefreshToken_Sets_Empty_Key()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("no_refresh").Options;
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
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, Guid.NewGuid().ToString(), user.Name,
            user.Surname, user.PhoneNumber, user.EmailAddress);
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One]);

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

        Assert.Equal(string.Empty, ctx.TokenValidityKey);
    }

    [Fact]
    public void Ctor_Computes_Permission_Bitmask()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("perm_bitmask").Options;
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
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        db.Roles.Add(role);
        MPermission p1 = new()
        {
            Name = SamplePermission.Read.ToString(),
            IsGranted = true
        };
        MPermission p2 = new()
        {
            Name = SamplePermission.Write.ToString(),
            IsGranted = true
        };
        db.Permissions.AddRange(p1, p2);
        db.SaveChanges();
        MUserRole entity = new()
        {
            UserId = user.EntityId,
            RoleId = role.EntityId
        };
        db.UserRoles.Add(entity);
        MRolePermission permission = new()
        {
            RoleId = role.EntityId,
            PermissionId = p1.EntityId
        };
        db.RolePermissions.AddRange(permission,
            new MRolePermission { RoleId = role.EntityId, PermissionId = p2.EntityId });
        db.SaveChanges();
        string validity = Guid.NewGuid().ToString();
        MRefreshToken refreshToken = new()
        {
            Token = "r",
            TokenValidityKey = validity,
            CreatorUserId = user.EntityId,
            CreationTime = Clock.UtcNow,
            LastUsedDate = Clock.UtcNow,
            ExpiredDate = Clock.UtcNow.AddMinutes(10)
        };
        db.RefreshTokens.Add(refreshToken);
        db.SaveChanges();
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

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

        Assert.Equal(((long)SamplePermission.Read | (long)SamplePermission.Write).ToString(), ctx.Permission);
    }

    [Fact]
    public void Ctor_AmqpContext_Missing_Headers_Uses_Defaults()
    {
        AmqpContext amqp = new();
        MAuthenticateInfoContext ctx = new(amqp);

        Assert.NotEqual(string.Empty, ctx.CorrelationId);
        Assert.Equal(string.Empty, ctx.CurrentUserGuid);
        Assert.Equal(string.Empty, ctx.CurrentUsername);
        Assert.Equal(string.Empty, ctx.GetAccessToken());
    }
}

