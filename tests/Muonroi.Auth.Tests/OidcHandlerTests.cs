using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Muonroi.Auth;

namespace Muonroi.Auth.Tests;

public sealed class OidcHandlerTests
{
    [Fact]
    public async Task AddOidcLogin_ShouldConfigureCookieDefaults_AndOidcOptions()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MOidcConfig.SectionName}:Authority"] = "https://login.example.com",
                [$"{MOidcConfig.SectionName}:ClientId"] = "client-id",
                [$"{MOidcConfig.SectionName}:ClientSecret"] = "client-secret",
                [$"{MOidcConfig.SectionName}:CallbackPath"] = "/signin-oidc",
                [$"{MOidcConfig.SectionName}:Scopes:0"] = "openid",
                [$"{MOidcConfig.SectionName}:Scopes:1"] = "profile",
                [$"{MOidcConfig.SectionName}:Scopes:2"] = "email"
            })
            .Build();

        services.AddOidcLogin(configuration, scheme: "custom-oidc");
        ServiceProvider provider = services.BuildServiceProvider();

        IOptions<AuthenticationOptions> authOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>();
        authOptions.Value.DefaultScheme.Should().Be(CookieAuthenticationDefaults.AuthenticationScheme);
        authOptions.Value.DefaultChallengeScheme.Should().Be("custom-oidc");

        IAuthenticationSchemeProvider schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemeProvider.GetSchemeAsync("custom-oidc")).Should().NotBeNull();

        IOptionsMonitor<OpenIdConnectOptions> oidcOptions = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        OpenIdConnectOptions options = oidcOptions.Get("custom-oidc");

        options.Authority.Should().Be("https://login.example.com");
        options.ClientId.Should().Be("client-id");
        options.ClientSecret.Should().Be("client-secret");
        options.CallbackPath.Value.Should().Be("/signin-oidc");
        options.ResponseType.Should().Be(OpenIdConnectResponseType.Code);
        options.SaveTokens.Should().BeTrue();
        options.Scope.Should().Contain(["openid", "profile", "email"]);
    }
}
