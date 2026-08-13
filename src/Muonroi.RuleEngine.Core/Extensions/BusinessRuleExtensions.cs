using Microsoft.Extensions.Caching.Memory;
using Muonroi.RuleEngine.Abstractions.Models;

namespace Muonroi.RuleEngine.Core.Extensions;

internal sealed class CompositeBusinessRule<TContext>(
    IBusinessRule<TContext> left,
    IBusinessRule<TContext> right,
    Func<bool, bool, bool> merge,
    string @operator) : IBusinessRule<TContext>
{
    public string Code { get; } = $"{left.Code} {@operator} {right.Code}";

    public async Task<bool> IsSatisfiedAsync(TContext context, CancellationToken cancellationToken = default)
    {
        bool l = await left.IsSatisfiedAsync(context, cancellationToken).ConfigureAwait(false);
        bool r = await right.IsSatisfiedAsync(context, cancellationToken).ConfigureAwait(false);
        return merge(l, r);
    }
}

internal sealed class CachedBusinessRule<TContext>(
    IBusinessRule<TContext> inner,
    IMemoryCache cache,
    TimeSpan? duration = null) : IBusinessRule<TContext>
{
    private readonly TimeSpan _duration = duration ?? TimeSpan.FromMinutes(5);

    public string Code => inner.Code;

    public async Task<bool> IsSatisfiedAsync(TContext context, CancellationToken cancellationToken = default)
    {
        string key = $"{Code}:{context?.GetHashCode() ?? 0}";
        if (cache.TryGetValue(key, out bool result)) return result;

        result = await inner.IsSatisfiedAsync(context, cancellationToken).ConfigureAwait(false);
        cache.Set(key, result, _duration);
        return result;
    }
}

internal sealed class MappedBusinessRule<TOuter, TInner>(IBusinessRule<TInner> inner, Func<TOuter, TInner> map)
    : IBusinessRule<TOuter>
{
    public string Code { get; } = inner.Code;

    public Task<bool> IsSatisfiedAsync(TOuter context, CancellationToken cancellationToken = default)
    {
        return inner.IsSatisfiedAsync(map(context), cancellationToken);
    }
}

/// <summary>
/// Extension helpers for composing <see cref="IBusinessRule{TContext}"/> instances.
/// </summary>
public static class BusinessRuleExtensions
{
    /// <summary>
    /// Combines two rules using a logical AND operation.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="left">The left rule.</param>
    /// <param name="right">The right rule.</param>
    /// <returns>A new rule representing the logical AND of the two rules.</returns>
    public static IBusinessRule<TContext> And<TContext>(this IBusinessRule<TContext> left,
        IBusinessRule<TContext> right)
    {
        return new CompositeBusinessRule<TContext>(left, right, static (l, r) => l && r, "AND");
    }

    /// <summary>
    /// Combines two rules using a logical OR operation.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="left">The left rule.</param>
    /// <param name="right">The right rule.</param>
    /// <returns>A new rule representing the logical OR of the two rules.</returns>
    public static IBusinessRule<TContext> Or<TContext>(this IBusinessRule<TContext> left, IBusinessRule<TContext> right)
    {
        return new CompositeBusinessRule<TContext>(left, right, static (l, r) => l || r, "OR");
    }

    /// <summary>
    /// Wraps a rule with a caching layer.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="rule">The rule to cache.</param>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="duration">Optional duration for the cache. Defaults to 5 minutes.</param>
    /// <returns>A cached version of the rule.</returns>
    public static IBusinessRule<TContext> Cache<TContext>(this IBusinessRule<TContext> rule, IMemoryCache cache,
        TimeSpan? duration = null)
    {
        return new CachedBusinessRule<TContext>(rule, cache, duration);
    }

    /// <summary>
    /// Adapts a rule from one context type to another.
    /// </summary>
    /// <typeparam name="TOuter">The outer context type.</typeparam>
    /// <typeparam name="TInner">The inner context type.</typeparam>
    /// <param name="rule">The rule to adapt.</param>
    /// <param name="map">A function to map the outer context to the inner context.</param>
    /// <returns>An adapted rule.</returns>
    public static IBusinessRule<TOuter> Adapt<TOuter, TInner>(this IBusinessRule<TInner> rule, Func<TOuter, TInner> map)
    {
        return new MappedBusinessRule<TOuter, TInner>(rule, map);
    }
}
