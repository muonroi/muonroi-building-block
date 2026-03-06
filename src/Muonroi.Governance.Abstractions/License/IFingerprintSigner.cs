namespace Muonroi.Governance.License;

public interface IFingerprintSigner
{
    string ComputeSignature(string previousSignature, LicenseActionContext context, long sequence);
}
