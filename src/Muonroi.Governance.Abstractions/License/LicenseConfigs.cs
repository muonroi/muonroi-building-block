using Muonroi.Governance.License;

namespace Muonroi.Governance.Abstractions.License;

/// <summary>
/// Represents the License Configs.
/// </summary>
public sealed class LicenseConfigs
{
    /// <summary>
    /// The Section Name.
    /// </summary>
    public const string SectionName = "LicenseConfigs";

    /// <summary>
    /// Gets or sets the Mode.
    /// </summary>
    public LicenseMode Mode { get; set; } = LicenseMode.Offline;
    /// <summary>
    /// Gets or sets the License File Path.
    /// </summary>
    public string? LicenseFilePath { get; set; }
    /// <summary>
    /// Gets or sets the Public Key Path.
    /// </summary>
    public string? PublicKeyPath { get; set; }

    /// <summary>
    /// Path to the activation proof file (generated during online activation).
    /// Default: "licenses/activation_proof.json"
    ///
    /// This file is automatically created when the license is activated online (dev/CI-CD).
    /// Production can use this proof to verify license offline (no internet needed).
    /// </summary>
    public string? ActivationProofPath { get; set; } = "licenses/activation_proof.json";

    /// <summary>
    /// Path to the activation JWT file for frontend license verification (MLicenseVerifier).
    /// Default: "licenses/activation_jwt.txt"
    /// This file is automatically created during online activation when the server provides a JWT.
    /// </summary>
    public string? ActivationJwtPath { get; set; } = "licenses/activation_jwt.txt";

    /// <summary>
    /// If true, attempt online activation when activation proof is not found.
    /// Default: true for better developer experience.
    /// Set to false in production if you want to require pre-activation.
    /// </summary>
    public bool FallbackToOnlineActivation { get; set; } = true;
    /// <summary>
    /// Gets or sets the Fingerprint Salt.
    /// </summary>
    public string? FingerprintSalt { get; set; }
    private string? _projectSeed;
    /// <summary>
    /// The Project Seed.
    /// </summary>
    public string? ProjectSeed
    {
        get => Obfuscate(_projectSeed);
        set => _projectSeed = Obfuscate(value);
    }

    /// <summary>
    /// Path to the signed policy file (e.g., "licenses/policy.json").
    /// </summary>
    public string? PolicyFilePath { get; set; }

    /// <summary>
    /// If true, a signed policy file must be present and valid for the application to run.
    /// Recommended for enterprise environments.
    /// </summary>
    public bool RequireSignedPolicy { get; set; } = false;

    /// <summary>
    /// Explicitly set the enforcement mode. If null, it will be automatically 
    /// determined based on the environment and license tier.
    /// </summary>
    public LicenseEnforcementMode? EnforcementMode { get; set; }

    /// <summary>
    /// List of hex-encoded public key tokens of assemblies trusted to call sensitive operations.
    /// This maintains security strength while allowing legitimate integration.
    /// </summary>
    public string[]? TrustedPublicKeyTokens { get; set; }

