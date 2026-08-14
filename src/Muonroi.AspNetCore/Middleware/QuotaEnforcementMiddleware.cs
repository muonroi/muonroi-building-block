namespace Muonroi.AspNetCore.Middleware;

/// <inheritdoc />
public sealed class QuotaEnforcementMiddleware(
    RequestDelegate next,
    ITenantQuotaTracker quotaTracker,
    IMLog<QuotaEnforcementMiddleware> logger,
    ISystemExecutionContextAccessor contextAccessor)
{
/// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        string? tenantId = contextAccessor.Get().TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await next(context);
            return;
        }

        bool allowed = await quotaTracker.CheckQuotaAsync(
            tenantId,
            QuotaType.ApiRequestsPerMinute,
            1,
            context.RequestAborted);

        if (!allowed)
        {
            logger.Warn("Tenant {TenantId} exceeded API quota.", tenantId);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "rate_limit_exceeded",
                message = "API quota exceeded for current period.",
                retryAfterSeconds = 60
            }, cancellationToken: context.RequestAborted);
            return;
        }

        await quotaTracker.IncrementUsageAsync(
            tenantId,
            QuotaType.ApiRequestsPerMinute,
            1,
            context.RequestAborted);

        await next(context);
    }
}

/// <inheritdoc />
public static class QuotaEnforcementMiddlewareExtensions
{
/// <inheritdoc />
    public static IApplicationBuilder UseQuotaEnforcement(this IApplicationBuilder app)
    {
        return app.UseMiddleware<QuotaEnforcementMiddleware>();
    }
}
