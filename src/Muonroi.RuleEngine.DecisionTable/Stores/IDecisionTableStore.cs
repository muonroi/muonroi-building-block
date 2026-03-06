using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Stores;

public interface IDecisionTableStore
{
    Task<DecisionTablePageResult> QueryAsync(DecisionTableQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisionTableModel>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<DecisionTableModel?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(
        DecisionTableModel table,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
    Task<DecisionTableBulkResult> BulkUpsertAsync(
        IReadOnlyList<DecisionTableModel> tables,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
    Task<DecisionTableBulkResult> BulkDeleteAsync(
        IReadOnlyList<string> ids,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
    Task<bool> ReorderRowsAsync(
        string id,
        IReadOnlyList<string> orderedRowIds,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisionTableVersionSnapshot>> GetVersionHistoryAsync(
        string id,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<DecisionTableVersionSnapshot?> GetVersionAsync(
        string id,
        int version,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisionTableAuditEntry>> GetAuditTrailAsync(
        string? id = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
