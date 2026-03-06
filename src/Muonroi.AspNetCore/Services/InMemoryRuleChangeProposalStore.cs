using System.Collections.Concurrent;
using Muonroi.AspNetCore.Models.Changes;

namespace Muonroi.AspNetCore.Services;

public sealed class InMemoryRuleChangeProposalStore(IMDateTimeService dateTimeService) : IRuleChangeProposalStore
{
    private readonly ConcurrentDictionary<Guid, RuleChangeProposal> _proposals = new();

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
            OrderedRuleCodes = request.OrderedRuleCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ProposedBy = string.IsNullOrWhiteSpace(proposedBy) ? "system" : proposedBy,
            ProposedAtUtc = dateTimeService.UtcNow(),
            ReviewNote = request.Note,
            Status = ProposalStatus.Pending
        };

        _ = _proposals[proposal.ProposalId] = proposal;
        return Task.FromResult(Clone(proposal));
    }

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
