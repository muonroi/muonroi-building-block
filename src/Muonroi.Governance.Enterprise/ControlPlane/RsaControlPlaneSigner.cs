using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the MRsa Control Plane Signer.
/// </summary>
public sealed class MRsaControlPlaneSigner(RSA rsa, string keyId = "control-plane", bool ownsRsa = false)
    : IMControlPlaneSigner, IDisposable
{
    private readonly RSA _rsa = MGuard.NotNull(rsa);

    /// <summary>
    /// Executes the Key Id operation.
    /// </summary>
    public string KeyId { get; } = string.IsNullOrWhiteSpace(keyId) ? "control-plane" : keyId.Trim();
    /// <summary>
    /// Gets the Signature Algorithm.
    /// </summary>
    public string SignatureAlgorithm => "RSA-SHA256";

    /// <summary>
    /// Executes the From Private Key Pem operation.
    /// </summary>
    public static MRsaControlPlaneSigner FromPrivateKeyPem(string pem, string keyId = "control-plane")
    {
        MGuard.NotEmpty(pem);
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(pem.ToCharArray());
        return new MRsaControlPlaneSigner(rsa, keyId, ownsRsa: true);
    }

    /// <summary>
    /// Executes the From Private Key File operation.
    /// </summary>
    public static MRsaControlPlaneSigner FromPrivateKeyFile(string path, string keyId = "control-plane")
    {
        MGuard.NotEmpty(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Private key file was not found.", path);
        }

        string pem = File.ReadAllText(path);
        return FromPrivateKeyPem(pem, keyId);
    }

    /// <summary>
    /// Executes the Create Ephemeral operation.
    /// </summary>
    public static MRsaControlPlaneSigner CreateEphemeral(string keyId = "ephemeral-control-plane")
    {
        return new MRsaControlPlaneSigner(RSA.Create(2048), keyId, ownsRsa: true);
    }

    /// <summary>
    /// Executes the Sign operation.
    /// </summary>
    public string Sign(string payload)
    {
        MGuard.NotNull(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] signature = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Executes the Verify operation.
    /// </summary>
    public bool Verify(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            byte[] signatureBytes = Convert.FromBase64String(signature);
            return _rsa.VerifyData(bytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes the Export Public Key Pem operation.
    /// </summary>
    public string ExportPublicKeyPem()
    {
        return _rsa.ExportRSAPublicKeyPem();
    }

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose()
    {
        if (ownsRsa)
        {
            _rsa.Dispose();
        }
    }
}


