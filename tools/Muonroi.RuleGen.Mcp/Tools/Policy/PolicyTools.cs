namespace Muonroi.RuleGen.Mcp.Tools.Policy;

internal static class PolicySigningHelpers
{
    public static string BuildSigningPayload(LicensePolicy policy, IMJsonSerializeService jsonService)
    {
        var payload = new
        {
            policy.PolicyId,
            policy.Version,
            policy.LicenseId,
            policy.IssuedAt,
            policy.ExpiresAt,
            policy.Enforcement,
            policy.FeatureQuotas
        };

        return jsonService.Serialize(payload);
    }
}

[McpServerToolType]
public sealed class SignPolicyTool(
    IMJsonSerializeService jsonService,
    IMDateTimeService dateTimeService)
{
    [McpServerTool(Name = "muonroi_policy_sign")]
    public async Task<string> ExecuteAsync(
        string privateKeyPath,
        string licenseId,
        string outputPath = "policy.json",
        string version = "1.0.0",
        DateTimeOffset? expiresAtUtc = null,
        bool enforceOnDatabase = true,
        bool enableAntiTampering = true,
        string failMode = "Hard",
        int maxApiRequestsPerMinute = 10,
        int maxDbOperationsPerMinute = 5,
        long defaultMaxUsagePerDay = 1000,
        string? workingDirectory = null,
        CancellationToken ct = default)
    {
        string cwd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(workingDirectory);
        string resolvedKeyPath = Path.GetFullPath(privateKeyPath, cwd);
        string resolvedOutputPath = Path.GetFullPath(outputPath, cwd);

        string privateKeyPem = await File.ReadAllTextAsync(resolvedKeyPath, ct);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.ToCharArray());

        LicensePolicy policy = new()
        {
            PolicyId = "pol_" + Guid.NewGuid().ToString("N")[..8],
            Version = version,
            LicenseId = licenseId,
            IssuedAt = new DateTimeOffset(dateTimeService.UtcNow(), TimeSpan.Zero),
            ExpiresAt = expiresAtUtc ?? new DateTimeOffset(dateTimeService.UtcNow(), TimeSpan.Zero).AddYears(1),
            Enforcement = new PolicyEnforcementRules
            {
                EnforceOnDatabase = enforceOnDatabase,
                EnableAntiTampering = enableAntiTampering,
                FailMode = Enum.TryParse(failMode, true, out LicenseFailMode parsedFailMode) ? parsedFailMode : LicenseFailMode.Hard,
                MaxApiRequestsPerMinute = maxApiRequestsPerMinute,
                MaxDbOperationsPerMinute = maxDbOperationsPerMinute
            },
            FeatureQuotas = new Dictionary<string, FeatureQuota>(StringComparer.OrdinalIgnoreCase)
            {
                ["api.create"] = new FeatureQuota { MaxUsagePerDay = defaultMaxUsagePerDay },
                ["db.save"] = new FeatureQuota { MaxUsagePerDay = defaultMaxUsagePerDay }
            }
        };

        string payload = PolicySigningHelpers.BuildSigningPayload(policy, jsonService);
        byte[] signature = rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        policy.Signature = Convert.ToBase64String(signature);

        string finalJson = jsonService.Serialize(policy);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutputPath)!);
        await File.WriteAllTextAsync(resolvedOutputPath, finalJson, ct);

        return jsonService.Serialize(new PolicySignResult(resolvedOutputPath, policy.PolicyId, policy.LicenseId, "RSA-SHA256", policy.ExpiresAt));
    }
}

[McpServerToolType]
public sealed class VerifyPolicyTool(
    IMJsonSerializeService jsonService,
    IMDateTimeService dateTimeService)
{
    [McpServerTool(Name = "muonroi_policy_verify")]
    public async Task<string> ExecuteAsync(string policyPath, string publicKeyPath, string? workingDirectory = null, CancellationToken ct = default)
    {
        string cwd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(workingDirectory);
        string resolvedPolicyPath = Path.GetFullPath(policyPath, cwd);
        string resolvedPublicKeyPath = Path.GetFullPath(publicKeyPath, cwd);
        List<string> errors = [];

        LicensePolicy? policy = jsonService.Deserialize<LicensePolicy>(await File.ReadAllTextAsync(resolvedPolicyPath, ct));
        if (policy is null)
        {
            errors.Add("Unable to deserialize policy.json.");
            return jsonService.Serialize(new PolicyVerifyResult(false, false, false, null, null, errors));
        }

        if (string.IsNullOrWhiteSpace(policy.Signature))
        {
            errors.Add("Policy signature is missing.");
            return jsonService.Serialize(new PolicyVerifyResult(false, false, false, policy.PolicyId, policy.LicenseId, errors));
        }

        string publicKeyPem = await File.ReadAllTextAsync(resolvedPublicKeyPath, ct);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem.ToCharArray());

        string payload = PolicySigningHelpers.BuildSigningPayload(policy, jsonService);
        bool signatureValid = rsa.VerifyData(
            Encoding.UTF8.GetBytes(payload),
            Convert.FromBase64String(policy.Signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        bool expired = policy.ExpiresAt.HasValue &&
            policy.ExpiresAt.Value < new DateTimeOffset(dateTimeService.UtcNow(), TimeSpan.Zero);
        if (!signatureValid)
        {
            errors.Add("RSA signature validation failed.");
        }

        if (expired)
        {
            errors.Add("Policy has expired.");
        }

        return jsonService.Serialize(new PolicyVerifyResult(signatureValid && !expired, signatureValid, expired, policy.PolicyId, policy.LicenseId, errors));
    }
}
