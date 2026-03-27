namespace Muonroi.Governance.License;

/// <summary>
/// Provides runtime fingerprint data for license activation/refresh flows.
/// OSS and Enterprise tiers can provide different implementations.
/// </summary>
public interface ILicenseFingerprintProvider
{
    /// <summary>
    /// Executes the Get Fingerprint operation.
    /// </summary>
    string GetFingerprint();
}
