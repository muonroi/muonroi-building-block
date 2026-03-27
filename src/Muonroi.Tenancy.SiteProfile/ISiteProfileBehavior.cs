using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Cross-site behavior that contributes DI registrations for a cross-cutting concern.
/// Implementations are applied via [SiteProfileBehavior(typeof(T))] on ISiteProfile classes.
/// <code>
/// public class AuditLoggingBehavior : ISiteProfileBehavior
/// {
///     public void Apply(IServiceCollection services, IConfiguration configuration, string siteId)
///     {
///         services.AddKeyedScoped&lt;IAuditLogger, SiteAuditLogger&gt;(siteId);
///     }
/// }
/// </code>
/// </summary>
public interface ISiteProfileBehavior
{
    /// <summary>
    /// Applies this behavior's DI registrations for the given site.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="siteId">The site identifier this behavior is being applied to.</param>
    void Apply(IServiceCollection services, IConfiguration configuration, string siteId);
}
