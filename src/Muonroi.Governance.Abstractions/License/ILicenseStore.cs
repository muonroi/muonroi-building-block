namespace Muonroi.Governance.License;

public interface ILicenseStore
{
    LicensePayload? Load();
    void Save(LicensePayload payload);
    ActivationProof? LoadActivationProof();
    void SaveActivationProof(ActivationProof proof);

    /// <summary>
    /// Loads the activation JWT string for frontend license verification.
    /// Returns null if no JWT file exists.
    /// </summary>
    string? LoadActivationJwt();

    /// <summary>
    /// Saves the activation JWT string to disk for frontend consumption.
    /// </summary>
    void SaveActivationJwt(string jwt);
}
