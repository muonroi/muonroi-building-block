namespace Muonroi.Core.Abstractions.Models.Common;

public class MOidcConfig
{
    public const string SectionName = "OidcConfig";
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public string CallbackPath { get; set; } = "/signin-oidc";
}
