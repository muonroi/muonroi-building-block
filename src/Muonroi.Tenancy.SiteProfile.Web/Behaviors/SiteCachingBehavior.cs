using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web.Behaviors;

/// <summary>
/// Contract for a per-site cache key prefix.
/// Inject via [FromKeyedServices(siteId)] ISiteCacheKeyPrefix.
///
/// Usage with DistributedCacheKeyBuilder:
/// <code>
/// var key = DistributedCacheKeyBuilder.Build("orders", keyNamespace: cacheKeyPrefix.Prefix);
/// // Result: "site:TCI:orders"
/// </code>
/// </summary>
public interface ISiteCacheKeyPrefix
{
    /// <summary>Cache key prefix for this site. Format: "site:{siteId}:"</summary>
    string Prefix { get; }
}

/// <summary>
/// Default implementation returning "site:{siteId}:".
/// </summary>
internal sealed class SiteCacheKeyPrefix : ISiteCacheKeyPrefix
{
    public SiteCacheKeyPrefix(string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        Prefix = $"site:{siteId}:";
    }

    public string Prefix { get; }
}

/// <summary>
/// Built-in ISiteProfileBehavior that isolates cache keys per site by registering
/// a keyed ISiteCacheKeyPrefix singleton (key = siteId) with prefix "site:{siteId}:".
///
/// Decorate your ISiteProfile with [SiteProfileBehavior(typeof(SiteCachingBehavior))].
///
/// Register: services.AddKeyedSingleton&lt;ISiteCacheKeyPrefix&gt;(siteId, instance)
/// Resolve:  [FromKeyedServices("TCI")] ISiteCacheKeyPrefix prefix
/// </summary>
public sealed class SiteCachingBehavior : ISiteProfileBehavior
{
    /// <inheritdoc />
    public void Apply(IServiceCollection services, IConfiguration configuration, string siteId)
    {
        var prefix = new SiteCacheKeyPrefix(siteId);
        services.AddKeyedSingleton<ISiteCacheKeyPrefix>(siteId, prefix);
    }
}
