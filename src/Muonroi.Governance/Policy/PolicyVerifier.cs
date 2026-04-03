using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Policy;

/// <summary>
/// Represents the Policy Verifier.
/// </summary>
public sealed class PolicyVerifier(
    LicenseConfigs configs,
    IHostEnvironment? environment,
    IMJsonSerializeService jsonSerializeService,
    IMLog<PolicyVerifier>? logger = null)
{
    /// <summary>
    /// Executes the Verify operation.
    /// </summary>
    public bool Verify(LicensePolicy policy)
    {
        if (policy == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(policy.Signature))
        {
            return false;
        }

        try
        {
            string? keyPath = ResolvePath(configs.PublicKeyPath, environment);
            if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
            {
                logger?.Warn("[Policy] Public key not found for policy verification.");
                return false;
            }

            string publicKey = File.ReadAllText(keyPath);
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(publicKey.ToCharArray());

            // Prepare data for verification (exclude the Signature field)
            var signingData = new
            {
                policy.PolicyId,
                policy.Version,
                policy.LicenseId,
                policy.IssuedAt,
                policy.ExpiresAt,
                policy.Enforcement,
                policy.FeatureQuotas
            };

            string json = jsonSerializeService.Serialize(signingData);
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] signature = Convert.FromBase64String(policy.Signature);

            bool isValid = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (isValid && policy.ExpiresAt.HasValue && policy.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                logger?.Warn("[Policy] Policy '{PolicyId}' has expired.", policy.PolicyId);
                return false;
            }

            return isValid;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[Policy] Error verifying policy signature.");
            return false;
        }
    }

    private static string? ResolvePath(string? path, IHostEnvironment? environment)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string root = !string.IsNullOrWhiteSpace(environment?.ContentRootPath)
            ? environment.ContentRootPath
            : AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, path));
    }
}
