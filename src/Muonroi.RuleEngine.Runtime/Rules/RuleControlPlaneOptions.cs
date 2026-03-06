namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleControlPlaneOptions
{
    public const string SectionName = "RuleControlPlane";

    public bool RequireApproval { get; set; }

    public bool NotifyOnStateChange { get; set; } = true;

    public bool EnableCanary { get; set; } = true;

    public string AuditSignerKeyId { get; set; } = "ruleset-control-plane";

    public string? AuditPrivateKeyPem { get; set; }

    public string? AuditPrivateKeyPemPath { get; set; }
}

