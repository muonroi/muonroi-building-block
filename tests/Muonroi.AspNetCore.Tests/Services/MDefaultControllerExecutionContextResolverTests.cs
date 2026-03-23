namespace Muonroi.AspNetCore.Tests.Services;

public class MDefaultControllerExecutionContextResolverTests
{
    private readonly IConfiguration _configuration;

    public MDefaultControllerExecutionContextResolverTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UiEngineLab:TenantId"] = "lab-tenant",
                ["UiEngineLab:TenantTier"] = "Professional"
            })
            .Build();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        return httpContext;
    }

    [Fact]
    public void Resolve_AnonymousUser_ReturnsDefaultContext()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();

        var result = resolver.Resolve(httpContext);

        result.IsAuthenticated.Should().BeFalse();
        result.Actor.Should().Be("anonymous");
        result.TenantTier.Should().Be("Professional");
    }

    [Fact]
    public void Resolve_WithClaimsPrincipal_ExtractsUserInfo()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Username, "testuser"),
            new Claim(ClaimConstants.UserIdentifier, userId.ToString()),
            new Claim(ClaimConstants.TenantId, "claim-tenant")
        ], "TestAuth"));

        var result = resolver.Resolve(httpContext);

        result.Username.Should().Be("testuser");
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be("claim-tenant");
        result.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WithHeaders_ExtractsFromHeaders()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers["X-Username"] = "header-user";
        httpContext.Request.Headers["X-Tenant-Id"] = "header-tenant";
        httpContext.Request.Headers["X-Tenant-Tier"] = "Enterprise";
        httpContext.Request.Headers["X-Actor"] = "ci-bot";

        var result = resolver.Resolve(httpContext);

        result.Username.Should().Be("header-user");
        result.TenantId.Should().Be("header-tenant");
        result.TenantTier.Should().Be("Enterprise");
        result.Actor.Should().Be("ci-bot");
    }

    [Fact]
    public void Resolve_WithPermissionsHeader_ParsesPermissions()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers["X-Permissions"] = "read,write|admin";

        var result = resolver.Resolve(httpContext);

        result.Permissions.Should().Contain("read");
        result.Permissions.Should().Contain("write");
        result.Permissions.Should().Contain("admin");
    }

    [Fact]
    public void Resolve_WithRoleClaims_IncludedInPermissions()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Manager"),
            new Claim(ClaimTypes.Role, "Viewer")
        ]));

        var result = resolver.Resolve(httpContext);

        result.Permissions.Should().Contain("Manager");
        result.Permissions.Should().Contain("Viewer");
    }

    [Fact]
    public void Resolve_NoTenantId_FallsBackToConfig()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();

        var result = resolver.Resolve(httpContext);

        result.TenantId.Should().Be("lab-tenant");
    }

    [Fact]
    public void Resolve_EmptyTenantId_DefaultsToGlobal()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var resolver = new MDefaultControllerExecutionContextResolver(config);
        var httpContext = CreateHttpContext();

        var result = resolver.Resolve(httpContext);

        result.TenantId.Should().Be("_global");
    }

    [Fact]
    public void Resolve_NoTenantTier_DefaultsToFree()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var resolver = new MDefaultControllerExecutionContextResolver(config);
        var httpContext = CreateHttpContext();

        var result = resolver.Resolve(httpContext);

        result.TenantTier.Should().Be("Free");
    }

    [Fact]
    public void Resolve_AuthContext_TakesPriorityOverClaims()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();

        var authContext = Substitute.For<IAuthenticateInfoContext>();
        authContext.CurrentUsername.Returns("auth-user");
        authContext.CurrentUserGuid.Returns(Guid.NewGuid().ToString());
        authContext.TenantId.Returns("auth-tenant");
        authContext.IsAuthenticated.Returns(true);

        httpContext.RequestServices = CreateServiceProvider(authContext);

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Username, "claim-user")
        ]));

        var result = resolver.Resolve(httpContext);

        result.Username.Should().Be("auth-user");
        result.TenantId.Should().Be("auth-tenant");
    }

    [Fact]
    public void Resolve_InvalidGuidUserId_ReturnsNull()
    {
        var resolver = new MDefaultControllerExecutionContextResolver(_configuration);
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "not-a-guid";

        var result = resolver.Resolve(httpContext);

        result.UserId.Should().BeNull();
    }

    private static IServiceProvider CreateServiceProvider(IAuthenticateInfoContext authContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authContext);
        return services.BuildServiceProvider();
    }
}
