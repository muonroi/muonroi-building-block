using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Stores;

/// <summary>
/// Defines the contract for storing and retrieving decision tables.
/// </summary>
public interface IDecisionTableStore
{
    /// <summary>
    /// Queries decision tables based on the specified criteria.
    /// </summary>
    /// <param name="query">The query criteria.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged result of decision tables.</returns>
    Task<DecisionTablePageResult> QueryAsync(DecisionTableQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all decision tables with pagination.
    /// </summary>
    /// <param name="page">The page number (starting from 1).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of decision tables.</returns>
    Task<IReadOnlyList<DecisionTableModel>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a decision table by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the decision table.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decision table if found; otherwise, null.</returns>
    Task<DecisionTableModel?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a decision table.
    /// </summary>
    /// <param name="table">The decision table to save.</param>
    /// <param name="actor">The user or system performing the action.</param>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(
        DecisionTableModel table,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a bulk upsert (insert or update) of decision tables.
    /// </summary>
    /// <param name="tables">The list of decision tables to upsert.</param>
    /// <param name="actor">The user or system performing the action.</param>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the bulk operation.</returns>
    Task<DecisionTableBulkResult> BulkUpsertAsync(
        IReadOnlyList<DecisionTableModel> tables,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a bulk deletion of decision tables.
    /// </summary>
    /// <param name="ids">The identifiers of the decision tables to delete.</param>
    /// <param name="actor">The user or system performing the action.</param>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the bulk operation.</returns>
    Task<DecisionTableBulkResult> BulkDeleteAsync(
        IReadOnlyList<string> ids,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders the rows of a decision table.
    /// </summary>
    /// <param name="id">The unique identifier of the decision table.</param>
    /// <param name="orderedRowIds">The list of row identifiers in their new order.</param>
    /// <param name="actor">The user or system performing the action.</param>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the reordering was successful; otherwise, false.</returns>
    Task<bool> ReorderRowsAsync(
        string id,
        IReadOnlyList<string> orderedRowIds,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the version history of a decision table.
    /// </summary>
    /// <param name="id">The unique identifier of the decision table.</param>
    /// <param name="page">The page number (starting from 1).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of version snapshots.</returns>
    Task<IReadOnlyList<DecisionTableVersionSnapshot>> GetVersionHistoryAsync(
        string id,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version of a decision table.
    /// </summary>
    /// <param name="id">The unique identifier of the decision table.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version snapshot if found; otherwise, null.</returns>
    Task<DecisionTableVersionSnapshot?> GetVersionAsync(
        string id,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the audit trail for decision tables.
    /// </summary>
    /// <param name="id">Optional unique identifier to filter audit entries for a specific table.</param>
    /// <param name="page">The page number (starting from 1).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of audit entries.</returns>
    Task<IReadOnlyList<DecisionTableAuditEntry>> GetAuditTrailAsync(
        string? id = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a decision table by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the decision table.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
