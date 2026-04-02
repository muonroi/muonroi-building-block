using System.Reflection;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Charlie;
using TestProject.Service.Sites.Default;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Demonstrates [SiteProfileAlias] — CHARLIE is aliased to DEFAULT.
/// CHARLIE resolves same service types as DEFAULT via keyed DI aliasing.
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class AliasDemoTests
{
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    private static (ServiceProvider provider, LogCapture logCapture) BuildProvider(SiteHolder holder)
    {
        var logCapture = new LogCapture();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logCapture));

        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));

        // DEFAULT keyed registration
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);

        // CHARLIE aliased to DEFAULT — register CHARLIE key pointing to DefaultOrderService
        // This mirrors what [SiteProfileAlias("DEFAULT")] source generator would emit
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.CHARLIE);

        services.AddSiteResolvedService<IOrderService>();

        return (services.BuildServiceProvider(validateScopes: true), logCapture);
    }

    [Fact]
    public void CharlieSite_ResolvesDefaultOrderService_SameAsDefault()
    {
        // [TEST:BREAKPOINT] AliasDemoTests - CHARLIE resolves DEFAULT service
        var holder = new SiteHolder { SiteId = SiteIds.CHARLIE };
        var (provider, logCapture) = BuildProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AliasDemoTests>>();
        logger.LogInformation(
            "[TEST:BREAKPOINT] AliasDemoTests - CHARLIE resolved {Type} (same as DEFAULT)",
            service.GetType().Name);

        // CHARLIE is an alias — should resolve DefaultOrderService just like DEFAULT
        Assert.IsType<DefaultOrderService>(service);
        Assert.True(logCapture.HasMessage("DefaultOrderService"),
            "CHARLIE should resolve DefaultOrderService (same as DEFAULT)");
    }

    [Fact]
    public void CharlieSiteAndDefaultSite_ResolveSameServiceType()
    {
        // [TEST:BREAKPOINT] AliasDemoTests - CHARLIE and DEFAULT return same concrete type
        foreach (var siteId in new[] { SiteIds.DEFAULT, SiteIds.CHARLIE })
        {
            var holder = new SiteHolder { SiteId = siteId };
            var (provider, logCapture) = BuildProvider(holder);

            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AliasDemoTests>>();
            logger.LogInformation(
                "[TEST:BREAKPOINT] AliasDemoTests - Site {SiteId} resolved {Type}",
                siteId, service.GetType().Name);

            Assert.IsType<DefaultOrderService>(service);
        }
    }

    [Fact]
    public void CharlieSiteProfile_HasSiteProfileAliasAttribute_TargetingDefault()
    {
        // [TEST:BREAKPOINT] AliasDemoTests - SiteProfileAlias attribute on Charlie class
        // Use reflection by attribute type name to avoid assembly version conflicts
        // between NuGet package (used by Default/Alpha/Bravo sites) and source project reference
        var attr = typeof(CharlieSiteProfile)
            .GetCustomAttributes()
            .FirstOrDefault(a => a.GetType().Name == "SiteProfileAliasAttribute");

        Assert.NotNull(attr);

        // Verify the TargetSiteId property equals "DEFAULT"
        var targetSiteId = attr.GetType().GetProperty("TargetSiteId")?.GetValue(attr) as string;
        Assert.Equal(SiteIds.DEFAULT, targetSiteId);
    }

    [Fact]
    public void CharlieSiteProfile_SiteId_IsCharlie()
    {
        // [TEST:BREAKPOINT] AliasDemoTests - Charlie SiteId property
        var profile = new CharlieSiteProfile();
        Assert.Equal(SiteIds.CHARLIE, profile.SiteId);
    }
}
