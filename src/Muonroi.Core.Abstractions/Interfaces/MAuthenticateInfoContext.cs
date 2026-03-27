namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Defines the context for authentication information.
/// </summary>
public interface IAuthenticateInfoContext : ICurrentUserContext
{
    /// <summary>
    /// Gets or sets the correlation ID for the current request.
    /// </summary>
    string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier (GUID) of the current user.
    /// </summary>
    string CurrentUserGuid { get; set; }

    /// <summary>
    /// Gets or sets the username of the current user.
    /// </summary>
    string CurrentUsername { get; set; }

    /// <summary>
    /// Gets or sets the ID of the tenant.
    /// </summary>
    string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the key used to check token validity.
    /// </summary>
    string TokenValidityKey { get; set; }

    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the permissions.
    /// </summary>
    string? Permission { get; set; }

    /// <summary>
    /// Gets or sets the language preference.
    /// </summary>
    string Language { get; set; }

    /// <summary>
    /// Gets or sets the caller information.
    /// </summary>
    string Caller { get; set; }

    /// <summary>
    /// Gets or sets the current user model.
    /// </summary>
    new MUserModel? CurrentUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets the current access token.
    /// </summary>
    /// <returns>The access token string.</returns>
    string GetAccessToken();
}

/// <summary>
/// Implementation of the authentication information context.
/// </summary>
public sealed class MAuthenticateInfoContext : IAuthenticateInfoContext
{
    /// <inheritdoc/>
    public string CorrelationId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string CurrentUserGuid { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string CurrentUsername { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? TenantId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string TokenValidityKey { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? AccessToken { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? ApiKey { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? Permission { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Language { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Caller { get; set; } = string.Empty;

    /// <inheritdoc/>
    public MUserModel? CurrentUser { get; set; }

    /// <inheritdoc/>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MAuthenticateInfoContext"/> class.
    /// </summary>
    /// <param name="isAuthenticated">A value indicating whether the user is authenticated.</param>
    public MAuthenticateInfoContext(bool isAuthenticated)
    {
        IsAuthenticated = isAuthenticated;
    }

    /// <inheritdoc/>
    public string GetAccessToken()
    {
        return AccessToken ?? string.Empty;
    }

    /// <summary>
    /// Gets a claim value from a list of claims.
    /// </summary>
    /// <typeparam name="T">The type to convert the claim value to.</typeparam>
    /// <param name="claims">The list of claims.</param>
    /// <param name="claimType">The type of the claim to find.</param>
    /// <returns>The claim value converted to <typeparamref name="T"/>, or the default value if not found.</returns>
    public static T? GetClaimValue<T>(IReadOnlyList<Claim> claims, string claimType)
    {
        Claim? claim = claims.FirstOrDefault(c => c.Type == claimType);
        return claim != null && !string.IsNullOrEmpty(claim.Value)
            ? (T)Convert.ChangeType(claim.Value, typeof(T))
            : default;
    }
}
