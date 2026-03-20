namespace Muonroi.Governance.License;

/// <summary>
/// Represents the ILicense Guard.
/// </summary>
public interface ILicenseGuard
{
    /// <summary>
    /// Gets the current license state including tier and validity.
    /// </summary>
    LicenseState Current { get; }

    /// <summary>
    /// Gets the current license tier (Free, Licensed, Enterprise).
    /// </summary>
    LicenseTier Tier { get; }

    /// <summary>
    /// Checks if the current license is in Free tier.
    /// </summary>
    bool IsFreeMode { get; }

    /// <summary>
    /// Validates the license for a given action.
    /// In Free mode, only free-tier actions are allowed.
    /// </summary>
    void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
        string? correlationId = null);

    /// <summary>
    /// Checks if a specific feature is available under the current license.
    /// </summary>
    bool HasFeature(string featureName);

    /// <summary>
    /// Ensures a feature is licensed. Throws if not available.
    /// </summary>
    void EnsureFeature(string featureName);

    /// <summary>
    /// Records an action in the license chain for audit purposes.
    /// </summary>
    void RecordAction(LicenseActionContext context);

    /// <summary>
    /// Gets the current chain token for action verification.
    /// </summary>
    string GetChainToken();

    /// <summary>
    /// Decrypts data using license-derived keys.
    /// </summary>
    string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor);
}
