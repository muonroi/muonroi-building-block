namespace Muonroi.AuthZ.RowSecurity;

using Muonroi.Core.Abstractions.Guards;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Logging.Abstractions;

internal sealed class RuleRowFilter<T>(
    IMRuleOrchestrator<RowFilterContext<T>> orchestrator,
    IMLog<RuleRowFilter<T>>? logger = null)
    : IRuleRowFilter<T>
{
    public async Task<IQueryable<T>> ApplyAsync(
        RowFilterContext<T> context,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(context);

        OrchestratorResult result = await orchestrator.ExecuteAsync(context, cancellationToken);

        if (!result.IsSuccess)
        {
            string failureReason = result.Errors.Count > 0 ? result.Errors[0] : "Unknown";
            logger?.Warn("[RLS] Row filter rules failed for User:{UserId} — returning empty set. Reason:{Reason}",
                context.UserId, failureReason);
            return Enumerable.Empty<T>().AsQueryable();
        }

        return context.Query;
    }
}
