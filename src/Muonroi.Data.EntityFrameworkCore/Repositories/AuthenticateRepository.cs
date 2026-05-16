namespace Muonroi.Data.EntityFrameworkCore.Repositories;

/// <summary>
/// Provides authentication-related data access for users and tokens.
/// </summary>
/// <typeparam name="TDbContext">The EF Core context type.</typeparam>
/// <typeparam name="TPermission">The permission enum type.</typeparam>
/// <param name="tokenHelper">Token helper for creating and validating tokens.</param>
/// <param name="dbContext">The database context.</param>
/// <param name="authContext">Authentication context for the current request.</param>
/// <param name="mTokenInfo">Token configuration information.</param>
/// <param name="cacheService">Cache service for token/session state.</param>
/// <param name="passwordHasher">The password hasher service.</param>
public class AuthenticateRepository<TDbContext, TPermission>(
    MAuthenticateTokenHelper<TPermission> tokenHelper,
    TDbContext dbContext,
    MAuthenticateInfoContext authContext,
    MTokenInfo mTokenInfo,
    IMultiLevelCacheService cacheService,
    IPasswordHasher passwordHasher) : IAuthenticateRepository
    where TDbContext : MDbContext
    where TPermission : Enum
{
    private readonly MAuthenticateInfoContext _authContext = authContext;

    /// <summary>
    /// Authenticates a user and issues tokens.
    /// </summary>
    /// <param name="model">The login request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The login response result.</returns>
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
                passwordHasher,
                cancellationToken);
        }

        result.AddError(nameof(SystemEnum.InvalidCredentials), _authContext.Language);
        return result;
    }


    /// <summary>
    /// Refreshes tokens using the provided refresh token request.
    /// </summary>
    /// <param name="request">The refresh token request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh token response result.</returns>
    public async Task<MResponse<RefreshTokenResponseModel>> RefreshToken(RefreshTokenRequestModel request,
        CancellationToken cancellationToken)
    {
        MResponse<RefreshTokenResponseModel> result = new();

        if (!Guid.TryParse(_authContext.CurrentUserGuid, out Guid currentUserId))
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), _authContext.Language, _authContext.CurrentUsername);
            return result;
        }

        MUser? existedUser =
            await dbContext.Users.FirstOrDefaultAsync(x => x.EntityId == currentUserId,
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

    /// <summary>
    /// Validates whether a token validity key is active.
    /// </summary>
    /// <param name="tokenValidity">The token validity key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
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
