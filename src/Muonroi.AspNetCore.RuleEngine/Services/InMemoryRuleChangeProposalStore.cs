namespace Muonroi.AspNetCore.Services;

/// <summary>
/// An in-memory implementation of <see cref="IRuleChangeProposalStore"/> for testing and local development.
/// </summary>
/// <param name="dateTimeService">The date time service.</param>
public sealed class InMemoryRuleChangeProposalStore(IMDateTimeService dateTimeService) : IRuleChangeProposalStore
{
    private readonly ConcurrentDictionary<Guid, RuleChangeProposal> _proposals = new();

    /// <summary>
    /// Proposes a new rule change asynchronously.
    /// </summary>
    /// <param name="request">The proposal request.</param>
    /// <param name="proposedBy">The user proposing the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="RuleChangeProposal"/> representing the newly created proposal.</returns>
    public Task<RuleChangeProposal> ProposeAsync(
        ProposeRuleChangeRequest request,
        string proposedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RuleChangeProposal proposal = new()
        {
            TenantId = NormalizeTenantId(request.TenantId),
            EndpointRoute = NormalizeRoute(request.EndpointRoute),
            OrderedRuleCodes = [.. request.OrderedRuleCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)],
            ProposedBy = string.IsNullOrWhiteSpace(proposedBy) ? "system" : proposedBy,
            ProposedAtUtc = dateTimeService.UtcNow(),
            ReviewNote = request.Note,
            Status = ProposalStatus.Pending
        };

        _ = _proposals[proposal.ProposalId] = proposal;
        return Task.FromResult(Clone(proposal));
    }

    /// <summary>
    /// Gets a specific proposal by its unique identifier asynchronously.
    /// </summary>
    /// <param name="proposalId">The unique identifier of the proposal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="RuleChangeProposal"/> if found; otherwise, null.</returns>
    public Task<RuleChangeProposal?> GetAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_proposals.TryGetValue(proposalId, out RuleChangeProposal? proposal))
        {
            return Task.FromResult<RuleChangeProposal?>(null);
        }

        return Task.FromResult<RuleChangeProposal?>(Clone(proposal));
    }

    /// <summary>
    /// Approves a pending rule change proposal asynchronously.
    /// </summary>
    /// <param name="proposalId">The unique identifier of the proposal to approve.</param>
    /// <param name="reviewedBy">The user reviewing the proposal.</param>
    /// <param name="note">An optional note regarding the approval.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated <see cref="RuleChangeProposal"/> if successful; otherwise, null.</returns>
    public Task<RuleChangeProposal?> ApproveAsync(
        Guid proposalId,
        string reviewedBy,
        string? note,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_proposals.TryGetValue(proposalId, out RuleChangeProposal? proposal))
        {
            return Task.FromResult<RuleChangeProposal?>(null);
        }

        lock (proposal)
        {
            if (proposal.Status != ProposalStatus.Pending)
            {
                return Task.FromResult<RuleChangeProposal?>(Clone(proposal));
            }

            proposal.Status = ProposalStatus.Approved;
            proposal.ReviewedBy = string.IsNullOrWhiteSpace(reviewedBy) ? "system" : reviewedBy;
            proposal.ReviewedAtUtc = dateTimeService.UtcNow();
            proposal.ReviewNote = note;
            return Task.FromResult<RuleChangeProposal?>(Clone(proposal));
        }
    }

    /// <summary>
    /// Rejects a pending rule change proposal asynchronously.
    /// </summary>
    /// <param name="proposalId">The unique identifier of the proposal to reject.</param>
    /// <param name="reviewedBy">The user reviewing the proposal.</param>
    /// <param name="note">An optional note regarding the rejection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated <see cref="RuleChangeProposal"/> if successful; otherwise, null.</returns>
    public Task<RuleChangeProposal?> RejectAsync(
        Guid proposalId,
        string reviewedBy,
        string? note,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_proposals.TryGetValue(proposalId, out RuleChangeProposal? proposal))
        {
            return Task.FromResult<RuleChangeProposal?>(null);
        }

        lock (proposal)
        {
            if (proposal.Status != ProposalStatus.Pending)
            {
                return Task.FromResult<RuleChangeProposal?>(Clone(proposal));
            }

            proposal.Status = ProposalStatus.Rejected;
            proposal.ReviewedBy = string.IsNullOrWhiteSpace(reviewedBy) ? "system" : reviewedBy;
            proposal.ReviewedAtUtc = dateTimeService.UtcNow();
            proposal.ReviewNote = note;
            return Task.FromResult<RuleChangeProposal?>(Clone(proposal));
        }
    }

    /// <summary>
    /// Lists all pending rule change proposals for a specific tenant asynchronously.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of pending <see cref="RuleChangeProposal"/> objects.</returns>
    public Task<IReadOnlyList<RuleChangeProposal>> ListPendingAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedTenantId = NormalizeTenantId(tenantId);

        IReadOnlyList<RuleChangeProposal> pending = [.. _proposals.Values
            .Where(x => x.Status == ProposalStatus.Pending &&
                        string.Equals(x.TenantId, normalizedTenantId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ProposedAtUtc)
            .Select(Clone)];

        return Task.FromResult(pending);
    }

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim();
    }

    private static string NormalizeRoute(string? endpointRoute)
    {
        if (string.IsNullOrWhiteSpace(endpointRoute))
        {
            return "/";
        }

        string route = endpointRoute.Trim();
        if (!route.StartsWith('/'))
        {
            route = "/" + route;
        }

        while (route.Contains("//", StringComparison.Ordinal))
        {
            route = route.Replace("//", "/", StringComparison.Ordinal);
        }

        return route;
    }

    private static RuleChangeProposal Clone(RuleChangeProposal proposal)
    {
        return new RuleChangeProposal
        {
            ProposalId = proposal.ProposalId,
            TenantId = proposal.TenantId,
            EndpointRoute = proposal.EndpointRoute,
            OrderedRuleCodes = [.. proposal.OrderedRuleCodes],
            ProposedBy = proposal.ProposedBy,
            ProposedAtUtc = proposal.ProposedAtUtc,
            Status = proposal.Status,
            ReviewedBy = proposal.ReviewedBy,
            ReviewedAtUtc = proposal.ReviewedAtUtc,
            ReviewNote = proposal.ReviewNote
        };
    }
}
