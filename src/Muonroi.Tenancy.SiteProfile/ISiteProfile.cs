using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Site profile contract — each deployment variant creates one ISiteProfile.
/// RegisterServices wires the correct DbContext subclass, service overrides, and mappers.
/// Consumer creates: class Sg01Profile : ISiteProfile { ... }
/// Program.cs: services.AddSiteProfile&lt;Sg01Profile&gt;(config)
/// </summary>
public interface ISiteProfile
{
    /// <summary>
    /// Unique site identifier (e.g., "sg01", "hn01", "small-01").
    /// Used for logging, diagnostics, and tenant-site mapping.
    /// </summary>
    string SiteId { get; }

    /// <summary>
    /// Register all DI services for this site.
    /// Called once at startup. Wire DbContext, repositories, services, mappers.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
