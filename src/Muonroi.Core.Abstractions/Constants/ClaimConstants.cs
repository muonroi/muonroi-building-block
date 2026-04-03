namespace Muonroi.Core.Abstractions.Constants;

/// <summary>
/// Defines constants for common claim types used within the application.
/// </summary>
public static class ClaimConstants
{
    /// <summary>
    /// The claim type for token validity.
    /// </summary>
    public const string TokenValidityKey = "token_validity_key";

    /// <summary>
    /// The claim type for the user's identifier.
    /// </summary>
    public const string UserIdentifier = "user_identifier";

    /// <summary>
    /// The claim type for related identifiers.
    /// </summary>
    public const string RelatedIdentifiers = "related_identifiers";

    /// <summary>
    /// The claim type for the user's name.
    /// </summary>
    public const string Username = "user_name";

    /// <summary>
    /// The claim type for the user's full name.
    /// </summary>
    public const string FullName = "full_name";

    /// <summary>
    /// The claim type for the user's preferred language.
    /// </summary>
    public const string Language = "language";

    /// <summary>
    /// The claim type for the access token.
    /// </summary>
    public const string AccessToken = "access_token";

    /// <summary>
    /// The claim type for permissions.
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    /// The claim type for the API key.
    /// </summary>
    public const string ApiKey = "ApiKey";

    /// <summary>
    /// The claim type for the tenant identifier.
    /// </summary>
    public const string TenantId = "tenant_id";
}
