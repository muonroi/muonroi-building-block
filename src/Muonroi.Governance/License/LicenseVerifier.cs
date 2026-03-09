using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.License;

public sealed class LicenseVerifier(
    LicenseConfigs configs,
    IHostEnvironment? environment,
    IMJsonSerializeService jsonSerializeService,
    IMLog<LicenseVerifier>? logger = null)
{
    public async Task<LicenseState> VerifyAsync(
        LicensePayload? payload,
        ActivationProof? activationProof,
        string runtimeFingerprint)
    {
        await Task.CompletedTask;
        return Verify(payload, activationProof, runtimeFingerprint);
    }

    /// <summary>
    /// Synchronous verification method (backward compatibility).
    /// </summary>
    public LicenseState Verify(LicensePayload? payload, string runtimeFingerprint)
    {
        return Verify(payload, null, runtimeFingerprint);
    }

    public LicenseState Verify(
        LicensePayload? payload,
        ActivationProof? activationProof,
        string runtimeFingerprint)
    {
        if (activationProof?.SignedLicensePayload != null)
        {
            payload = activationProof.SignedLicensePayload;
        }

        // No license file = Free tier (always valid, limited features)
        if (payload is null)
        {
            return Finalize(LicenseState.CreateFree());
        }

        // Free license ID means explicit free tier
        if (payload.LicenseId == "FREE")
        {
            return Finalize(LicenseState.CreateFree());
        }

        if (payload.NotBefore.HasValue && payload.NotBefore.Value > DateTimeOffset.UtcNow)
        {
            return new LicenseState { IsValid = false, Error = "License not active yet.", Payload = payload };
        }

        if (payload.ExpiresAt.HasValue && payload.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            return new LicenseState
            {
                IsValid = false,
                IsExpired = true,
                Error = "License expired.",
                Payload = payload
            };
        }

        if (!string.IsNullOrWhiteSpace(payload.Fingerprint) &&
            !payload.Fingerprint.Equals(runtimeFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseState { IsValid = false, Error = "Software fingerprint mismatch.", Payload = payload };
        }

        // Deep Hardware Binding Check
        if (!string.IsNullOrWhiteSpace(payload.HardwareId) &&
            !payload.HardwareId.Equals(runtimeFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseState { IsValid = false, Error = "Hardware binding mismatch. License is registered for another machine.", Payload = payload };
        }

        // Signature verification is always required for non-free licenses.
        // This prevents fake key / fake payload privilege escalation via config bypass.
        if (!VerifySignature(payload))
        {
            if (configs.SkipSignatureVerification)
            {
                logger?.Warn(
                    "[License] SkipSignatureVerification is ignored for non-free licenses. Signature validation is mandatory.");
            }
            logger?.Warn(
                "[License] Signature verification failed for license {LicenseId}. Falling back to restricted mode.",
                payload.LicenseId ?? "UNKNOWN");
            return new LicenseState { IsValid = false, Error = "Signature invalid.", Payload = payload };
        }

        // Determine tier based on AllowedFeatures
        string[] features = payload.AllowedFeatures ?? [];
        LicenseTier tier = DetermineTier(payload);
        ActivationProof? verifiedProof = null;
        if (activationProof != null)
        {
            string? proofError = VerifyActivationProof(activationProof, payload);
            if (proofError != null)
            {
                logger?.Warn("[License] Activation proof rejected for license {LicenseId}: {Error}",
                    payload.LicenseId ?? "UNKNOWN",
                    proofError);
                return new LicenseState { IsValid = false, Error = proofError, Payload = payload };
            }

            verifiedProof = activationProof;
            tier = activationProof.Tier;
            features = activationProof.Features ?? features;
        }

        return Finalize(new LicenseState
        {
            IsValid = true,
            Payload = payload,
            Tier = tier,
            ActivationProof = verifiedProof,
            LicenseId = verifiedProof?.LicenseId ?? payload.LicenseId,
            OrganizationName = verifiedProof?.OrganizationName,
            ExpiresAt = verifiedProof?.ExpiresAt ?? payload.ExpiresAt,
            Features = features
        });
    }

    private LicenseState Finalize(LicenseState state)
    {
        LicenseEnforcementMode mode = configs.GetEffectiveEnforcementMode(state.Tier);
        logger?.Info("[License] Verified tier: {Tier}. Enforcement Mode: {Mode}", state.Tier, mode);
        return state;
    }

    private static LicenseTier DetermineTier(LicensePayload payload)
    {
        if (payload.AllowedFeatures == null || payload.AllowedFeatures.Length == 0)
        {
            return LicenseTier.Licensed;
        }

        // Wildcard = Enterprise
        if (payload.AllowedFeatures.Contains("*"))
        {
            return LicenseTier.Enterprise;
        }

        return LicenseTier.Licensed;
    }

    private bool VerifySignature(LicensePayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Signature))
        {
            return false;
        }

        string? keyPath = ResolvePath(configs.PublicKeyPath, environment);
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            return false;
        }

        string publicKey = File.ReadAllText(keyPath);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKey.ToCharArray());

        var signingData = new
        {
            payload.LicenseId,
            payload.ProjectId,
            payload.TenantId,
            payload.AllowedFeatures,
            payload.Fingerprint,
            payload.HardwareId,
            payload.ServerNonce,
            payload.NotBefore,
            payload.ExpiresAt
        };
        string json = jsonSerializeService.Serialize(signingData);
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] signature = Convert.FromBase64String(payload.Signature);
        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private string? VerifyActivationProof(ActivationProof proof, LicensePayload payload)
    {
        if (string.IsNullOrWhiteSpace(proof.Signature))
        {
            return "Activation proof signature missing.";
        }

        string? keyPath = ResolvePath(configs.PublicKeyPath, environment);
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            return "Activation proof public key not found.";
        }

        string publicKey = File.ReadAllText(keyPath);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKey.ToCharArray());

        byte[] signatureBytes = Convert.FromBase64String(proof.Signature);
        byte[] currentData = Encoding.UTF8.GetBytes(proof.GetSigningData());
        bool currentValid = rsa.VerifyData(currentData, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (!currentValid)
        {
            byte[] legacyData = Encoding.UTF8.GetBytes(proof.GetLegacySigningData());
            bool legacyValid = rsa.VerifyData(legacyData, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!legacyValid)
            {
                return "Activation proof signature invalid.";
            }
        }

        if (proof.SignedLicensePayload == null)
        {
            return "Activation proof payload missing.";
        }

        if (!string.Equals(proof.LicenseId, payload.LicenseId, StringComparison.Ordinal))
        {
            return "Activation proof license mismatch.";
        }

        if (proof.ExpiresAt != payload.ExpiresAt)
        {
            return "Activation proof expiry mismatch.";
        }

        return null;
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
