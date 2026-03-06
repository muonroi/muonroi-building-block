using Muonroi.AspNetCore.Models.Changes;

namespace Muonroi.AspNetCore.Services;

public interface IRuleChangeProposalStore
{
    Task<RuleChangeProposal> ProposeAsync(
        ProposeRuleChangeRequest request,
        string proposedBy,
        CancellationToken cancellationToken = default);

    Task<RuleChangeProposal?> GetAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<RuleChangeProposal?> ApproveAsync(
        Guid proposalId,
        string reviewedBy,
        string? note,
        CancellationToken cancellationToken = default);

    Task<RuleChangeProposal?> RejectAsync(
        Guid proposalId,
        string reviewedBy,
        string? note,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleChangeProposal>> ListPendingAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
