namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the IMControl Plane Signer.
/// </summary>
public interface IMControlPlaneSigner
{
    /// <summary>
    /// Gets the Key Id.
    /// </summary>
    string KeyId { get; }
    /// <summary>
    /// Gets the Signature Algorithm.
    /// </summary>
    string SignatureAlgorithm { get; }
    /// <summary>
    /// Executes the Sign operation.
    /// </summary>
    string Sign(string payload);
    /// <summary>
    /// Executes the Verify operation.
    /// </summary>
    bool Verify(string payload, string signature);
}


