namespace Muonroi.Governance.License;

public sealed class NoopFingerprintSigner : IFingerprintSigner
{
    public string ComputeSignature(string previousSignature, LicenseActionContext context, long sequence)
    {
        return previousSignature;
    }
}
