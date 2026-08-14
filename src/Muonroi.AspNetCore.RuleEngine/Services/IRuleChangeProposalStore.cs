namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Defines a store for managing rule change proposals.
/// </summary>
public interface IRuleChangeProposalStore
{
    /// <summary>
    /// Proposes a new rule change.
    /// </summary>
    /// <param name="request">The proposal request.</param>
    /// <param name="proposedBy">The user proposing the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="RuleChangeProposal"/> representing the newly created proposal.</returns>
    Task<RuleChangeProposal> ProposeAsync(
        ProposeRuleChangeRequest request,
        string proposedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific proposal by its unique identifier.
    /// </summary>
    /// <param name="proposalId">The unique identifier of the proposal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="RuleChangeProposal"/> if found; otherwise, null.</returns>
    Task<RuleChangeProposal?> GetAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending rule change proposal.
    /// </summary>
    /// <param name="proposalId">The unique identifier of the proposal to approve.</param>
    /// <param name="reviewedBy">The user reviewing the proposal.</param>
    /// <param name="note">An optional note regarding the approval.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated <see cref="RuleChangeProposal"/> if successful; otherwise, null.</returns>
    Task<RuleChangeProposal?> ApproveAsync(
        Guid proposalId,
        string reviewedBy,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending rule change proposal.
    /// </summary>
    /// <param name="proposalId">The unique identifier of the proposal to reject.</param>
    /// <param name="reviewedBy">The user reviewing the proposal.</param>
    /// <param name="note">An optional note regarding the rejection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated <see cref="RuleChangeProposal"/> if successful; otherwise, null.</returns>
    Task<RuleChangeProposal?> RejectAsync(
        Guid proposalId,
        string reviewedBy,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all pending rule change proposals for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of pending <see cref="RuleChangeProposal"/> objects.</returns>
    Task<IReadOnlyList<RuleChangeProposal>> ListPendingAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
