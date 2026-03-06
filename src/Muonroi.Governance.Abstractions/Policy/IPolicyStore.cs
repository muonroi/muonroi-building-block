namespace Muonroi.Governance.Policy;

public interface IPolicyStore
{
    LicensePolicy? Load();
    void Save(LicensePolicy policy);
}
