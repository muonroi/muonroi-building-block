namespace Muonroi.RuleEngine.Runtime.Rules;

public interface IRuleSetApprovalService
{
    Task<RuleSetRecord> SubmitForApprovalAsync(
        string workflowName,
        int version,
        string submittedBy,
        CancellationToken cancellationToken = default);

    Task<RuleSetRecord> ApproveAsync(
        string workflowName,
        int version,
        string approvedBy,
        CancellationToken cancellationToken = default);

    Task<RuleSetRecord> RejectAsync(
        string workflowName,
        int version,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default);
}

