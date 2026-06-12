namespace Muonroi.RuleEngine.Abstractions.Rules;

/// <summary>
/// Lifecycle state of a ruleset version.
/// </summary>
public enum RuleSetStatus
{
    /// <summary>Draft state.</summary>
    Draft = 0,
    /// <summary>Waiting for approval.</summary>
    PendingApproval = 1,
    /// <summary>Approved and ready for activation.</summary>
    Approved = 2,
    /// <summary>Rejected by the approver.</summary>
    Rejected = 3,
    /// <summary>Currently active.</summary>
    Active = 4,
    /// <summary>Superseded by a newer version.</summary>
    Superseded = 5,
    /// <summary>Rolled back after activation.</summary>
    RolledBack = 6
}