    private static string? Obfuscate(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        // Simple XOR with a fixed internal key to hide it from plain memory scanners
        char[] chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(chars[i] ^ 0x57 + i);
        }
        return new string(chars);
    }
    /// <summary>
    /// Enable action chain tracking. Default: false (disabled for Free mode).
    /// Only enable for Licensed/Enterprise tiers that require audit trails.
    /// </summary>
    public bool EnableChain { get; set; } = false;

    /// <summary>
    /// Gets or sets the Chain Storage.
    /// </summary>
    public LicenseChainStorage ChainStorage { get; set; } = LicenseChainStorage.None;
    /// <summary>
    /// Gets or sets the Chain File Path.
    /// </summary>
    public string? ChainFilePath { get; set; }

    /// <summary>
    /// How to handle license failures. Default: Soft (log only, don't throw).
    /// Use Hard mode only in production with a valid license.
    /// </summary>
    public LicenseFailMode FailMode { get; set; } = LicenseFailMode.Soft;

    /// <summary>
    /// Enforce license checks on database operations. Default: false.
    /// </summary>
    public bool EnforceOnDatabase { get; set; } = false;

    /// <summary>
    /// Enforce license checks on HTTP middleware. Default: false.
    /// </summary>
    public bool EnforceOnMiddleware { get; set; } = false;

    /// <summary>
    /// Enable anti-tampering protection. Default: false (disabled for developer experience).
    /// Only enable for production deployments with Licensed/Enterprise tiers.
    /// </summary>
    public bool EnableAntiTampering { get; set; } = false;

    /// <summary>
    /// Minimum interval (seconds) between runtime anti-tampering checks for each tenant partition.
    /// Lower values increase security checks but add overhead. Set 0 to check on every guarded call.
    /// </summary>
    public int AntiTamperingCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Enable hardware breakpoint detection on compatible runtimes.
    /// Default false to avoid unstable behavior on unsupported execution contexts.
    /// </summary>
    public bool EnableHardwareBreakpointDetection { get; set; } = false;

    /// <summary>
    /// Skip signature verification. Default: false.
    /// WARNING: Only set to true for development/testing. Never in production!
    /// </summary>
    public bool SkipSignatureVerification { get; set; } = false;

    /// <summary>
    /// Skip assembly whitelist verification during activation.
    /// WARNING: Only set to true for development/testing. Never in production!
    /// </summary>
    public bool SkipAssemblyWhitelist { get; set; } = false;

    /// <summary>
    /// TIER 3: Submit action chains to the license server for remote audit.
    /// </summary>
    public bool EnableServerValidation { get; set; } = false;

    /// <summary>
    /// TIER 3: How often to submit action chains to the server (in minutes).
    /// </summary>
    public int ChainSubmissionIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// TIER 3: Maximum number of chain entries to submit in a single batch.
    /// </summary>
    public int ChainSubmissionBatchSize { get; set; } = 100;

    /// <summary>
    /// TIER 3: Enable hardware-level anchoring using Windows DPAPI or TPM.
    /// Makes license files non-transferable between machines.
    /// </summary>
    public bool EnableTpmAnchoring { get; set; } = false;

    /// <summary>
    /// Executes the Online operation.
    /// </summary>
    public OnlineLicenseConfigs Online { get; set; } = new();

    /// <summary>
    /// Enterprise security profile controls.
    /// E2 secure-by-default behavior is enabled by default for Enterprise + Production.
    /// </summary>
    public MEnterpriseSecurityConfigs Enterprise { get; set; } = new();

    /// <summary>
    /// E4 compliance export + evidence-pack controls.
    /// </summary>
    public MComplianceConfigs Compliance { get; set; } = new();

    /// <summary>
    /// Automatically determines the enforcement mode based on the environment and tier.
    /// </summary>
    public LicenseEnforcementMode GetEffectiveEnforcementMode(LicenseTier tier)
    {
        if (EnforcementMode.HasValue)
        {
            return EnforcementMode.Value;
        }

        if (tier == LicenseTier.Free)
        {
            return LicenseEnforcementMode.Free;
        }

        string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        if (env.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            return LicenseEnforcementMode.Development;
        }

        return LicenseEnforcementMode.Production;
    }
}

/// <summary>
/// Represents the Online License Configs.
/// </summary>
public sealed class OnlineLicenseConfigs
{
    /// <summary>
    /// Gets or sets the Endpoint.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// TIER 3: Endpoint for submitting action chains (e.g., "/api/v1/chain/submit").
    /// </summary>
    public string? ChainSubmissionEndpoint { get; set; } = "/api/v1/chain/submit";
    /// <summary>
    /// Gets or sets the Timeout Seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
    /// <summary>
    /// Gets or sets the Refresh Minutes.
    /// </summary>
    public int RefreshMinutes { get; set; } = 1440;
    /// <summary>
    /// Gets or sets the Enable Heartbeat.
    /// </summary>
    public bool EnableHeartbeat { get; set; } = false;
    /// <summary>
    /// Gets or sets the Heartbeat Interval Minutes.
    /// </summary>
    public int HeartbeatIntervalMinutes { get; set; } = 240;
    /// <summary>
    /// Gets or sets the Revocation Grace Hours.
    /// </summary>
    public int RevocationGraceHours { get; set; } = 24;

