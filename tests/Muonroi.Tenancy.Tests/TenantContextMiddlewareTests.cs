namespace Muonroi.Tenancy.Tests;

public class TenantContextMiddlewareTests
{
    private sealed class StaticTenantResolver : ITenantIdResolver
    {
        public Task<string?> ResolveTenantIdAsync(HttpContext context)
        {
            return Task.FromResult<string?>("resolvedTenant");
        }
    }

    private sealed class NullTenantResolver : ITenantIdResolver
    {
        public Task<string?> ResolveTenantIdAsync(HttpContext context)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeLicenseGate(bool enabled) : ITenantLicenseFeatureGate
    {
        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, TenantLicenseFeatures.Premium.MultiTenant, StringComparison.OrdinalIgnoreCase) || enabled;
        }
    }

    private sealed class NullLogContext : IMLogContext
    {
        public IMLogContextScope PushProperty(string key, object? value) => NullScope.Instance;

        public IMLogContextScope PushProperties(IReadOnlyDictionary<string, object?> properties) => NullScope.Instance;

        private sealed class NullScope : IMLogContextScope
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public async Task Invoke_Sets_And_Clears_TenantId()
    {
        DefaultHttpContext context = new();
        bool nextCalled = false;

        Task Next(HttpContext _)
        {
            nextCalled = true;
            TenantContext.CurrentTenantId.Should().Be("resolvedTenant");
            return Task.CompletedTask;
        }

        TenantContextMiddleware middleware = new(
            Next,
            new StaticTenantResolver(),
            new NullLogContext());

        await middleware.Invoke(context);

        nextCalled.Should().BeTrue();
        TenantContext.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public async Task Invoke_Returns_401_When_MultiTenant_Enabled_And_Claim_Is_Required()
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "protected"));

        bool nextCalled = false;
        TenantContextMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new StaticTenantResolver(),
            new NullLogContext(),
            new FakeLicenseGate(true),
            Options.Create(new MultiTenantConfigs
            {
                Enabled = true,
                RequireTenantClaimForAuthenticatedUser = true
            }));

        await middleware.Invoke(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Invoke_Returns_403_When_License_Does_Not_Allow_MultiTenant()
    {
        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-1");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "protected"));

        bool nextCalled = false;
        TenantContextMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new StaticTenantResolver(),
            new NullLogContext(),
            new FakeLicenseGate(false),
            Options.Create(new MultiTenantConfigs { Enabled = true }));

        await middleware.Invoke(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
