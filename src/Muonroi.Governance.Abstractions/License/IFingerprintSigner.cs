namespace Muonroi.Governance.License;

/// <summary>
/// Represents the IFingerprint Signer.
/// </summary>
public interface IFingerprintSigner
{
    /// <summary>
    /// Executes the Compute Signature operation.
    /// </summary>
    string ComputeSignature(string previousSignature, LicenseActionContext context, long sequence);
}
