namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the IMControl Plane Store.
/// </summary>
public interface IMControlPlaneStore
{
    /// <summary>
    /// Executes the Load operation.
    /// </summary>
    MControlPlaneRegistry Load();
    /// <summary>
    /// Executes the Save operation.
    /// </summary>
    void Save(MControlPlaneRegistry registry);
}


