using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Middleware that checks site enabled state per-request.
/// Returns 503 Service Unavailable if the resolved site is disabled.
///
/// State resolution order:
/// 1. ISiteProfileStateRegistry (mutable, written by hot-reload client) — if non-null, use it
/// 2. ISiteProfile.IsEnabled (default interface member, compile-time constant) — fallback
///
/// Opt-in via app.UseSiteProfileStateMiddleware().
/// </summary>
public sealed class SiteProfileStateMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var resolver = context.RequestServices.GetService<ISiteProfileResolver>();
        if (resolver is not null)
        {
            var siteProfile = resolver.Current;
            var stateRegistry = context.RequestServices.GetService<ISiteProfileStateRegistry>();

            // Check mutable state first (from hot-reload events), fall back to interface default
            var isEnabled = stateRegistry?.IsSiteEnabled(siteProfile.SiteId) ?? siteProfile.IsEnabled;

            if (!isEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    $"{{\"error\":\"Site '{siteProfile.SiteId}' is currently disabled.\"}}");
                return;
            }
        }

        await _next(context);
    }
}
