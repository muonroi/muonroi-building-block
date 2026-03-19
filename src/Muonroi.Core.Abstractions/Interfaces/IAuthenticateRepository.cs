namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Repository for authentication operations.
/// </summary>
public interface IAuthenticateRepository
{
    /// <summary>
    /// Performs a login operation.
    /// </summary>
    /// <param name="model">The login request model containing credentials.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the login response.</returns>
    Task<MResponse<LoginResponseModel>> Login(LoginRequestModel model, CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes an authentication token.
    /// </summary>
    /// <param name="request">The refresh token request model.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the refreshed token response.</returns>
    Task<MResponse<RefreshTokenResponseModel>> RefreshToken(RefreshTokenRequestModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the validity of a token.
    /// </summary>
    /// <param name="tokenValidity">The token validity string to validate.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<MResponse<string>> ValidateTokenValidity(string tokenValidity, CancellationToken cancellationToken);
}
