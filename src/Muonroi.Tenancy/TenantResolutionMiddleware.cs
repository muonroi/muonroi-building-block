using System.Text.RegularExpressions;

namespace Muonroi.Tenancy;

/// <summary>
/// Middleware resolves the tenant id from multiple sources and
/// propagates it through the <see cref="TenantContext"/> as well as
/// OpenTelemetry traces.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
public partial class TenantResolutionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Compiled regex for validating tenant ID format.
    /// Allows alphanumeric with dots and hyphens, max 64 chars, must start with alphanumeric.
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$")]
    private static partial Regex TenantIdFormatRegex();

    private static bool IsValidTenantId(string? tenantId)
        => !string.IsNullOrWhiteSpace(tenantId) && TenantIdFormatRegex().IsMatch(tenantId);

    /// <summary>
    /// Resolves the tenant identifier and applies it to the current request scope.
    /// Returns 400 Bad Request if a tenant ID source is present but contains invalid characters.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Invoke(HttpContext context)
    {
        (string? resolved, bool hadInvalidInput) = ResolveTenantId(context);

        if (hadInvalidInput)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string? claimTenant = context.User.FindFirst(ClaimConstants.TenantId)?.Value;

        if (string.IsNullOrWhiteSpace(claimTenant))
        {
            TenantResolutionTelemetry.RecordAuthFailure(
                "missing_claim",
                headerTenantId: resolved,
                claimTenantId: null);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (resolved != null && !string.Equals(resolved, claimTenant, StringComparison.Ordinal))
        {
            TenantResolutionTelemetry.RecordAuthFailure(
                "header_claim_mismatch",
                headerTenantId: resolved,
                claimTenantId: claimTenant);
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

    private static (string? tenantId, bool hadInvalidInput) ResolveTenantId(HttpContext context)
    {
        bool hadInvalidInput = false;

        // Header takes precedence
        if (context.Request.Headers.TryGetValue(CustomHeader.TenantId, out Microsoft.Extensions.Primitives.StringValues header))
        {
            string? value = header.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (IsValidTenantId(value)) return (value, false);
                hadInvalidInput = true;
            }
        }

        // Path: first segment
        string? path = context.Request.Path.Value;
        if (!string.IsNullOrWhiteSpace(path))
        {
            string? segment = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(segment))
            {
                if (IsValidTenantId(segment)) return (segment, false);
                hadInvalidInput = true;
            }
        }

        // Subdomain
        string host = context.Request.Host.Host;
        string[] parts = host.Split('.');
        if (parts.Length > 2)
        {
            if (IsValidTenantId(parts[0])) return (parts[0], false);
            hadInvalidInput = true;
        }

        return (null, hadInvalidInput);
    }
}
