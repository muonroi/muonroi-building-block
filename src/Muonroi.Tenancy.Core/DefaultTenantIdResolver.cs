namespace Muonroi.Tenancy.Core;

public class DefaultTenantIdResolver : ITenantIdResolver
{
    private static readonly HashSet<string> ReservedPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "swagger",
        "health",
        "grpc",
        "v1",
        "v2",
        "v3"
    };

    public Task<string?> ResolveTenantIdAsync(HttpContext context)
    {
        string? tenantId = context.User.FindFirst(ClaimConstants.TenantId)?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(tenantId) &&
            context.Request.Headers.TryGetValue(CustomHeader.TenantId, out StringValues header))
        {
            tenantId = header.FirstOrDefault()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = ResolveFromRouteValues(context);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = ResolveFromPath(context.Request.Path.Value);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = ResolveFromSubdomain(context.Request.Host.Host);
        }

        return Task.FromResult(string.IsNullOrWhiteSpace(tenantId) ? null : tenantId);
    }

    private static string? ResolveFromRouteValues(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("tenantId", out object? tenantIdObj))
        {
            string? tenantId = tenantIdObj?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                return tenantId;
            }
        }

        if (context.Request.RouteValues.TryGetValue("tenant", out object? tenantObj))
        {
            string? tenant = tenantObj?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                return tenant;
            }
        }

        return null;
    }

    private static string? ResolveFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string[] segments = path.Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length >= 3 &&
                (string.Equals(segments[1], "tenant", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(segments[1], "tenants", StringComparison.OrdinalIgnoreCase)))
            {
                return string.IsNullOrWhiteSpace(segments[2]) ? null : segments[2];
            }

            return null;
        }

        if (ReservedPathPrefixes.Contains(segments[0]))
        {
            return null;
        }

        return segments[0];
    }

    private static string? ResolveFromSubdomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        host = host.Trim().TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (System.Net.IPAddress.TryParse(host, out _))
        {
            return null;
        }

        string[] parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 2)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(parts[0]) ? null : parts[0];
    }
}
