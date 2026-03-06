namespace Muonroi.BuildingBlock.Test;

public class MAuthenticateInfoContextTests
{
    [Fact]
    public void Ctor_IHttpContextAccessor_NullContext_Uses_Defaults()
    {
        HttpContextAccessor accessor = new()
        {
            HttpContext = null
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder().Build();
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ctx_null").Options;
        using TestDbContext db = new(opts);

        MAuthenticateInfoContext ctx = new(accessor, setting, config, db);

        Assert.False(ctx.IsAuthenticated);
        Assert.Equal(string.Empty, ctx.CorrelationId);
        Assert.Null(ctx.CurrentUser);
    }

    [Fact]
    public void Ctor_AmqpContext_Populates_From_Headers()
    {
        AmqpContext amqp = new();
        Dictionary<string, object> headers = new()
        {
            [CustomHeader.CorrelationId] = Encoding.UTF8.GetBytes("c1"),
            [ClaimConstants.UserIdentifier] = Encoding.UTF8.GetBytes("u1"),
            [ClaimConstants.Username] = Encoding.UTF8.GetBytes("user"),
            [ClaimConstants.AccessToken] = Encoding.UTF8.GetBytes("tok")
        };
        amqp.AddHeaders(headers);

        MAuthenticateInfoContext ctx = new(amqp);

        Assert.Equal("c1", ctx.CorrelationId);
        Assert.Equal("u1", ctx.CurrentUserGuid);
        Assert.Equal("user", ctx.CurrentUsername);
        Assert.Equal("tok", ctx.GetAccessToken());
    }

    [Fact]
    public void GetCurrentUser_Returns_Set_Value_Or_Null()
    {
        MAuthenticateInfoContext ctx = new(false);
        Assert.Null(ctx.CurrentUser);

        MUser user = new()
        {
            UserName = "u"
        };
        ctx.CurrentUser = user;
        Assert.Same(user, ctx.CurrentUser);
    }

    [Fact]
    public void GetAccessToken_Returns_Token_Or_Empty()
    {
        MAuthenticateInfoContext ctx = new(false)
        {
            AccessToken = null
        };
        Assert.Equal(string.Empty, ctx.GetAccessToken());

        ctx.AccessToken = "token";
        Assert.Equal("token", ctx.GetAccessToken());
    }

    private static T? InvokeGetClaimValue<T>(List<Claim> claims, string? type)
    {
        MethodInfo mi = typeof(MAuthenticateInfoContext)
            .GetMethod("GetClaimValue", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T));
        return (T?)mi.Invoke(null, [claims, type!]);
    }

    private static List<Claim> InvokeExtractClaims(string token)
    {
        MethodInfo mi = typeof(MAuthenticateInfoContext)
            .GetMethod("ExtractClaimsFromToken", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (List<Claim>)mi.Invoke(null, [token])!;
    }

    [Fact]
    public void GetClaimValue_Various_Cases()
    {
        List<Claim> claims = [new(ClaimConstants.Username, "admin")];
        string? value = InvokeGetClaimValue<string>(claims, ClaimConstants.Username);
        string? missing = InvokeGetClaimValue<string>(claims, "missing");
        string? empty = InvokeGetClaimValue<string>(claims, null!);
        Assert.Equal("admin", value);
        Assert.Null(missing);
        Assert.Null(empty);
    }

    private static (MTokenInfo Info, MAuthenticateTokenHelper<TestPerm> Helper) CreateHelper()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "iss",
            Audience = "aud",
            ExpiryMinutes = 60,
            RefreshTokenTtl = 5,
            RefreshTokenEim = 5,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        return (info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)));
    }

    [Fact]
    public void ExtractClaimsFromToken_Various_Cases()
    {
        TenantContext.CurrentTenantId = "t1";
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateHelper();
        string token = helper.GenerateAuthenticateToken(
            new MUserModel("guid", "user", "valid", "name", "surname", "phone", "email"), [TestPerm.One]);
        List<Claim> claims = InvokeExtractClaims(token);
        Assert.Contains(claims, c => c is { Type: ClaimConstants.UserIdentifier, Value: "guid" });

        List<Claim> invalid = InvokeExtractClaims("invalid");
        Assert.Empty(invalid);
    }

    [Fact]
    public void ExtractClaimsFromToken_Empty_Returns_Empty()
    {
        List<Claim> empty = InvokeExtractClaims(string.Empty);
        Assert.Empty(empty);
    }

    [Fact]
    public void GetClaimValue_NullClaims_Throws()
    {
        MethodInfo mi = typeof(MAuthenticateInfoContext)
            .GetMethod("GetClaimValue", BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = mi.MakeGenericMethod(typeof(string));
        Assert.Throws<TargetInvocationException>(() => generic.Invoke(null, [null!, ClaimConstants.Username]));
    }

    [Fact]
    public void ExtractClaimsFromToken_Null_Returns_Empty()
    {
        MethodInfo mi = typeof(MAuthenticateInfoContext)
            .GetMethod("ExtractClaimsFromToken", BindingFlags.NonPublic | BindingFlags.Static)!;
        List<Claim> claims = (List<Claim>)mi.Invoke(null, [null!])!;
        Assert.Empty(claims);
    }
}

