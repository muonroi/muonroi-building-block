



namespace Muonroi.Auth.BearerToken;

/// <summary>
/// Default refresh token validator that resolves tokens from the HTTP context.
/// </summary>
public class DefaultRefreshTokenValidator<TDbContext, TPermission>(
    TDbContext dbContext,
    IMultiLevelCacheService cacheService,
    IOptions<AuthOptions> authOptions,
    IMLog<MDbContext> logger,
    MTokenInfo tokenInfo)
    : IRefreshTokenValidator
    where TDbContext : MDbContext
    where TPermission : Enum
{
    /// <inheritdoc/>
    public async Task<MAuthenticateInfoContext?> ValidateAsync(HttpContext httpContext)
    {
        MRefreshToken? refresh = await dbContext.ResolveTokenFromHttpContext(
            httpContext,
            cacheService,
            logger,
            authOptions,
            tokenInfo);

        return refresh == null
            ? null
            : new MAuthenticateInfoContext(true)
            {
                CurrentUserGuid = refresh.CreatorUserId.ToString(),
                TokenValidityKey = refresh.TokenValidityKey ?? string.Empty
            };
    }
}
