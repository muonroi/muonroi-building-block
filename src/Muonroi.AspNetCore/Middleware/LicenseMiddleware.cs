using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.AspNetCore.Middleware;

/// <inheritdoc />
public sealed class LicenseMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = MGuard.NotNull(next);

/// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, ILicenseGuard guard, LicenseConfigs configs)
    {
        if (configs.EnforceOnMiddleware)
        {
            guard.EnsureValid("api.request", context.Request.Path.Value);
            LicenseActionContext actionContext = new()
            {
                ActionType = "api.request",
                ActionName = context.Request.Path.Value,
                PayloadHash = ComputeRequestHash(context),
                TenantId = ResolveTenantId(context)
            };
            guard.RecordAction(actionContext);
        }

        await _next(context);
    }

    private static string ComputeRequestHash(HttpContext context)
    {
        string payload = $"{context.Request.Method}|{context.Request.Path}|{context.Request.QueryString}";
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        string? tenantFromContext = TenantContext.CurrentTenantId;
        if (!string.IsNullOrWhiteSpace(tenantFromContext))
        {
            return tenantFromContext;
        }

        string? tenantFromHeader = context.Request.Headers[CustomHeader.TenantId].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tenantFromHeader))
        {
            return tenantFromHeader;
        }

        return context.User.FindFirst(ClaimConstants.TenantId)?.Value;
    }
}
