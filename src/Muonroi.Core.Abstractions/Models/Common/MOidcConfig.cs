namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents the configuration for OpenID Connect (OIDC). </summary>
public class MOidcConfig
{
    /// <summary> The name of the configuration section. </summary>
    public const string SectionName = "OidcConfig";

    /// <summary> Gets or sets the authority for the OIDC provider. </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary> Gets or sets the client ID for the application. </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary> Gets or sets the client secret for the application. </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary> Gets or sets the requested scopes for OIDC authentication. </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary> Gets or sets the callback path for OIDC sign-in. </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";
}
