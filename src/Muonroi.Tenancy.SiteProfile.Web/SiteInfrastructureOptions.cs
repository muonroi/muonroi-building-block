using System.Reflection;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Options for <see cref="SiteProfileWebExtensions.AddSiteInfrastructure"/>.
/// Consumer sets only what's project-specific; everything else is handled by the ecosystem.
/// </summary>
public sealed class SiteInfrastructureOptions
{
    /// <summary>
    /// Resolves the current site code per request.
    /// Required — typically: <c>sp => sp.GetRequiredService&lt;IWorkContextAccessor&gt;().WorkContext?.SiteCode</c>
    /// </summary>
    public Func<IServiceProvider, string?>? SiteCodeAccessor { get; set; }

    /// <summary>
    /// Assemblies containing ISiteProfile implementations.
    /// Required — typically the assembly of each site project.
    /// </summary>
    public Assembly[] SiteAssemblies { get; set; } = [];

    /// <summary>
    /// Enable per-site REST controller discovery via ApplicationParts.
    /// When true, <see cref="SiteAssemblies"/> are also scanned for controllers.
    /// Default: false (gRPC services don't need this).
    /// </summary>
    public bool EnableControllerDiscovery { get; set; }

    /// <summary>
    /// Skip startup validation (useful for aggregate services that have no DbContext).
    /// Default: false.
    /// </summary>
    public bool SkipStartupValidation { get; set; }
}
