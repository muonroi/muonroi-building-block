using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

namespace Muonroi.BuildingBlock.IntegrationTests;

/// <summary>
/// Integration test host entry point.
/// </summary>
public class Program
{
    /// <summary>
    /// Starts the test web application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        WebApplication app = BuildApp(args);
         await app.RunAsync();
    }

    /// <summary>
    /// Builds the test web application pipeline.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The configured web application.</returns>
    public static WebApplication BuildApp(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRouting();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorization();

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<IDistributedCache, SharedInMemoryDistributedCache>();
        builder.Services.AddSingleton<IMultiLevelCacheService, MultiLevelCacheService>();

        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("IntegrationTestDb"));

        WebApplication app = builder.Build();

        app.Use(async (context, next) =>
        {
            string? tenantId = await ResolveTenantIdAsync(context);
            TenantContext.CurrentTenantId = tenantId;
            try
            {
                await next();
            }
            finally
            {
                TenantContext.CurrentTenantId = null;
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/api/health", () => Results.Ok("healthy")).RequireAuthorization();
        app.MapGet("/api/protected", () => Results.Ok("protected")).RequireAuthorization();
        app.MapGet("/api/public/authenticated", () => Results.Ok("authenticated")).RequireAuthorization();
        app.MapGet("/api/user/profile", (ClaimsPrincipal user) => Results.Ok(new
        {
            user = ResolveClaim(user, ClaimConstants.Username, ClaimTypes.Name, "CurrentUsername")
        })).RequireAuthorization();

        app.MapGet("/api/claims/{claimType}", (ClaimsPrincipal user, string claimType) =>
        {
            string value = claimType.ToLowerInvariant() switch
            {
                "useridentifier" => ResolveClaim(user, ClaimConstants.UserIdentifier, ClaimTypes.NameIdentifier, "CurrentUserGuid"),
                "username" => ResolveClaim(user, ClaimConstants.Username, ClaimTypes.Name, "CurrentUsername"),
                "permission" => ResolveClaim(user, ClaimConstants.Permission, "Permission"),
                _ => string.Empty
            };
            return Results.Ok(value);
        }).RequireAuthorization();

        app.MapGet("/api/permissions/check", (ClaimsPrincipal user) =>
        {
            long permissions = ResolvePermissions(user);
            return permissions > 0 ? Results.Ok(new { allowed = true }) : Results.Forbid();
        }).RequireAuthorization();

        app.MapGet("/api/protected/read", (ClaimsPrincipal user) => CheckPermission(user, 0b0001)).RequireAuthorization();
        app.MapGet("/api/protected/write", (ClaimsPrincipal user) => CheckPermission(user, 0b0010)).RequireAuthorization();
        app.MapGet("/api/protected/delete", (ClaimsPrincipal user) => CheckPermission(user, 0b0100)).RequireAuthorization();
        app.MapGet("/api/protected/admin", (ClaimsPrincipal user) => CheckPermission(user, 0b1000)).RequireAuthorization();
        app.MapGet("/api/protected/readwrite", (ClaimsPrincipal user) => CheckPermission(user, 0b0011)).RequireAuthorization();

        app.MapGet("/api/tenant/current", () =>
        {
            return Results.Ok(new { tenantId = TenantContext.CurrentTenantId ?? "test-tenant-001" });
        }).RequireAuthorization();

        app.MapGet("/api/tenant/{tenantId}/data", (string tenantId) =>
        {
            return Results.Ok(new { routeTenant = tenantId, currentTenant = TenantContext.CurrentTenantId });
        }).RequireAuthorization();

        app.MapGet("/api/tenant/verify-immutability", () =>
        {
            return Results.Ok(new { tenantId = TenantContext.CurrentTenantId });
        }).RequireAuthorization();

        app.MapGet("/api/users", async (TestDbContext dbContext) =>
        {
            string tenantId = TenantContext.CurrentTenantId ?? "test-tenant-001";
            List<TestUser> users = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsActive)
                .ToListAsync();
            return Results.Ok(users.Select(x => new { x.Id, x.Username, x.TenantId, x.Permissions }));
        }).RequireAuthorization();

        app.MapGet("/api/user/permissions", async (TestDbContext dbContext, ClaimsPrincipal user) =>
        {
            string? userId = ResolveClaim(user, ClaimConstants.UserIdentifier, ClaimTypes.NameIdentifier, "CurrentUserGuid");
            long permissionsFromToken = ResolvePermissions(user);
            if (Guid.TryParse(userId, out Guid parsed))
            {
                TestUser? dbUser = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == parsed);
                if (dbUser is not null)
                {
                    permissionsFromToken = dbUser.Permissions;
                }
            }

            return Results.Ok(new { permissions = permissionsFromToken });
        }).RequireAuthorization();

        app.MapGet("/api/cache/{key}", async (IMultiLevelCacheService cacheService, string key) =>
        {
            string tenantId = TenantContext.CurrentTenantId ?? "test-tenant-001";
            string cacheKey = $"tenant:{tenantId}:{key}";
            string value = (await cacheService.GetOrSetAsync(
                cacheKey,
                () => Task.FromResult<string?>($"cache-{tenantId}-{key}"),
                absoluteExpirationInMinutes: 10)) ?? string.Empty;
            return Results.Ok(new { value, tenantId });
        }).RequireAuthorization();

        app.MapGet("/api/data/cached/{key}", async (IMultiLevelCacheService cacheService, string key) =>
        {
            string tenantId = TenantContext.CurrentTenantId ?? "test-tenant-001";
            string cacheKey = $"data:{tenantId}:{key}";
            string value = (await cacheService.GetOrSetAsync(
                cacheKey,
                () => Task.FromResult<string?>($"payload-{tenantId}-{key}"),
                absoluteExpirationInMinutes: 10)) ?? string.Empty;
            return Results.Ok(value);
        }).RequireAuthorization();

        app.MapPost("/api/data/create", () => Results.Ok(new { created = true, tenantId = TenantContext.CurrentTenantId }))
            .RequireAuthorization();

        return app;
    }

    private static IResult CheckPermission(ClaimsPrincipal user, long requiredMask)
    {
        long permissions = ResolvePermissions(user);
        bool allowed = (permissions & requiredMask) == requiredMask;
        return allowed ? Results.Ok(new { allowed = true }) : Results.Forbid();
    }

    private static long ResolvePermissions(ClaimsPrincipal user)
    {
        string value = ResolveClaim(user, ClaimConstants.Permission, "Permission");
        return long.TryParse(value, out long permissions) ? permissions : 0;
    }

    private static string ResolveClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static async Task<string?> ResolveTenantIdAsync(HttpContext context)
    {
        string? headerTenant = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrWhiteSpace(headerTenant))
        {
            return headerTenant;
        }

        string? claimTenant = context.User.FindFirst(ClaimConstants.TenantId)?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(claimTenant))
        {
            return claimTenant;
        }

        string? legacyTenant = context.User.FindFirst("TenantId")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(legacyTenant))
        {
            return legacyTenant;
        }

        ITenantIdResolver? resolver = context.RequestServices.GetService<ITenantIdResolver>();
        if (resolver is not null)
        {
            string? resolved = (await resolver.ResolveTenantIdAsync(context))?.Trim();
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return "test-tenant-001";
    }
}
