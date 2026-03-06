using Muonroi.Core.Abstractions.Configuration;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;

namespace Muonroi.Auth.BearerToken;

public class DefaultRefreshTokenValidator<TDbContext, TPermission>(
    TDbContext dbContext,
    IMultiLevelCacheService cacheService,
    IOptions<AuthOptions> authOptions,
    ILogger<DefaultRefreshTokenValidator<TDbContext, TPermission>> logger,
    MTokenInfo tokenInfo)
    : IRefreshTokenValidator
    where TDbContext : MDbContext
    where TPermission : Enum
{
    public async Task<MAuthenticateInfoContext?> ValidateAsync(HttpContext httpContext)
    {
        MRefreshToken? refresh = await dbContext.ResolveTokenFromHttpContext(
            httpContext,
            cacheService,
            logger,
            authOptions,
            tokenInfo);

        if (refresh == null)
        {
            return null;
        }

        return new MAuthenticateInfoContext(true)
        {
            CurrentUserGuid = refresh.CreatorUserId.ToString(),
            TokenValidityKey = refresh.TokenValidityKey ?? string.Empty
        };
    }
}
