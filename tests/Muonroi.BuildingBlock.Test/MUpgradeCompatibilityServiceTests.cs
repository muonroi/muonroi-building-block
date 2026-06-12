using Muonroi.Governance.Operations;
using Muonroi.Governance.Policy;

namespace Muonroi.BuildingBlock.Test;

public class MUpgradeCompatibilityServiceTests
{
    private readonly MUpgradeCompatibilityService _service = new(NullLogger<MUpgradeCompatibilityService>.Instance);

    [Fact]
    public void Evaluate_LicenseFeatureRemoved_ReturnsBlockingIssue()
    {
        MUpgradeCompatibilityRequest request = new()
        {
            BaselineLicense = new LicensePayload { AllowedFeatures = ["audit.trail", "rules.runtime"] },
            TargetLicense = new LicensePayload { AllowedFeatures = ["rules.runtime"] }
        };
        MUpgradeCompatibilityResult result = _service.Evaluate(request);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, x => x.Code == "UPGRADE.LICENSE.FEATURE_REMOVED");
    }

    [Fact]
    public void Evaluate_PolicyFailModeRelaxed_ReturnsBlockingIssue()
    {
        MUpgradeCompatibilityRequest request = new()
        {
            BaselinePolicy = new LicensePolicy
            {
                Enforcement = new PolicyEnforcementRules { FailMode = LicenseFailMode.Hard }
            },
            TargetPolicy = new LicensePolicy
            {
                Enforcement = new PolicyEnforcementRules { FailMode = LicenseFailMode.Soft }
            }
        };
        MUpgradeCompatibilityResult result = _service.Evaluate(request);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, x => x.Code == "UPGRADE.POLICY.FAILMODE_RELAXED");
    }

    [Fact]
    public void Evaluate_ConfigSignedPolicyDisabled_ReturnsBlockingIssue()
    {
        MUpgradeCompatibilityRequest request = new()
        {
            BaselineConfig = new MUpgradeLicenseConfigSnapshot
            {
                RequireSignedPolicy = true
            },
            TargetConfig = new MUpgradeLicenseConfigSnapshot
            {
                RequireSignedPolicy = false
            }
        };
        MUpgradeCompatibilityResult result = _service.Evaluate(request);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Issues, x => x.Code == "UPGRADE.CONFIG.SIGNED_POLICY_DISABLED");
    }

    [Fact]
    public void Evaluate_MajorVersionJump_ReturnsWarningAndCompatible()
    {
        MUpgradeCompatibilityRequest request = new()
        {
            BaselinePackageVersion = "1.9.10",
            TargetPackageVersion = "2.0.0"
        };
        MUpgradeCompatibilityResult result = _service.Evaluate(request);

        Assert.True(result.IsCompatible);
        Assert.True(result.HasWarnings);
        Assert.Contains(result.Issues, x => x.Code == "UPGRADE.VERSION.MAJOR_JUMP");
    }

    [Fact]
    public void Evaluate_MajorVersionJump_WithFailOnWarning_ReturnsIncompatible()
    {
        MUpgradeCompatibilityRequest request = new()
        {
            BaselinePackageVersion = "1.9.10",
            TargetPackageVersion = "2.0.0",
            TreatWarningsAsBlocking = true
        };
        MUpgradeCompatibilityResult result = _service.Evaluate(request);

        Assert.False(result.IsCompatible);
        Assert.True(result.HasWarnings);
        Assert.DoesNotContain(result.Issues, x => x.Severity == MUpgradeCompatibilitySeverity.Blocking);
    }

    [Fact]
    public void EvaluateFromFiles_ValidInputs_ReturnsCompatible()
    {
        using CompatibilityFileHarness harness = new();
        string baselineLicensePath = harness.WriteJson("baseline-license.json", new LicensePayload
        {
            AllowedFeatures = ["audit.trail", "rules.runtime"]
        });
        string targetLicensePath = harness.WriteJson("target-license.json", new LicensePayload
        {
            AllowedFeatures = ["audit.trail", "rules.runtime", "cache.distributed"]
        });
        string baselinePolicyPath = harness.WriteJson("baseline-policy.json", new LicensePolicy
        {
            Enforcement = new PolicyEnforcementRules
            {
                FailMode = LicenseFailMode.Hard,
                EnforceOnDatabase = true
            },
            FeatureQuotas = new Dictionary<string, FeatureQuota>
            {
                ["audit.trail"] = new() { MaxUsagePerDay = 1000, MaxConcurrentUsage = 10 }
            }
        });
        string targetPolicyPath = harness.WriteJson("target-policy.json", new LicensePolicy
        {
            Enforcement = new PolicyEnforcementRules
            {
                FailMode = LicenseFailMode.Hard,
                EnforceOnDatabase = true
            },
            FeatureQuotas = new Dictionary<string, FeatureQuota>
            {
                ["audit.trail"] = new() { MaxUsagePerDay = 1500, MaxConcurrentUsage = 20 }
            }
        });
        string baselineAppsettingsPath = harness.WriteRaw("baseline-appsettings.json", """
            {
              "LicenseConfigs": {
                "Mode": "Online",
                "RequireSignedPolicy": true,
                "EnableChain": true,
                "EnableServerValidation": true,
                "Compliance": { "Enabled": true },
                "Enterprise": { "EnableSecureDefaults": true }
              }
            }
            """);
        string targetAppsettingsPath = harness.WriteRaw("target-appsettings.json", """
            {
              "LicenseConfigs": {
                "Mode": "Online",
                "RequireSignedPolicy": true,
                "EnableChain": true,
                "EnableServerValidation": true,
                "Compliance": { "Enabled": true },
                "Enterprise": { "EnableSecureDefaults": true }
              }
            }
            """);

        MUpgradeCompatibilityFileRequest request = new()
        {
            BaselinePackageVersion = "1.9.10",
            TargetPackageVersion = "1.10.0",
            BaselineLicensePath = baselineLicensePath,
            TargetLicensePath = targetLicensePath,
            BaselinePolicyPath = baselinePolicyPath,
            TargetPolicyPath = targetPolicyPath,
            BaselineAppsettingsPath = baselineAppsettingsPath,
            TargetAppsettingsPath = targetAppsettingsPath
        };
        MUpgradeCompatibilityResult result = _service.EvaluateFromFiles(request);

        Assert.True(result.IsCompatible);
        Assert.DoesNotContain(result.Issues, x => x.Severity == MUpgradeCompatibilitySeverity.Blocking);
    }

    private sealed class CompatibilityFileHarness : IDisposable
    {
        private readonly string _root;

        public CompatibilityFileHarness()
        {
            _root = Path.Combine(Path.GetTempPath(), "muonroi-upgrade-compat-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public string WriteJson<T>(string fileName, T payload)
        {
            string path = Path.Combine(_root, fileName);
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return path;
        }

        public string WriteRaw(string fileName, string content)
        {
            string path = Path.Combine(_root, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
    }
}
