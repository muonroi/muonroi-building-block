namespace Muonroi.RuleEngine.Runtime.Rules;

public interface IRuleSetAuditStore
{
    Task AppendAsync(RuleSetAuditEntry entry, CancellationToken cancellationToken = default);

    Task<RuleSetAuditPage> QueryAsync(
        string? workflowName = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
