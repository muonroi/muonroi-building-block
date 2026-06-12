using Muonroi.Core.Abstractions.Security;

namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents the configuration for tokens. </summary>
public class MTokenInfo
{
    /// <summary> Gets or sets the name of the configuration section. </summary>
    public string SectionName { get; set; } = "TokenConfigs";

    /// <summary> Gets or sets the symmetric secret key. </summary>
    public string SymmetricSecretKey { get; set; } = null!;

    /// <summary> Gets or sets the signing keys by tenant. </summary>
    public Dictionary<string, string> SigningKeysByTenant { get; set; } = [];

    /// <summary> Gets or sets the issuer of the token. </summary>
    public string Issuer { get; set; } = null!;

    /// <summary> Gets or sets the audience of the token. </summary>
    public string Audience { get; set; } = null!;

    /// <summary> Gets or sets the expiry time in minutes. </summary>
    public int ExpiryMinutes { get; set; }

    /// <summary> Gets or sets the time-to-live for the refresh token. </summary>
    public int RefreshTokenTtl { get; set; }

    /// <summary> Gets or sets the expiration in minutes for the refresh token. </summary>
    public int RefreshTokenEim { get; set; }

    /// <summary> Gets or sets a value indicating whether to use RSA. </summary>
    public bool UseRsa { get; set; } = true;

    /// <summary> Gets or sets the RSA public key. Can be inline PEM string or loaded from file via PublicKeyPath. </summary>
    public string PublicKey { get; set; } = null!;

    /// <summary> Gets or sets the RSA private key. Can be inline PEM string or loaded from file via PrivateKeyPath. </summary>
    public string PrivateKey { get; set; } = null!;

    /// <summary> Gets or sets the path to the RSA public key PEM file. If set, takes priority over inline PublicKey. </summary>
    public string? PublicKeyPath { get; set; }

    /// <summary> Gets or sets the path to the RSA private key PEM file. If set, takes priority over inline PrivateKey. </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary> Gets or sets a value indicating whether multi-tenancy is enabled. </summary>
    public bool MultiTenantEnabled { get; set; }

    /// <summary> Gets or sets the ID of the tenant. </summary>
    public string? TenantId { get; set; }

    /// <summary> Gets or sets a value indicating whether to enable cookie authentication. </summary>
    public bool EnableCookieAuth { get; set; }

    /// <summary> Gets or sets the name of the cookie. </summary>
    public string CookieName { get; set; } = "AuthToken";

    /// <summary> Gets or sets the SameSite attribute of the cookie. </summary>
    public string CookieSameSite { get; set; } = "Lax";

    /// <summary> Gets the effective private key from file if path specified, otherwise returns the inline key. </summary>
    /// <returns> The private key string. </returns>
    public string GetEffectivePrivateKey()
    {
        if (!string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            return MSecureFileReader.ReadKeyFile(PrivateKeyPath);
        }
        return PrivateKey;
    }

    /// <summary> Gets the effective public key from file if path specified, otherwise returns the inline key. </summary>
    /// <returns> The public key string. </returns>
    public string GetEffectivePublicKey()
    {
        if (!string.IsNullOrWhiteSpace(PublicKeyPath))
        {
            return MSecureFileReader.ReadKeyFile(PublicKeyPath);
        }
        return PublicKey;
    }
}
