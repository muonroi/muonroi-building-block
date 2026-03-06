namespace Muonroi.Core.Abstractions.Interfaces;

public interface IAuthenticateInfoContext : ICurrentUserContext
{
    string CorrelationId { get; set; }
    string CurrentUserGuid { get; set; }
    string CurrentUsername { get; set; }
    string? TenantId { get; set; }
    string TokenValidityKey { get; set; }
    string? AccessToken { get; set; }
    string? ApiKey { get; set; }
    string? Permission { get; set; }
    string Language { get; set; }
    string Caller { get; set; }
    new MUserModel? CurrentUser { get; set; }
    bool IsAuthenticated { get; set; }
    string GetAccessToken();
}

public sealed class MAuthenticateInfoContext : IAuthenticateInfoContext
{
    public string CorrelationId { get; set; } = string.Empty;
    public string CurrentUserGuid { get; set; } = string.Empty;
    public string CurrentUsername { get; set; } = string.Empty;
    public string? TenantId { get; set; } = string.Empty;
    public string TokenValidityKey { get; set; } = string.Empty;
    public string? AccessToken { get; set; } = string.Empty;
    public string? ApiKey { get; set; } = string.Empty;
    public string? Permission { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Caller { get; set; } = string.Empty;
    public MUserModel? CurrentUser { get; set; }
    public bool IsAuthenticated { get; set; }

    public MAuthenticateInfoContext(bool isAuthenticated)
    {
        IsAuthenticated = isAuthenticated;
    }

    public string GetAccessToken()
    {
        return AccessToken ?? string.Empty;
    }

    public static T? GetClaimValue<T>(IReadOnlyList<Claim> claims, string claimType)
    {
        Claim? claim = claims.FirstOrDefault(c => c.Type == claimType);
        return claim != null && !string.IsNullOrEmpty(claim.Value)
            ? (T)Convert.ChangeType(claim.Value, typeof(T))
            : default;
    }
}
