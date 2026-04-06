using Microsoft.IdentityModel.Tokens;
using Muonroi.Core.Abstractions.Context;

namespace Muonroi.AspNetCore.Tests.Extensions;

/// <summary>
/// Proves that the AspNetCore middleware stack works correctly
/// when the Muonroi.Auth package is NOT registered in DI.
/// </summary>
public class NoAuthMiddlewareTests
{
    [Fact]
    public async Task BearerTokenSetup_WithoutAuth_ResolvesSignerAndSkipsRefresh()
    {
        // Arrange — register bearer token but NO Auth package (no IRefreshTokenValidator)
        var services = new ServiceCollection();
        services.AddLogging();

        var tokenConfig = new MTokenInfo
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SymmetricSecretKey = "test-key-at-least-32-characters-long!",
            UseRsa = false,
            ExpiryMinutes = 60
        };
        services.AddSingleton(tokenConfig);

        var config = BuildMinimalConfig();
        services.AddValidateBearerToken(config);

        using var sp = services.BuildServiceProvider();

        // Assert 1: ITokenSigner resolves to internal HmacTokenSigner
        var signer = sp.GetService<ITokenSigner>();
        signer.Should().NotBeNull("ITokenSigner should resolve to internal signer when Auth is absent");

        // Assert 2: Refresh delegate resolves and returns IsAuthenticated=false (no IRefreshTokenValidator)
        var refreshDelegate = sp.GetService<Func<IServiceProvider, HttpContext, Task<IAuthenticateInfoContext>>>();
        refreshDelegate.Should().NotBeNull("refresh delegate should always be registered");

        var httpContext = new DefaultHttpContext();
        IAuthenticateInfoContext result = await refreshDelegate!(sp, httpContext);
        result.IsAuthenticated.Should().BeFalse("without Auth, refresh should return not-authenticated");
    }

    [Fact]
    public void Middleware_WithoutAuth_BuildsServiceProviderWithoutException()
    {
        // Arrange — minimal DI without Auth
        var services = new ServiceCollection();
        services.AddLogging();

        var tokenConfig = new MTokenInfo
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SymmetricSecretKey = "test-key-at-least-32-characters-long!",
            UseRsa = false,
            ExpiryMinutes = 60
        };
        services.AddSingleton(tokenConfig);

        var config = BuildMinimalConfig();

        // Act — should not throw even though Auth is absent
        var act = () => services.AddValidateBearerToken(config);
        act.Should().NotThrow("AddValidateBearerToken should work without Auth package");

        // Assert — ServiceProvider builds successfully
        using var sp = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext();
        httpContext.User.Should().NotBeNull("HttpContext.User should be default ClaimsPrincipal");
        httpContext.User.Identity?.IsAuthenticated.Should().BeFalse("default user is not authenticated");
    }

#pragma warning disable CS0618 // Obsolete member usage is intentional — testing backward compatibility
    [Fact]
    public void GenericOverload_StillCompiles_MarkedObsolete()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var tokenConfig = new MTokenInfo
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SymmetricSecretKey = "test-key-at-least-32-characters-long!",
            UseRsa = false,
            ExpiryMinutes = 60
        };
        services.AddSingleton(tokenConfig);

        var config = BuildMinimalConfig();

        // Act — generic overload compiles and delegates to non-generic
        services.AddValidateBearerToken<MDbContext, TestPermission>(config);

        // Assert — ITokenSigner resolves correctly
        using var sp = services.BuildServiceProvider();
        var signer = sp.GetService<ITokenSigner>();
        signer.Should().NotBeNull("generic overload should forward to non-generic and register signer");
    }
#pragma warning restore CS0618

    private static IConfiguration BuildMinimalConfig()
    {
        var dict = new Dictionary<string, string?>
        {
            ["TokenConfigs:Issuer"] = "test-issuer",
            ["TokenConfigs:Audience"] = "test-audience",
            ["TokenConfigs:UseRsa"] = "false",
            ["TokenConfigs:SymmetricSecretKey"] = "test-key-at-least-32-characters-long!",
            ["TokenConfigs:ExpiryMinutes"] = "60"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    private enum TestPermission
    {
        Read,
        Write
    }
}
