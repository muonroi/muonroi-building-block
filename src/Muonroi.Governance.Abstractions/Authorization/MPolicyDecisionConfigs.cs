namespace Muonroi.Governance.Authorization;

public enum MPolicyDecisionProvider
{
    Opa = 0,
    OpenFga = 1
}

public enum MPolicyDecisionFailureMode
{
    FallbackToLocal = 0,
    Deny = 1
}

public sealed class MPolicyDecisionConfigs
{
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
