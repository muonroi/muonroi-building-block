namespace TestProject.Service.IntegrationTests.Helpers;

/// <summary>
/// Minimal ISiteProfile implementation for use in tests.
/// Returns the given siteId and does nothing in RegisterServices.
/// </summary>
public sealed class FakeSiteProfile(string siteId) : ISiteProfile
{
    public string SiteId => siteId;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Test stub — no services registered.
    }
}

/// <summary>
/// Minimal ISiteProfileResolver implementation for use in tests.
/// Always returns the FakeSiteProfile with the configured SiteId.
/// </summary>
public sealed class FakeSiteProfileResolver(string siteId) : ISiteProfileResolver
{
    public ISiteProfile Current { get; } = new FakeSiteProfile(siteId);
}
