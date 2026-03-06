namespace Muonroi.AspNetCore.Services;

public interface IAuthService<TPermission, TDbContext>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    Task<MResponse<object>> LogoutAsync(CancellationToken cancellationToken);
    Task<MResponse<object>> LogoutAllAsync(CancellationToken cancellationToken);
    Task<MResponse<LoginResponseModel>> RegisterAsync(RegisterRequestModel request, CancellationToken cancellationToken);
    Task<MResponse<LoginResponseModel>> LoginAsync(
        LoginRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken);
    Task<MResponse<RefreshTokenResponseModel>> RefreshTokenAsync(
        RefreshTokenRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken);
}
