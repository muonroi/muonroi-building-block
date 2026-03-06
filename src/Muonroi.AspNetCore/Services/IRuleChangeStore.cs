using Muonroi.AspNetCore.Models.Changes;

namespace Muonroi.AspNetCore.Services;

public interface IRuleChangeStore
{
    Task<IReadOnlyList<string>> GetCurrentAsync(
        string tenantId,
        string endpointRoute,
        CancellationToken cancellationToken = default);

    Task<RuleChangeRecord> ApplyAsync(
        RuleOrderChangeRequest request,
        string appliedBy,
        CancellationToken cancellationToken = default);

    Task<RuleChangeRecord?> RollbackAsync(
        string tenantId,
        string endpointRoute,
        string appliedBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleChangeRecord>> GetHistoryAsync(
        string tenantId,
        string endpointRoute,
        CancellationToken cancellationToken = default);
}
