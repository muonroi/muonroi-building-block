namespace Muonroi.Tenancy;

/// <summary>
/// Middleware resolves the tenant id from multiple sources and
/// propagates it through the <see cref="TenantContext"/> as well as
/// OpenTelemetry traces.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        string? resolved = ResolveTenantId(context);
        string? claimTenant = context.User.FindFirst(ClaimConstants.TenantId)?.Value;

        if (string.IsNullOrWhiteSpace(claimTenant) ||
            (resolved != null && !string.Equals(resolved, claimTenant, StringComparison.Ordinal)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        string tenantId = resolved ?? claimTenant;
        TenantContext.CurrentTenantId = tenantId;

        try
        {
            Activity? activity = Activity.Current;
            activity?.SetTag("tenant.id", tenantId);
            activity?.AddBaggage("tenant.id", tenantId);

            await next(context);
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
        }
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        // Header takes precedence
        if (context.Request.Headers.TryGetValue(CustomHeader.TenantId, out Microsoft.Extensions.Primitives.StringValues header))
        {
            string? value = header.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        // Path: first segment
        string? path = context.Request.Path.Value;
        if (!string.IsNullOrWhiteSpace(path))
        {
            string? segment = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(segment)) return segment;
        }

        // Subdomain
        string host = context.Request.Host.Host;
        string[] parts = host.Split('.');
        if (parts.Length > 2) return parts[0];

        return null;
    }
}