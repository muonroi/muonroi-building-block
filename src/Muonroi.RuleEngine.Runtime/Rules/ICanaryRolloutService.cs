namespace Muonroi.RuleEngine.Runtime.Rules;

public interface ICanaryRolloutService
{
    Task<CanaryRolloutRecord> StartCanaryAsync(
        StartCanaryRequest request,
        CancellationToken cancellationToken = default);

    Task PromoteCanaryAsync(
        Guid rolloutId,
        string promotedBy,
        CancellationToken cancellationToken = default);

    Task RollbackCanaryAsync(
        Guid rolloutId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> IsCanaryActiveForTenantAsync(
        string workflowName,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<int?> GetCanaryVersionForTenantAsync(
        string workflowName,
        string tenantId,
        CancellationToken cancellationToken = default);
}

