namespace Muonroi.Governance.ControlPlane;

public sealed class MRsaControlPlaneSigner(RSA rsa, string keyId = "control-plane", bool ownsRsa = false)
    : IMControlPlaneSigner, IDisposable
{
    private readonly RSA _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa));

    public string KeyId { get; } = string.IsNullOrWhiteSpace(keyId) ? "control-plane" : keyId.Trim();
    public string SignatureAlgorithm => "RSA-SHA256";

    public static MRsaControlPlaneSigner FromPrivateKeyPem(string pem, string keyId = "control-plane")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(pem.ToCharArray());
        return new MRsaControlPlaneSigner(rsa, keyId, ownsRsa: true);
    }

    public static MRsaControlPlaneSigner FromPrivateKeyFile(string path, string keyId = "control-plane")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Private key file was not found.", path);
        }

        string pem = File.ReadAllText(path);
        return FromPrivateKeyPem(pem, keyId);
    }

    public static MRsaControlPlaneSigner CreateEphemeral(string keyId = "ephemeral-control-plane")
    {
        return new MRsaControlPlaneSigner(RSA.Create(2048), keyId, ownsRsa: true);
    }

    public string Sign(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] signature = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

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

    public string ExportPublicKeyPem()
    {
        return _rsa.ExportRSAPublicKeyPem();
    }

    public void Dispose()
    {
        if (ownsRsa)
        {
            _rsa.Dispose();
        }
    }
}


