namespace Muonroi.Governance.ControlPlane;

public interface IMControlPlaneStore
{
    MControlPlaneRegistry Load();
    void Save(MControlPlaneRegistry registry);
}


