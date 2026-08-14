namespace Muonroi.Bff.Tests;

public class BffAuthenticationExtensionsTests
{
    [Fact]
    public void CookieConfiguredWithSecureFlags()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddBffAuthentication();

        using ServiceProvider provider = services.BuildServiceProvider();
        CookieAuthenticationOptions options = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
    }

    [Fact]
    public void AntiforgeryConfiguredWithSecureFlags()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddBffAuthentication();

        using ServiceProvider provider = services.BuildServiceProvider();
        AntiforgeryOptions options = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
    }

    [Fact]
    public void TokenStoreIsRegistered()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddBffAuthentication();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITokenStore? store = provider.GetService<ITokenStore>();
        Assert.NotNull(store);
    }

    [Fact]
    public void TokenStoreCanBeConfiguredToUseRedisImplementation()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton(Substitute.For<IMCacheService>());
        services.AddBffAuthentication(useRedisTokenStore: true);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Assert.IsType<RedisTokenStore>(scope.ServiceProvider.GetRequiredService<ITokenStore>());
    }
}
