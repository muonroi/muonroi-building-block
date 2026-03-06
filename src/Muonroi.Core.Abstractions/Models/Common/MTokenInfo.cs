namespace Muonroi.Core.Abstractions.Models.Common;

public class MTokenInfo
{
    public string SectionName { get; set; } = "TokenConfigs";
    public string SymmetricSecretKey { get; set; } = null!;
    public Dictionary<string, string> SigningKeysByTenant { get; set; } = [];
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int ExpiryMinutes { get; set; }
    public int RefreshTokenTtl { get; set; }
    public int RefreshTokenEim { get; set; }
    public bool UseRsa { get; set; } = true;

    /// <summary>
    /// RSA Public Key - can be inline PEM string or loaded from file via PublicKeyPath
    /// </summary>
    public string PublicKey { get; set; } = null!;

    /// <summary>
    /// RSA Private Key - can be inline PEM string or loaded from file via PrivateKeyPath
    /// </summary>
    public string PrivateKey { get; set; } = null!;

    /// <summary>
    /// Path to RSA Public Key PEM file (e.g., "keys/public.pem")
    /// If set, takes priority over inline PublicKey
    /// </summary>
    public string? PublicKeyPath { get; set; }

    /// <summary>
    /// Path to RSA Private Key PEM file (e.g., "keys/private.pem")
    /// If set, takes priority over inline PrivateKey
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    public bool MultiTenantEnabled { get; set; }
    public string? TenantId { get; set; }
    public bool EnableCookieAuth { get; set; }
    public string CookieName { get; set; } = "AuthToken";
    public string CookieSameSite { get; set; } = "Lax";

    /// <summary>
    /// Gets the effective private key (from file if path specified, otherwise inline)
    /// </summary>
    public string GetEffectivePrivateKey()
    {
        if (!string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            string fullPath = Path.IsPathRooted(PrivateKeyPath)
                ? PrivateKeyPath
                : Path.Combine(AppContext.BaseDirectory, PrivateKeyPath);

            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
        }
        return PrivateKey;
    }

    /// <summary>
    /// Gets the effective public key (from file if path specified, otherwise inline)
    /// </summary>
    public string GetEffectivePublicKey()
    {
        if (!string.IsNullOrWhiteSpace(PublicKeyPath))
        {
            string fullPath = Path.IsPathRooted(PublicKeyPath)
                ? PublicKeyPath
                : Path.Combine(AppContext.BaseDirectory, PublicKeyPath);

            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }
        }
        return PublicKey;
    }
}
