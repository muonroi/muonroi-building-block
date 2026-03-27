namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// RSA-based signer for ruleset audit payloads.
/// </summary>
public sealed class RsaRuleSetAuditSigner(RSA rsa, string keyId = "ruleset-control-plane", bool ownsRsa = false)
    : IRuleSetAuditSigner, IDisposable
{
    private readonly RSA _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa));

    /// <inheritdoc />
    public string KeyId { get; } = string.IsNullOrWhiteSpace(keyId) ? "ruleset-control-plane" : keyId.Trim();

    /// <inheritdoc />
    public string SignatureAlgorithm => "RSA-SHA256";

    /// <summary>Creates a signer from a PEM-encoded private key.</summary>
    /// <param name="pem">PEM contents.</param>
    /// <param name="keyId">Key identifier.</param>
    /// <returns>A configured signer.</returns>
    public static RsaRuleSetAuditSigner FromPrivateKeyPem(string pem, string keyId = "ruleset-control-plane")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(pem.ToCharArray());
        return new RsaRuleSetAuditSigner(rsa, keyId, ownsRsa: true);
    }

    /// <summary>Creates a signer from a PEM private key file.</summary>
    /// <param name="path">Path to the PEM file.</param>
    /// <param name="keyId">Key identifier.</param>
    /// <returns>A configured signer.</returns>
    public static RsaRuleSetAuditSigner FromPrivateKeyFile(string path, string keyId = "ruleset-control-plane")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Audit signer private key was not found.", path);
        }

        return FromPrivateKeyPem(File.ReadAllText(path), keyId);
    }

    /// <summary>Creates an ephemeral signer with a new RSA keypair.</summary>
    /// <param name="keyId">Key identifier.</param>
    /// <returns>A configured signer.</returns>
    public static RsaRuleSetAuditSigner CreateEphemeral(string keyId = "ruleset-control-plane-ephemeral")
    {
        return new RsaRuleSetAuditSigner(RSA.Create(2048), keyId, ownsRsa: true);
    }

    /// <inheritdoc />
    public string Sign(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] signature = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (ownsRsa)
        {
            _rsa.Dispose();
        }
    }
}

