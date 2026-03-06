namespace Muonroi.Governance.License;

/// <summary>
/// Provides runtime fingerprint data for license activation/refresh flows.
/// OSS and Enterprise tiers can provide different implementations.
/// </summary>
public interface ILicenseFingerprintProvider
{
    string GetFingerprint();
}
