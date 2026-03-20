namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Persists audit entries for ruleset actions.
/// </summary>
public interface IRuleSetAuditStore
{
    /// <summary>Appends an audit entry.</summary>
    /// <param name="entry">Audit entry to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AppendAsync(RuleSetAuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Queries audit entries.</summary>
    /// <param name="workflowName">Optional workflow filter.</param>
    /// <param name="page">Page number starting at 1.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged audit entries.</returns>
    Task<RuleSetAuditPage> QueryAsync(
        string? workflowName = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
