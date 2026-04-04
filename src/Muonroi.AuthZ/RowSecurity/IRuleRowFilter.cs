namespace Muonroi.AuthZ.RowSecurity;

using Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Applies rule-driven row-level filtering to a queryable.
/// Rules of type IRule&lt;RowFilterContext&lt;T&gt;&gt; are executed in order;
/// each rule narrows the query by modifying RowFilterContext.Query.
/// </summary>
public interface IRuleRowFilter<T>
{
    /// <summary>
    /// Applies rule-driven filtering to the supplied query context.
    /// </summary>
    Task<IQueryable<T>> ApplyAsync(
        RowFilterContext<T> context,
        CancellationToken cancellationToken = default);
}
