namespace Muonroi.Core.Abstractions.Configuration;

/// <summary>
/// Options for authentication configuration.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>
    /// Gets or sets the claim mapping.
    /// </summary>
    public AuthClaimMap ClaimMap { get; init; } = new();
}

/// <summary>
/// Mapping of authentication claims.
/// </summary>
public sealed class AuthClaimMap
{
    /// <summary>
    /// Gets or sets the token validity key.
    /// </summary>
    public string TokenValidityKey { get; init; } = ClaimConstants.TokenValidityKey;

    /// <summary>
    /// Gets or sets the user identifier claim key.
    /// </summary>
    public string UserIdentifier { get; init; } = ClaimConstants.UserIdentifier;

    /// <summary>
    /// Gets or sets the related identifiers claim key.
    /// </summary>
    public string RelatedIdentifiers { get; init; } = ClaimConstants.RelatedIdentifiers;

    /// <summary>
    /// Gets or sets the username claim key.
    /// </summary>
    public string Username { get; init; } = ClaimConstants.Username;

    /// <summary>
    /// Gets or sets the full name claim key.
    /// </summary>
    public string FullName { get; init; } = ClaimConstants.FullName;

    /// <summary>
    /// Gets or sets the language claim key.
    /// </summary>
    public string Language { get; init; } = ClaimConstants.Language;

    /// <summary>
    /// Gets or sets the access token claim key.
    /// </summary>
    public string AccessToken { get; init; } = ClaimConstants.AccessToken;

    /// <summary>
    /// Gets or sets the permission claim key.
    /// </summary>
    public string Permission { get; init; } = ClaimConstants.Permission;

    /// <summary>
    /// Gets or sets the API key claim key.
    /// </summary>
    public string ApiKey { get; init; } = ClaimConstants.ApiKey;

    /// <summary>
    /// Gets or sets the tenant identifier claim key.
    /// </summary>
    public string TenantId { get; init; } = ClaimConstants.TenantId;
}
