namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Configuration options for the runtime rule control plane.
/// </summary>
public sealed class RuleControlPlaneOptions
{
    /// <summary>Configuration section name used to bind these options.</summary>
    public const string SectionName = "RuleControlPlane";

    /// <summary>Requires approval before rule changes are applied.</summary>
    public bool RequireApproval { get; set; }

    /// <summary>Emits notifications when the control plane state changes.</summary>
    public bool NotifyOnStateChange { get; set; } = true;

    /// <summary>Enables canary rollout behavior for rule changes.</summary>
    public bool EnableCanary { get; set; } = true;

    /// <summary>Identifier of the key used to sign audit records.</summary>
    public string AuditSignerKeyId { get; set; } = "ruleset-control-plane";

    /// <summary>PEM-encoded private key used for audit signing, if supplied inline.</summary>
    public string? AuditPrivateKeyPem { get; set; }

    /// <summary>File path to the PEM-encoded private key used for audit signing.</summary>
    public string? AuditPrivateKeyPemPath { get; set; }
}

