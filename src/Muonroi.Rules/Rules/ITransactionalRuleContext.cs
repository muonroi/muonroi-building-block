namespace Muonroi.Rules.Rules;


/// <summary>
/// Represents a context capable of handling transactions during rule execution.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
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
