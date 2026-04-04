using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class OidcHandlerTests
{
    private static IConfiguration CreateConfig(IDictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AddOidcLogin_Registers_OpenIdConnect()
    {
        ServiceCollection services = [];
        Dictionary<string, string?> cfg = new()
        {
            ["OidcConfig:Authority"] = "https://auth",
            ["OidcConfig:ClientId"] = "cid",
            ["OidcConfig:ClientSecret"] = "sec",
            ["OidcConfig:Scopes:0"] = "scope1"
        };
        services.AddOidcLogin(CreateConfig(cfg));
        ServiceProvider sp = services.BuildServiceProvider();
        IOptionsMonitor<AuthenticationOptions> auth = sp.GetRequiredService<IOptionsMonitor<AuthenticationOptions>>();
        Assert.Equal("oidc", auth.CurrentValue.DefaultChallengeScheme);
        IOptionsMonitor<OpenIdConnectOptions> oidc = sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        OpenIdConnectOptions options = oidc.Get("oidc");
        Assert.Equal("https://auth", options.Authority);
        Assert.Contains("scope1", options.Scope);
    }

    [Fact]
    public void AddOidcLogin_Null_Services_Throws()
    {
        IServiceCollection? services = null;
        IConfiguration config = CreateConfig(new Dictionary<string, string?>());
        Assert.Throws<MArgumentException>(() => services!.AddOidcLogin(config));
    }

    [Fact]
    public void AddOidcLogin_Invalid_Config_Uses_Defaults()
    {
        ServiceCollection services = [];
        IConfiguration config = new ConfigurationBuilder().Build();
        services.AddOidcLogin(config);
        ServiceProvider sp = services.BuildServiceProvider();
        IOptionsMonitor<OpenIdConnectOptions> oidc = sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>();
        Assert.Throws<MArgumentException>(() => oidc.Get("oidc"));
    }
}
