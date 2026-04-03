namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Defines a service for user authentication and session management.
/// </summary>
/// <typeparam name="TPermission">The type of the permission enum.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public interface IAuthService<TPermission, TDbContext>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    /// <summary>
    /// Logs out the current user by revoking their active refresh token asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result.</returns>
    Task<MResponse<object>> LogoutAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Logs out the current user from all devices by revoking all their refresh tokens asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result.</returns>
    Task<MResponse<object>> LogoutAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registers a new user asynchronously.
    /// </summary>
    /// <param name="request">The registration request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{LoginResponseModel}"/> containing the result of the registration.</returns>
    Task<MResponse<LoginResponseModel>> RegisterAsync(RegisterRequestModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates a user and generates tokens asynchronously.
    /// </summary>
    /// <param name="request">The login request model.</param>
    /// <param name="tokenInfo">The token configuration information.</param>
    /// <param name="tokenHelper">The helper for generating and validating tokens.</param>
    /// <param name="cacheService">The cache service for session management.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{LoginResponseModel}"/> containing the authenticated tokens.</returns>
    Task<MResponse<LoginResponseModel>> LoginAsync(
        LoginRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes an expired authentication token using a refresh token asynchronously.
    /// </summary>
    /// <param name="request">The refresh token request model.</param>
    /// <param name="tokenInfo">The token configuration information.</param>
    /// <param name="tokenHelper">The helper for generating and validating tokens.</param>
    /// <param name="cacheService">The cache service for session management.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{RefreshTokenResponseModel}"/> containing the new tokens.</returns>
    Task<MResponse<RefreshTokenResponseModel>> RefreshTokenAsync(
        RefreshTokenRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken);
}
