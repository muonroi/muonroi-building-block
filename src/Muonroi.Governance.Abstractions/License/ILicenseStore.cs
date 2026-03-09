namespace Muonroi.Governance.License;

public interface ILicenseStore
{
    LicensePayload? Load();
    void Save(LicensePayload payload);
    ActivationProof? LoadActivationProof();
    void SaveActivationProof(ActivationProof proof);
}
