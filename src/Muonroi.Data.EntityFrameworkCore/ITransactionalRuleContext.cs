using Microsoft.EntityFrameworkCore.Storage;

namespace Muonroi.Data.EntityFrameworkCore;

/// <summary>
/// Represents a context capable of handling transactions during rule execution.
/// Defined locally to avoid circular dependency on Muonroi.RuleEngine.Runtime.
/// </summary>
public interface ITransactionalRuleContext
{
    /// <summary>
    /// Gets whether there is an active transaction.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <returns>The started transaction or null.</returns>
    Task<IDbContextTransaction?> BeginTransactionAsync();

    /// <summary>
    /// Commits the provided transaction.
    /// </summary>
    /// <param name="transaction">Transaction to commit.</param>
    Task CommitTransactionAsync(IDbContextTransaction transaction);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    void RollbackTransaction();
}
