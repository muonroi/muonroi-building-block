using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

public sealed class LicenseState
{
    public bool IsValid { get; init; }
    public bool IsExpired { get; init; }
    public string? Error { get; init; }
    public LicensePayload? Payload { get; init; }
    public ActivationProof? ActivationProof { get; init; }

    /// <summary>
    /// The license tier determines available features.
    /// Free tier is always valid but with limited features.
    /// </summary>
    public LicenseTier Tier { get; init; } = LicenseTier.Free;

    /// <summary>
    /// License ID (when using activation proof).
    /// </summary>
    public string? LicenseId { get; init; }

    /// <summary>
    /// Organization name (when using activation proof).
    /// </summary>
    public string? OrganizationName { get; init; }

    /// <summary>
    /// License expiry date (when using activation proof).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Enabled features (when using activation proof).
    /// </summary>
    public string[]? Features { get; init; }

    public LicenseTier TrustedTier => ActivationProof?.Tier ?? Tier;

    /// <summary>
    /// Checks if a specific feature is allowed under the current license.
    /// Uses capability-based resolution with backward compatibility for legacy feature keys.
    /// </summary>
    public bool HasFeature(string featureName)
    {
        return LicenseCapabilityResolver.HasAccess(this, featureName);
    }

    /// <summary>
    /// Creates a Free tier license state - always valid, limited features.
    /// </summary>
    public static LicenseState CreateFree()
    {
        LicenseState free = new()
        {
            IsValid = true,
            Tier = LicenseTier.Free,
            Payload = new LicensePayload()
        };
        free.Payload.LicenseId = "FREE";
        free.Payload.AllowedFeatures = FreeTierFeatures.All;
        return free;
    }
}

/// <summary>
/// Defines what features are available in the Free tier.
/// </summary>
public static class FreeTierFeatures
{
    /// <summary>
    /// Core database operations - always allowed.
    /// </summary>
    public const string DbQuery = "db.query";
    public const string DbSave = "db.save";
    public const string DbAdd = "db.add";
    public const string DbUpdate = "db.update";
    public const string DbDelete = "db.delete";

    /// <summary>
    /// Basic HTTP operations - always allowed.
    /// </summary>
    public const string HttpRequest = "http.request";

    /// <summary>
    /// All free tier features.
    /// </summary>
    public static readonly string[] All =
    [
        DbQuery, DbSave, DbAdd, DbUpdate, DbDelete, HttpRequest
    ];

    /// <summary>
    /// Premium features requiring a paid license.
    /// </summary>
    public static class Premium
    {
        public const string MultiTenant = "multi-tenant";
        public const string AdvancedAuth = "advanced-auth";
        public const string RuleEngine = "rule-engine";
        public const string Grpc = "grpc";
        public const string MessageBus = "message-bus";
        public const string DistributedCache = "distributed-cache";
        public const string AuditTrail = "audit-trail";
        public const string AntiTampering = "anti-tampering";
        public const string Connectors = "connectors";
        public const string JavaScriptExpressions = "js-expressions";
    }

    public static bool IsAllowed(string featureName)
    {
        return All.Contains(featureName, StringComparer.OrdinalIgnoreCase);
    }
}
