namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Configuration for ruleset storage on disk.
/// </summary>
public sealed class RuleStoreConfigs
{
    /// <summary>Configuration section name used to bind these options.</summary>
    public const string SectionName = "RuleStore";

    /// <summary>
    /// Root folder that stores ruleset files. When relative, it will be resolved
    /// against the application's content root (or AppContext.BaseDirectory if unavailable).
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// When true, relative paths are resolved against IHostEnvironment.ContentRootPath.
    /// When false or not available, AppContext.BaseDirectory is used.
    /// </summary>
    public bool UseContentRoot { get; set; } = true;

    /// <summary>
    /// Maximum allowed size in bytes for a single ruleset artifact.
    /// Prevents oversized payloads and accidental large file commits.
    /// </summary>
    public int MaxRuleSetSizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Enforces signature verification for ruleset artifacts.
    /// When enabled, loading unsigned artifacts will fail.
    /// </summary>
    public bool RequireSignature { get; set; }

    /// <summary>
    /// Regex pattern used to validate tenant/workflow path segments.
    /// Keeps file-backed store safe from path traversal payloads.
    /// </summary>
    public string AllowedPathSegmentPattern { get; set; } = "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$";

    /// <summary>
    /// Enables in-memory runtime cache for ruleset payloads.
    /// Cache is hot-invalidated through <see cref="IRuleSetChangeNotifier"/>.
    /// </summary>
    public bool EnableRuntimeCache { get; set; } = true;

    /// <summary>
    /// Absolute expiration in minutes for the runtime ruleset cache.
    /// </summary>
    public int RuntimeCacheMinutes { get; set; } = 10;

    /// <summary>
    /// Channel name used for cross-node ruleset change notifications.
    /// </summary>
    public string RuleChangeChannel { get; set; } = "muonroi:ruleset:changed";

    /// <summary>
    /// Enables maker-checker workflow for ruleset activation.
    /// </summary>
    public bool RequireApproval { get; set; }

    /// <summary>
    /// Publishes state change notifications when rulesets move between lifecycle states.
    /// </summary>
    public bool NotifyOnStateChange { get; set; } = true;
}
