namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Noop Fingerprint Signer.
/// </summary>
public sealed class NoopFingerprintSigner : IFingerprintSigner
{
    /// <summary>
    /// Executes the Compute Signature operation.
    /// </summary>
    public string ComputeSignature(string previousSignature, LicenseActionContext context, long sequence)
    {
        return previousSignature;
    }
}
