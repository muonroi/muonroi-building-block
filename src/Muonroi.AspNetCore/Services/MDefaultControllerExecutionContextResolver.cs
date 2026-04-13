using System.Security.Claims;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Tenancy.Core;

namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Resolves the controller execution context from the current HTTP context.
/// </summary>
/// <param name="configuration">The application configuration.</param>
public sealed class MDefaultControllerExecutionContextResolver(IConfiguration configuration)
    : IMControllerExecutionContextResolver
{
    /// <summary>
    /// Resolves the execution context from the specified HTTP context.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>A <see cref="MControllerExecutionContext"/> representing the current execution state.</returns>
    public MControllerExecutionContext Resolve(HttpContext httpContext)
    {
        IAuthenticateInfoContext? authContext =
            httpContext.RequestServices.GetService(typeof(IAuthenticateInfoContext)) as IAuthenticateInfoContext;

        ClaimsPrincipal user = httpContext.User;
        string? username = authContext?.CurrentUsername
                           ?? user.FindFirst(ClaimConstants.Username)?.Value
                           ?? user.Identity?.Name
                           ?? ReadHeader(httpContext, "X-Username");

        string? rawUserId = authContext?.CurrentUserGuid
                            ?? user.FindFirst(ClaimConstants.UserIdentifier)?.Value
                            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? ReadHeader(httpContext, "X-User-Id");

        string? tenantId = authContext?.TenantId
                           ?? user.FindFirst(ClaimConstants.TenantId)?.Value
                           ?? ReadHeader(httpContext, "X-Tenant-Id")
                           ?? ReadHeader(httpContext, "TenantId")
                           ?? TenantContext.CurrentTenantId
                           ?? configuration.GetValue<string>("UiEngineLab:TenantId");

        IReadOnlyList<string> permissions = ParsePermissions(
            authContext?.Permission
            ?? user.FindFirst(ClaimConstants.Permission)?.Value
            ?? ReadHeader(httpContext, "X-Permissions"),
            user);

        return new MControllerExecutionContext
        {
            UserId = ParseGuid(rawUserId),
            Username = username,
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim(),
            TenantTier = ReadHeader(httpContext, "X-Tenant-Tier")
                         ?? configuration.GetValue<string>("UiEngineLab:TenantTier")
                         ?? "Free",
            Actor = ReadHeader(httpContext, "X-Actor") ?? username ?? "anonymous",
            IsAuthenticated = authContext?.IsAuthenticated == true || user.Identity?.IsAuthenticated == true,
            Permissions = permissions
        };
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ParsePermissions(string? rawPermissions, ClaimsPrincipal user)
    {
        List<string> result = [];

        if (!string.IsNullOrWhiteSpace(rawPermissions))
        {
            result.AddRange(rawPermissions
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        result.AddRange(user.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return [.. result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ReadHeader(HttpContext httpContext, string key)
    {
        if (!httpContext.Request.Headers.TryGetValue(key, out StringValues values))
        {
            return null;
        }

        string? value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
