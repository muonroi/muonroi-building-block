using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Data.EntityFrameworkCore.Auth;

/// <summary>
/// Default implementation for validating refresh tokens and building auth context.
/// </summary>
/// <typeparam name="TDbContext">The EF Core context type.</typeparam>
/// <typeparam name="TPermission">The permission enum type.</typeparam>
/// <param name="dbContext">The database context.</param>
/// <param name="cacheService">The cache service.</param>
/// <param name="resourceSetting">The resource setting storage.</param>
/// <param name="configuration">The application configuration.</param>
/// <param name="tokenInfo">Token configuration info.</param>
/// <param name="logger">Optional logger.</param>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "Generic TDbContext constraint and EF Core LINQ projections prevent MGuard.NotNull usage; values are null-checked by prior EF query filters.")]
public class DefaultRefreshTokenValidator<TDbContext, TPermission>(
    TDbContext dbContext,
    IMultiLevelCacheService cacheService,
    ResourceSetting resourceSetting,
    IConfiguration configuration,
    MTokenInfo tokenInfo,
    IMLog<MDbContext>? logger = null) : IRefreshTokenValidator
    where TDbContext : MDbContext
    where TPermission : Enum
{
    /// <summary>Validates the refresh token from the current HTTP context.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The authentication context or <c>null</c> if validation fails.</returns>
    public async Task<MAuthenticateInfoContext?> ValidateAsync(HttpContext httpContext)
    {
        MRefreshToken? refresh =
            await dbContext.ResolveTokenFromHttpContext(
                httpContext,
                cacheService,
                logger,
                null, // authOptions
                tokenInfo: tokenInfo);
        if (refresh is null)
        {
            return null;
        }

        string langHeader = httpContext.Request.Headers.AcceptLanguage.ToString();
        string language = string.IsNullOrWhiteSpace(langHeader)
            ? "vi-VN"
            : langHeader.Split(',').FirstOrDefault() ?? "vi-VN";
        resourceSetting[ResourceSettingKeys.Lang] = language;

        List<Claim> claims = GetClaimsFromAccessToken(httpContext.Request.Headers.Authorization.ToString());
        if (claims.Count == 0)
        {
            claims = [.. httpContext.User.Claims];
        }

        MAuthenticateInfoContext context = new(true)
        {
            CorrelationId = httpContext.Request.Headers[CustomHeader.CorrelationId].FirstOrDefault() ??
                            Guid.NewGuid().ToString(),
            AccessToken = httpContext.Request.Headers.Authorization,
            CurrentUserGuid = refresh.CreatorUserId.ToString(),
            CurrentUsername = MAuthenticateInfoContext.GetClaimValue<string>(claims, ClaimConstants.Username) ?? string.Empty,
            TenantId = MAuthenticateInfoContext.GetClaimValue<string>(claims, ClaimConstants.TenantId) ??
                       httpContext.Request.Headers[CustomHeader.TenantId].FirstOrDefault(),
            TokenValidityKey = refresh.TokenValidityKey ?? string.Empty,
            Permission = MAuthenticateInfoContext.GetClaimValue<string>(claims, ClaimConstants.Permission) ?? string.Empty,
            Language = language,
            ApiKey = configuration[ClaimConstants.ApiKey]
        };

        httpContext.Items[typeof(MAuthenticateInfoContext).FullName!] = context;
        return context;
    }

    private static List<Claim> GetClaimsFromAccessToken(string authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return [];
        }

        string token = authorizationHeader;
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token);
            return [.. jwt.Claims];
        }
        catch
        {
            return [];
        }
    }
}
