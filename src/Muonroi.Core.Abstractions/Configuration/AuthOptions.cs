using Muonroi.Core.Abstractions.Constants;

namespace Muonroi.Core.Abstractions.Configuration;

public sealed class AuthOptions
{
    public AuthClaimMap ClaimMap { get; init; } = new();
}

public sealed class AuthClaimMap
{
    public string TokenValidityKey { get; init; } = ClaimConstants.TokenValidityKey;
    public string UserIdentifier { get; init; } = ClaimConstants.UserIdentifier;
    public string RelatedIdentifiers { get; init; } = ClaimConstants.RelatedIdentifiers;
    public string Username { get; init; } = ClaimConstants.Username;
    public string FullName { get; init; } = ClaimConstants.FullName;
    public string Language { get; init; } = ClaimConstants.Language;
    public string AccessToken { get; init; } = ClaimConstants.AccessToken;
    public string Permission { get; init; } = ClaimConstants.Permission;
    public string ApiKey { get; init; } = ClaimConstants.ApiKey;
    public string TenantId { get; init; } = ClaimConstants.TenantId;
}
