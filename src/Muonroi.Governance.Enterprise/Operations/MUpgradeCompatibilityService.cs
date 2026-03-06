using Microsoft.Extensions.Logging;
using Muonroi.Governance.License;
using Muonroi.Governance.Policy;

namespace Muonroi.Governance.Operations;

public sealed class MUpgradeCompatibilityService(
    ILogger<MUpgradeCompatibilityService>? logger = null) : IMUpgradeCompatibilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<MUpgradeCompatibilityService>? _logger = logger;

    public MUpgradeCompatibilityResult Evaluate(MUpgradeCompatibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<MUpgradeCompatibilityIssue> issues = [];

        EvaluatePackageVersion(request, issues);
        EvaluateLicense(request.BaselineLicense, request.TargetLicense, issues);
        EvaluatePolicy(request.BaselinePolicy, request.TargetPolicy, issues);
        EvaluateConfigs(request.BaselineConfig, request.TargetConfig, issues);

        bool hasBlocking = issues.Any(x => x.Severity == MUpgradeCompatibilitySeverity.Blocking);
        bool hasWarnings = issues.Any(x => x.Severity == MUpgradeCompatibilitySeverity.Warning);
        bool compatible = !hasBlocking && (!request.TreatWarningsAsBlocking || !hasWarnings);

        return new MUpgradeCompatibilityResult
        {
            IsCompatible = compatible,
            HasWarnings = hasWarnings,
            Issues = issues
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public MUpgradeCompatibilityResult EvaluateFromFiles(MUpgradeCompatibilityFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        MUpgradeCompatibilityRequest model = new()
        {
            BaselinePackageVersion = request.BaselinePackageVersion,
            TargetPackageVersion = request.TargetPackageVersion,
            BaselineLicense = LoadJson<LicensePayload>(request.BaselineLicensePath),
            TargetLicense = LoadJson<LicensePayload>(request.TargetLicensePath),
            BaselinePolicy = LoadJson<LicensePolicy>(request.BaselinePolicyPath),
            TargetPolicy = LoadJson<LicensePolicy>(request.TargetPolicyPath),
            BaselineConfig = LoadLicenseConfigSnapshot(request.BaselineAppsettingsPath),
            TargetConfig = LoadLicenseConfigSnapshot(request.TargetAppsettingsPath),
            TreatWarningsAsBlocking = request.TreatWarningsAsBlocking
        };

        return Evaluate(model);
    }

    private static void EvaluatePackageVersion(MUpgradeCompatibilityRequest request, List<MUpgradeCompatibilityIssue> issues)
    {
        string? baseline = request.BaselinePackageVersion;
        string? target = request.TargetPackageVersion;

        if (string.IsNullOrWhiteSpace(baseline) || string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        if (!TryParseSemVer(baseline, out Version? baselineVersion) || !TryParseSemVer(target, out Version? targetVersion))
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.VERSION.INVALID_FORMAT",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Package version format is invalid; semantic compatibility could not be fully evaluated.",
                Path = "packageVersion"
            });
            return;
        }

        if (targetVersion.CompareTo(baselineVersion) < 0)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.VERSION.DOWNGRADE",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = $"Target package version '{target}' is lower than baseline '{baseline}'.",
                Path = "packageVersion"
            });
            return;
        }

        if (targetVersion.Major > baselineVersion.Major)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.VERSION.MAJOR_JUMP",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = $"Major version jump from '{baseline}' to '{target}' requires migration validation.",
                Path = "packageVersion"
            });
        }
    }

    private static void EvaluateLicense(
        LicensePayload? baseline,
        LicensePayload? target,
        List<MUpgradeCompatibilityIssue> issues)
    {
        if (baseline == null && target == null)
        {
            return;
        }

        if (baseline != null && target == null)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.LICENSE.MISSING_TARGET",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target license payload is missing while baseline license exists.",
                Path = "license"
            });
            return;
        }

        if (baseline == null || target == null)
        {
            return;
        }

        HashSet<string> baselineFeatures = NormalizeDistinct(baseline.AllowedFeatures);
        HashSet<string> targetFeatures = NormalizeDistinct(target.AllowedFeatures);
        List<string> missingFeatures = [.. baselineFeatures.Where(feature => !targetFeatures.Contains(feature) && !targetFeatures.Contains("*"))];
        foreach (string? feature in missingFeatures)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.LICENSE.FEATURE_REMOVED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = $"Target license no longer grants feature '{feature}' from baseline.",
                Path = "license.allowedFeatures"
            });
        }

        if (!string.IsNullOrWhiteSpace(baseline.TenantId) &&
            !string.Equals(baseline.TenantId, target.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.LICENSE.TENANT_CHANGED",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = $"Tenant binding changed from '{baseline.TenantId}' to '{target.TenantId ?? "<null>"}'.",
                Path = "license.tenantId"
            });
        }

        if (baseline.ExpiresAt.HasValue && target.ExpiresAt.HasValue && target.ExpiresAt.Value < baseline.ExpiresAt.Value)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.LICENSE.EXPIRY_REDUCED",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Target license expires earlier than baseline.",
                Path = "license.expiresAt"
            });
        }
    }

    private static void EvaluatePolicy(
        LicensePolicy? baseline,
        LicensePolicy? target,
        List<MUpgradeCompatibilityIssue> issues)
    {
        if (baseline == null && target == null)
        {
            return;
        }

        if (baseline != null && target == null)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.POLICY.MISSING_TARGET",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target policy is missing while baseline policy exists.",
                Path = "policy"
            });
            return;
        }

        if (baseline == null || target == null)
        {
            return;
        }

        if (baseline.Enforcement.FailMode == LicenseFailMode.Hard &&
            target.Enforcement.FailMode != LicenseFailMode.Hard)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.POLICY.FAILMODE_RELAXED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target policy relaxes fail mode from Hard to Soft.",
                Path = "policy.enforcement.failMode"
            });
        }

        if (baseline.Enforcement.EnforceOnDatabase && !target.Enforcement.EnforceOnDatabase)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.POLICY.DB_ENFORCEMENT_RELAXED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target policy disables DB enforcement previously enabled in baseline.",
                Path = "policy.enforcement.enforceOnDatabase"
            });
        }

        if (baseline.Enforcement.EnableAntiTampering && !target.Enforcement.EnableAntiTampering)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.POLICY.ANTITAMPER_RELAXED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target policy disables anti-tampering previously enabled in baseline.",
                Path = "policy.enforcement.enableAntiTampering"
            });
        }

        if (baseline.Enforcement.MaxApiRequestsPerMinute > 0 &&
            target.Enforcement.MaxApiRequestsPerMinute > 0 &&
            target.Enforcement.MaxApiRequestsPerMinute < baseline.Enforcement.MaxApiRequestsPerMinute)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.POLICY.API_RATE_REDUCED",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Target policy reduces API rate limit compared to baseline.",
                Path = "policy.enforcement.maxApiRequestsPerMinute"
            });
        }

        if (baseline.Enforcement.MaxDbOperationsPerMinute > 0 &&
            target.Enforcement.MaxDbOperationsPerMinute > 0 &&
            target.Enforcement.MaxDbOperationsPerMinute < baseline.Enforcement.MaxDbOperationsPerMinute)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.POLICY.DB_RATE_REDUCED",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Target policy reduces DB rate limit compared to baseline.",
                Path = "policy.enforcement.maxDbOperationsPerMinute"
            });
        }

        List<string> baselineQuotaKeys = [.. baseline.FeatureQuotas.Keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        foreach (string? quotaKey in baselineQuotaKeys)
        {
            if (!target.FeatureQuotas.TryGetValue(quotaKey, out FeatureQuota? targetQuota))
            {
                issues.Add(new MUpgradeCompatibilityIssue
                {
                    Code = "UPGRADE.POLICY.QUOTA_REMOVED",
                    Severity = MUpgradeCompatibilitySeverity.Blocking,
                    Message = $"Target policy removes feature quota '{quotaKey}'.",
                    Path = $"policy.featureQuotas.{quotaKey}"
                });
                continue;
            }

            FeatureQuota baselineQuota = baseline.FeatureQuotas[quotaKey];
            if (baselineQuota.MaxUsagePerDay > 0 &&
                targetQuota.MaxUsagePerDay > 0 &&
                targetQuota.MaxUsagePerDay < baselineQuota.MaxUsagePerDay)
            {
                issues.Add(new MUpgradeCompatibilityIssue
                {
                    Code = "UPGRADE.POLICY.QUOTA_USAGE_REDUCED",
                    Severity = MUpgradeCompatibilitySeverity.Warning,
                    Message = $"Target policy reduces daily quota for '{quotaKey}'.",
                    Path = $"policy.featureQuotas.{quotaKey}.maxUsagePerDay"
                });
            }

            if (baselineQuota.MaxConcurrentUsage > 0 &&
                targetQuota.MaxConcurrentUsage > 0 &&
                targetQuota.MaxConcurrentUsage < baselineQuota.MaxConcurrentUsage)
            {
                issues.Add(new MUpgradeCompatibilityIssue
                {
                    Code = "UPGRADE.POLICY.QUOTA_CONCURRENCY_REDUCED",
                    Severity = MUpgradeCompatibilitySeverity.Warning,
                    Message = $"Target policy reduces concurrent quota for '{quotaKey}'.",
                    Path = $"policy.featureQuotas.{quotaKey}.maxConcurrentUsage"
                });
            }
        }
    }

    private static void EvaluateConfigs(
        MUpgradeLicenseConfigSnapshot? baseline,
        MUpgradeLicenseConfigSnapshot? target,
        List<MUpgradeCompatibilityIssue> issues)
    {
        if (baseline == null && target == null)
        {
            return;
        }

        if (baseline != null && target == null)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.MISSING_TARGET",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target license configuration snapshot is missing while baseline exists.",
                Path = "licenseConfigs"
            });
            return;
        }

        if (baseline == null || target == null)
        {
            return;
        }

        if (baseline.RequireSignedPolicy && !target.RequireSignedPolicy)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.SIGNED_POLICY_DISABLED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target configuration disables signed policy requirement.",
                Path = "licenseConfigs.requireSignedPolicy"
            });
        }

        if (baseline.EnableChain && !target.EnableChain)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.CHAIN_DISABLED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target configuration disables audit chain while baseline enabled it.",
                Path = "licenseConfigs.enableChain"
            });
        }

        if (baseline.ComplianceEnabled && !target.ComplianceEnabled)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.COMPLIANCE_DISABLED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target configuration disables compliance export/evidence tooling.",
                Path = "licenseConfigs.compliance.enabled"
            });
        }

        if (baseline.EnterpriseSecureDefaultsEnabled && !target.EnterpriseSecureDefaultsEnabled)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.ENTERPRISE_DEFAULTS_DISABLED",
                Severity = MUpgradeCompatibilitySeverity.Blocking,
                Message = "Target configuration disables enterprise secure defaults.",
                Path = "licenseConfigs.enterprise.enableSecureDefaults"
            });
        }

        if (baseline.EnableServerValidation && !target.EnableServerValidation)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.SERVER_VALIDATION_DISABLED",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Target configuration disables server validation previously enabled in baseline.",
                Path = "licenseConfigs.enableServerValidation"
            });
        }

        if (baseline.EnforceOnDatabase && !target.EnforceOnDatabase)
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.DB_ENFORCEMENT_DISABLED",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Target configuration disables DB enforcement previously enabled in baseline.",
                Path = "licenseConfigs.enforceOnDatabase"
            });
        }

        if (!string.IsNullOrWhiteSpace(baseline.Mode) &&
            !string.IsNullOrWhiteSpace(target.Mode) &&
            baseline.Mode.Equals("Online", StringComparison.OrdinalIgnoreCase) &&
            target.Mode.Equals("Offline", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new MUpgradeCompatibilityIssue
            {
                Code = "UPGRADE.CONFIG.MODE_ONLINE_TO_OFFLINE",
                Severity = MUpgradeCompatibilitySeverity.Warning,
                Message = "Target configuration downgrades license mode from Online to Offline.",
                Path = "licenseConfigs.mode"
            });
        }
    }

    private T? LoadJson<T>(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return default;
        }

        string resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
        {
            _logger?.LogWarning("Compatibility checker: file '{Path}' not found.", resolved);
            return default;
        }

        try
        {
            string json = File.ReadAllText(resolved);
            return JsonSerializer.Deserialize<T>(json, JsonOptions); // MBB002-exempt: requires custom JsonOptions (PropertyNameCaseInsensitive) not available in wrapper
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Compatibility checker failed to parse '{Path}'.", resolved);
            return default;
        }
    }

    private static MUpgradeLicenseConfigSnapshot? LoadLicenseConfigSnapshot(string? appsettingsPath)
    {
        if (string.IsNullOrWhiteSpace(appsettingsPath))
        {
            return null;
        }

        string resolved = Path.GetFullPath(appsettingsPath);
        if (!File.Exists(resolved))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(resolved);
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("LicenseConfigs", out JsonElement section))
            {
                return null;
            }

            LicenseConfigs? config = section.Deserialize<LicenseConfigs>(JsonOptions);
            if (config == null)
            {
                return null;
            }

            MUpgradeLicenseConfigSnapshot snapshot = new()
            {
                Mode = config.Mode.ToString(),
                RequireSignedPolicy = config.RequireSignedPolicy,
                EnforceOnDatabase = config.EnforceOnDatabase,
                EnforceOnMiddleware = config.EnforceOnMiddleware,
                EnableChain = config.EnableChain,
                EnableServerValidation = config.EnableServerValidation,
                EnableAntiTampering = config.EnableAntiTampering,
                ComplianceEnabled = config.Compliance.Enabled,
                EnterpriseSecureDefaultsEnabled = config.Enterprise.EnableSecureDefaults
            };
            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> NormalizeDistinct(string[]? features)
    {
        if (features == null || features.Length == 0)
        {
            return [];
        }

        return features
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryParseSemVer(string value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        int dash = normalized.IndexOf('-');
        if (dash > 0)
        {
            normalized = normalized[..dash];
        }

        int plus = normalized.IndexOf('+');
        if (plus > 0)
        {
            normalized = normalized[..plus];
        }

        string[] parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int major) ||
            !int.TryParse(parts[1], out int minor))
        {
            return false;
        }

        int patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
        {
            return false;
        }

        version = new Version(major, minor, patch);
        return true;
    }
}
