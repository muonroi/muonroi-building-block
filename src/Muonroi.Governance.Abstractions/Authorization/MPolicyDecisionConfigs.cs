namespace Muonroi.Governance.Authorization;

/// <summary>
/// Represents the MPolicy Decision Provider.
/// </summary>
public enum MPolicyDecisionProvider
{
    /// <summary>
    /// Represents the Opa value.
    /// </summary>
    Opa = 0,
    /// <summary>
    /// Represents the Open Fga value.
    /// </summary>
    OpenFga = 1
}

/// <summary>
/// Represents the MPolicy Decision Failure Mode.
/// </summary>
public enum MPolicyDecisionFailureMode
{
    /// <summary>
    /// Represents the Fallback To Local value.
    /// </summary>
    FallbackToLocal = 0,
    /// <summary>
    /// Represents the Deny value.
    /// </summary>
    Deny = 1
}

/// <summary>
/// Represents the MPolicy Decision Configs.
/// </summary>
public sealed class MPolicyDecisionConfigs
{
    /// <summary>
    /// The Section Name.
    /// </summary>
    public const string SectionName = "MPolicyDecision";

    /// <summary>
    /// Enables centralized PDP calls. If disabled, local RBAC is always used.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Selects remote decision provider protocol.
    /// </summary>
    public MPolicyDecisionProvider Provider { get; set; } = MPolicyDecisionProvider.Opa;

    /// <summary>
    /// Base endpoint of PDP service.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Relative decision path. If empty, provider default path is used.
    /// </summary>
    public string? DecisionPath { get; set; }

    /// <summary>
    /// Outbound request timeout for PDP calls.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// How runtime should behave when centralized PDP call fails.
    /// </summary>
    public MPolicyDecisionFailureMode FailureMode { get; set; } = MPolicyDecisionFailureMode.FallbackToLocal;

    /// <summary>
    /// Emits structured decision logs with tenant and correlation context.
    /// </summary>
    public bool EnableDecisionLogging { get; set; } = true;

    /// <summary>
    /// Optional static headers for PDP requests (for API key/service token).
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
