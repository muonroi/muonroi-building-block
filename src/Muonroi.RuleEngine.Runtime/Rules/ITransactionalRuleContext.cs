


namespace Muonroi.RuleEngine.Runtime.Rules;


/// <summary>
/// Represents a context capable of handling transactions during rule execution.
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
    Task<IDbContextTransaction?> BeginTransactionAsync();

    /// <summary>
    /// Commits the provided transaction.
    /// </summary>
    Task CommitTransactionAsync(IDbContextTransaction transaction);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    void RollbackTransaction();
}
