namespace Muonroi.Data.EntityFrameworkCore.Repositories;

public class AuthenticateRepository<TDbContext, TPermission>(
    MAuthenticateTokenHelper<TPermission> tokenHelper,
    TDbContext dbContext,
    MAuthenticateInfoContext authContext,
    MTokenInfo mTokenInfo,
    IMultiLevelCacheService cacheService) : IAuthenticateRepository
    where TDbContext : MDbContext
    where TPermission : Enum
{
    private readonly MAuthenticateInfoContext _authContext = authContext;

    public async Task<MResponse<LoginResponseModel>> Login(LoginRequestModel model, CancellationToken cancellationToken)
    {
        MResponse<LoginResponseModel> result = new();
        MUser? existedUser = await dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == model.Username, cancellationToken);
        if (existedUser is not null)
        {
            return await dbContext.ResolveLoginAsync(
                model,
                result,
                existedUser,
                mTokenInfo,
                tokenHelper,
                cacheService,
                _authContext.Language,
                claims: null,
                cancellationToken);
        }

        result.AddError(nameof(SystemEnum.InvalidCredentials), _authContext.Language);
        return result;
    }


    public async Task<MResponse<RefreshTokenResponseModel>> RefreshToken(RefreshTokenRequestModel request,
        CancellationToken cancellationToken)
    {
        MResponse<RefreshTokenResponseModel> result = new();

        MUser? existedUser =
            await dbContext.Users.FirstOrDefaultAsync(x => x.EntityId == Guid.Parse(_authContext.CurrentUserGuid),
                cancellationToken);

        if (existedUser is null)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), _authContext.Language, _authContext.CurrentUsername);
            return result;
        }

        result = await dbContext.ResolveRefreshToken(request, result, mTokenInfo, tokenHelper, existedUser,
            cacheService, _authContext.Language, claims: null, cancellationToken);

        return result;
    }

    public async Task<MResponse<string>> ValidateTokenValidity(string tokenValidity,
        CancellationToken cancellationToken)
    {
        MResponse<string> result = new();
        if (string.IsNullOrEmpty(tokenValidity))
        {
            result.AddError(nameof(SystemEnum.InvalidTokenValidity), _authContext.Language, _authContext.CurrentUsername);
            return result;
        }

        result = await dbContext.ResolveTokenValidity(tokenValidity, _authContext.Language, cancellationToken);
        return result;
    }
}
