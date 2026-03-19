namespace Muonroi.Data.EntityFrameworkCore.Auth;

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
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new();
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt = handler.ReadJwtToken(token);
            return [.. jwt.Claims];
        }
        catch
        {
            return [];
        }
    }
}
