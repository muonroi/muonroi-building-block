namespace Muonroi.Governance.ControlPlane;

public interface IMControlPlaneSigner
{
    string KeyId { get; }
    string SignatureAlgorithm { get; }
    string Sign(string payload);
    bool Verify(string payload, string signature);
}


