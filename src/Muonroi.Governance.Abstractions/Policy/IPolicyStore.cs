namespace Muonroi.Governance.Policy;

/// <summary>
/// Represents the IPolicy Store.
/// </summary>
public interface IPolicyStore
{
    /// <summary>
    /// Executes the Load operation.
    /// </summary>
    LicensePolicy? Load();
    /// <summary>
    /// Executes the Save operation.
    /// </summary>
    void Save(LicensePolicy policy);
}
