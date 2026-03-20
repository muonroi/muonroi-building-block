using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents the License State.
/// </summary>
public sealed class LicenseState
{
    /// <summary>
    /// Gets or sets the Is Valid.
    /// </summary>
    public bool IsValid { get; init; }
    /// <summary>
    /// Gets or sets the Is Expired.
    /// </summary>
    public bool IsExpired { get; init; }
    /// <summary>
    /// Gets or sets the Error.
    /// </summary>
    public string? Error { get; init; }
    /// <summary>
    /// Gets or sets the Payload.
    /// </summary>
    public LicensePayload? Payload { get; init; }
    /// <summary>
    /// Gets or sets the Activation Proof.
    /// </summary>
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

    /// <summary>
    /// Gets the Trusted Tier.
    /// </summary>
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
    /// <summary>
    /// The Db Save.
    /// </summary>
    public const string DbSave = "db.save";
    /// <summary>
    /// The Db Add.
    /// </summary>
    public const string DbAdd = "db.add";
    /// <summary>
    /// The Db Update.
    /// </summary>
    public const string DbUpdate = "db.update";
    /// <summary>
    /// The Db Delete.
    /// </summary>
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
        /// <summary>
        /// The Multi Tenant.
        /// </summary>
        public const string MultiTenant = "multi-tenant";
        /// <summary>
        /// The Advanced Auth.
        /// </summary>
        public const string AdvancedAuth = "advanced-auth";
        /// <summary>
        /// The Rule Engine.
        /// </summary>
        public const string RuleEngine = "rule-engine";
        /// <summary>
        /// The Grpc.
        /// </summary>
        public const string Grpc = "grpc";
        /// <summary>
        /// The Message Bus.
        /// </summary>
        public const string MessageBus = "message-bus";
        /// <summary>
        /// The Distributed Cache.
        /// </summary>
        public const string DistributedCache = "distributed-cache";
        /// <summary>
        /// The Audit Trail.
        /// </summary>
        public const string AuditTrail = "audit-trail";
        /// <summary>
        /// The Anti Tampering.
        /// </summary>
        public const string AntiTampering = "anti-tampering";
        /// <summary>
        /// The Connectors.
        /// </summary>
        public const string Connectors = "connectors";
        /// <summary>
        /// The Java Script Expressions.
        /// </summary>
        public const string JavaScriptExpressions = "js-expressions";
    }

    /// <summary>
    /// Executes the Is Allowed operation.
    /// </summary>
    public static bool IsAllowed(string featureName)
    {
        return All.Contains(featureName, StringComparer.OrdinalIgnoreCase);
    }
}
