namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Handles approval workflows for ruleset versions.
/// </summary>
public interface IRuleSetApprovalService
{
    /// <summary>Submits a ruleset version for approval.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Ruleset version.</param>
    /// <param name="submittedBy">Actor submitting the ruleset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated ruleset record.</returns>
    Task<RuleSetRecord> SubmitForApprovalAsync(
        string workflowName,
        int version,
        string submittedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Approves a ruleset version.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Ruleset version.</param>
    /// <param name="approvedBy">Actor approving the ruleset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated ruleset record.</returns>
    Task<RuleSetRecord> ApproveAsync(
        string workflowName,
        int version,
        string approvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Rejects a ruleset version.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Ruleset version.</param>
    /// <param name="rejectedBy">Actor rejecting the ruleset.</param>
    /// <param name="reason">Reason for rejection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated ruleset record.</returns>
    Task<RuleSetRecord> RejectAsync(
        string workflowName,
        int version,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default);
}

