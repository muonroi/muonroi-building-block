using Muonroi.Core.Abstractions.Guards;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web.Behaviors;

/// <summary>
/// Enriches the current OpenTelemetry Activity (span) with a "site.id" tag.
/// Call EnrichCurrentActivity() inside your handlers to tag the active span.
/// </summary>
public interface ISiteActivityEnricher
{
    /// <summary>
    /// Sets "site.id" tag on Activity.Current. No-op if no active Activity exists.
    /// </summary>
    void EnrichCurrentActivity();
}

/// <summary>
/// Built-in ISiteProfileBehavior that registers a per-site ISiteActivityEnricher that enriches
/// OpenTelemetry spans with a "site.id" tag matching the resolved SiteId.
///
/// Decorate your ISiteProfile with [SiteProfileBehavior(typeof(SiteObservabilityBehavior))].
///
/// Usage:
/// <code>
/// var enricher = sp.GetRequiredKeyedService&lt;ISiteActivityEnricher&gt;("TCI");
/// enricher.EnrichCurrentActivity();
/// // Activity.Current?.GetTagItem("site.id") == "TCI"
/// </code>
///
/// Verify with OTel in-memory exporter:
/// <code>
/// Assert.Equal("TCI", activity.GetTagItem("site.id"));
/// </code>
/// </summary>
public sealed class SiteObservabilityBehavior : ISiteProfileBehavior
{
    /// <inheritdoc />
    public void Apply(IServiceCollection services, IConfiguration configuration, string siteId)
    {
        var enricher = new SiteActivityEnricher(siteId);
        services.AddKeyedSingleton<ISiteActivityEnricher>(siteId, enricher);
    }
}

/// <summary>
/// Default ISiteActivityEnricher — sets "site.id" = siteId on Activity.Current.
/// </summary>
internal sealed class SiteActivityEnricher : ISiteActivityEnricher
{
    private readonly string _siteId;

    public SiteActivityEnricher(string siteId)
    {
        MGuard.NotEmpty(siteId);
        _siteId = siteId;
    }

    public void EnrichCurrentActivity()
    {
        Activity.Current?.SetTag("site.id", _siteId);
    }
}
