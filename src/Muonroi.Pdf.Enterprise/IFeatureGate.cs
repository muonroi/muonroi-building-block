namespace Muonroi.Pdf.Enterprise;

/// <summary>
/// Runtime capability gate. The commercial assembly provides a real implementation;
/// the OSS engine (Muonroi.Pdf) has zero awareness of this interface.
/// </summary>
public interface IFeatureGate
{
    /// <summary>
    /// Returns <c>true</c> if the capability key is licensed under the current activation proof.
    /// Never throws.
    /// </summary>
    bool IsEnabled(string capabilityKey);

    /// <summary>
    /// Throws <see cref="FeatureNotLicensedException"/> if <paramref name="capabilityKey"/>
    /// is not licensed. Returns void on success.
    /// </summary>
    void EnsureFeatureOrThrow(string capabilityKey);
}
