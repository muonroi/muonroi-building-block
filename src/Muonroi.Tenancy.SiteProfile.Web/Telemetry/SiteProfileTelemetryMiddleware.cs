using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web.Telemetry;

/// <summary>
/// Middleware that enriches each HTTP request with site-scoped observability:
/// <list type="bullet">
///   <item>Tags <see cref="Activity.Current"/> with <c>site.id</c> (TELE-01)</item>
///   <item>Adds <c>SiteId</c> to the IMLog structured scope for downstream log entries (TELE-02)</item>
///   <item>Increments <c>site_profile_requests_total</c> OTel counter with <c>site_id</c> dimension (TELE-03)</item>
/// </list>
/// Opt-in via <c>app.UseSiteProfileTelemetry()</c>. Place after SiteProfileStateMiddleware.
/// When <see cref="ISiteProfileResolver"/> is not registered, the middleware is a no-op passthrough.
/// </summary>
public sealed class SiteProfileTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMLog<SiteProfileTelemetryMiddleware> _log;

    public SiteProfileTelemetryMiddleware(
        RequestDelegate next,
        IMLog<SiteProfileTelemetryMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var resolver = context.RequestServices.GetService<ISiteProfileResolver>();

        if (resolver is not null)
        {
            var siteId = resolver.Current.SiteId;

            // TELE-01: tag the current Activity span with site.id
            Activity.Current?.SetTag("site.id", siteId);

            // TELE-02: enrich IMLog scope — all downstream log entries include SiteId
            using var scope = _log.BeginScope(new Dictionary<string, object?> { ["SiteId"] = siteId });

            // TELE-03: increment per-site request counter
            SiteProfileTelemetryMetrics.RequestCounter.Add(
                1,
                new TagList { { "site_id", siteId } });

            await _next(context);
        }
        else
        {
            await _next(context);
        }
    }
}
