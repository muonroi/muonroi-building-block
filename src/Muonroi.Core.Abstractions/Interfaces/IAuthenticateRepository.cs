


namespace Muonroi.Core.Abstractions.Interfaces;

public interface IAuthenticateRepository
{
    Task<MResponse<LoginResponseModel>> Login(LoginRequestModel model, CancellationToken cancellationToken);
    Task<MResponse<RefreshTokenResponseModel>> RefreshToken(RefreshTokenRequestModel request, CancellationToken cancellationToken);
    Task<MResponse<string>> ValidateTokenValidity(string tokenValidity, CancellationToken cancellationToken);
}