    /// <summary>
    /// TIER 3+: Enable certificate pinning to prevent fake server and MITM attacks.
    /// When enabled, the client will only connect to servers with the expected certificate thumbprint.
    /// Default: true (recommended for production).
    /// </summary>
    public bool EnableCertificatePinning { get; set; } = true;

    /// <summary>
    /// TIER 3+: SHA256 thumbprint of the expected server certificate (e.g., "A1:B2:C3:D4:...").
    /// Get this value from your server certificate using: openssl x509 -fingerprint -sha256
    /// Required when EnableCertificatePinning is true.
    /// </summary>
    public string? ExpectedCertificateThumbprint { get; set; }

    /// <summary>
    /// TIER 3+: List of trusted certificate thumbprints for certificate rotation support.
    /// When rotating certificates, add the new certificate thumbprint here before the old one expires.
    /// If null or empty, only ExpectedCertificateThumbprint will be checked.
    /// </summary>
    public List<string>? TrustedCertificateThumbprints { get; set; }
}

/// <summary>
/// Represents the MEnterprise Security Configs.
/// </summary>
public sealed class MEnterpriseSecurityConfigs
{
    /// <summary>
    /// Enables secure defaults for Enterprise tier in Production mode.
    /// </summary>
    public bool EnableSecureDefaults { get; set; } = true;

    /// <summary>
    /// If false (default), Enterprise+Production requires a valid signed policy.
    /// </summary>
    public bool AllowPolicyBypassInProduction { get; set; } = false;

    /// <summary>
    /// If false (default), remote trust checks (trusted host, pinning, server signature)
    /// are fail-closed in Enterprise+Production.
    /// </summary>
    public bool AllowEndpointTrustBypassInProduction { get; set; } = false;

    /// <summary>
    /// Requires certificate pinning for Enterprise+Production remote operations.
    /// </summary>
    public bool RequireCertificatePinningInProduction { get; set; } = true;

    /// <summary>
    /// Requires endpoint host to match the trusted list in Enterprise+Production.
    /// </summary>
    public bool RequireTrustedEndpointInProduction { get; set; } = true;

    /// <summary>
    /// Requires license-server response signatures in Enterprise+Production.
    /// </summary>
    public bool RequireServerResponseSignatureInProduction { get; set; } = true;

    /// <summary>
    /// Trusted license server hosts for Enterprise+Production.
    /// </summary>
    public string[] TrustedLicenseServerHosts { get; set; } =
    [
        "license.muonroi.com",
        "license-backup.muonroi.com",
        "license.muonroi.net",
        "license-api.muonroi.com"
    ];
}

/// <summary>
/// Represents the MCompliance Configs.
/// </summary>
public sealed class MComplianceConfigs
{
    /// <summary>
    /// Enables compliance export pipeline and evidence pack generation.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Root folder containing export and checkpoint artifacts.
    /// </summary>
    public string ExportRootPath { get; set; } = Path.Combine("logs", "compliance");

    /// <summary>
    /// NDJSON append-only export file name.
    /// </summary>
    public string ExportFileName { get; set; } = "compliance-export.ndjson";

    /// <summary>
    /// Incremental cursor/checkpoint file.
    /// </summary>
    public string CheckpointFileName { get; set; } = "compliance-export.checkpoint.json";

    /// <summary>
    /// Folder for generated evidence packs.
    /// </summary>
    public string EvidencePackFolderName { get; set; } = "evidence-packs";

    /// <summary>
    /// Interval for periodic export hosted service.
    /// </summary>
    public int ExportIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Enables background export hosted service.
    /// </summary>
    public bool EnableBackgroundExport { get; set; } = false;

    /// <summary>
    /// Enables automatic pruning of evidence-pack files.
    /// </summary>
    public bool EnableAutoPruneEvidencePacks { get; set; } = true;

    /// <summary>
    /// Retention window for evidence-pack files.
    /// </summary>
    public int EvidencePackRetentionDays { get; set; } = 365;

    /// <summary>
    /// Max number of records loaded when generating one evidence pack.
    /// </summary>
    public int MaxRecordsPerPack { get; set; } = 100000;
}
