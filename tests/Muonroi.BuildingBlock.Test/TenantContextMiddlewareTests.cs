namespace Muonroi.BuildingBlock.Test;

using Muonroi.Governance.License;

public class TenantContextMiddlewareTests
{
    private class StaticTenantResolver : ITenantIdResolver
    {
        public Task<string?> ResolveTenantIdAsync(HttpContext context)
        {
            return Task.FromResult<string?>("resolvedTenant");
        }
    }

    private class NullTenantResolver : ITenantIdResolver
    {
        public Task<string?> ResolveTenantIdAsync(HttpContext context)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FeatureGuard(bool hasMultiTenantFeature) : ILicenseGuard
    {
        public LicenseState Current => LicenseState.CreateFree();
        public LicenseTier Tier => Current.Tier;
        public bool IsFreeMode => true;
        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            if (string.Equals(featureName, FreeTierFeatures.Premium.MultiTenant, StringComparison.OrdinalIgnoreCase))
                return hasMultiTenantFeature;

            return true;
        }

        public void EnsureFeature(string featureName)
        {
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => string.Empty;

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("test", encryptedData);
        }
    }

    [Fact]
    public async Task Invoke_SetsAndClears_TenantId()
    {
        DefaultHttpContext context = new();
        bool nextCalled = false;

        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            Assert.Equal("resolvedTenant", TenantContext.CurrentTenantId);
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(Next, new StaticTenantResolver());
        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task Invoke_MultiTenantEnabled_WithoutTenant_Returns401()
    {
        DefaultHttpContext context = new();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected"));

        bool nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new NullTenantResolver(),
            new FeatureGuard(true),
            Options.Create(new MultiTenantConfigs { Enabled = true }));

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MultiTenantEnabled_WithoutFeature_Returns403()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-1");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected"));

        bool nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new StaticTenantResolver(),
            new FeatureGuard(false),
            Options.Create(new MultiTenantConfigs { Enabled = true }));

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MultiTenantEnabled_WithClaimHeaderMismatch_Returns401()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-header");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-claim")
        ], "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected"));

        bool nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new StaticTenantResolver(),
            new FeatureGuard(true),
            Options.Create(new MultiTenantConfigs { Enabled = true }));

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MultiTenantEnabled_WithoutEndpoint_DoesNotBlock()
    {
        DefaultHttpContext context = new();
        bool nextCalled = false;

        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new NullTenantResolver(),
            new FeatureGuard(true),
            Options.Create(new MultiTenantConfigs { Enabled = true }));

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MultiTenantEnabled_AuthenticatedWithoutTenantClaim_Returns401()
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected"));

        bool nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new StaticTenantResolver(),
            new FeatureGuard(true),
            Options.Create(new MultiTenantConfigs { Enabled = true, RequireTenantClaimForAuthenticatedUser = true }));

        await middleware.Invoke(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_MultiTenantEnabled_RequireTenantClaimDisabled_AllowsAuthenticatedWithoutClaim()
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected"));

        bool nextCalled = false;
        Task Next(HttpContext _)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new StaticTenantResolver(),
            new FeatureGuard(true),
            Options.Create(new MultiTenantConfigs { Enabled = true, RequireTenantClaimForAuthenticatedUser = false }));

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}
