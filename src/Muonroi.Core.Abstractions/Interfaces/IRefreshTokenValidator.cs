namespace Muonroi.Core.Abstractions.Interfaces;

public interface IRefreshTokenValidator
{
    Task<MAuthenticateInfoContext?> ValidateAsync(HttpContext httpContext);
}
