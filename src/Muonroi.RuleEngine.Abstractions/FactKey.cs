namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Typed key for <see cref="FactBag"/> access — eliminates stringly-typed key errors.
/// Declare as static readonly fields in a constants class per domain.
///
/// <example>
/// <code>
/// public static class OrderFacts
/// {
///     public static readonly FactKey&lt;string&gt; BookingNo = new("mapped.BookingNo");
///     public static readonly FactKey&lt;int&gt; OrderId = new("mapped.OrderId");
/// }
///
/// // Usage:
/// facts.Set(OrderFacts.BookingNo, "BK-2026-001");
/// string? booking = facts.Get(OrderFacts.BookingNo); // typed, no typo possible
/// </code>
/// </example>
/// </summary>
/// <typeparam name="T">The type of value stored under this key.</typeparam>
public readonly record struct FactKey<T>(string Key)
{
    /// <summary>Implicit conversion to string for backward compatibility with raw key APIs.</summary>
    public static implicit operator string(FactKey<T> key) => key.Key;

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>
/// Extension methods for typed <see cref="FactKey{T}"/> access on <see cref="FactBag"/>.
/// </summary>
public static class FactKeyExtensions
{
    /// <summary>Sets a fact using a typed key.</summary>
    public static void Set<T>(this FactBag facts, FactKey<T> key, T value)
        => facts.Set(key.Key, value);

    /// <summary>Gets a fact using a typed key.</summary>
    public static T? Get<T>(this FactBag facts, FactKey<T> key)
        => facts.Get<T>(key.Key);

    /// <summary>Tries to get a fact using a typed key.</summary>
    public static bool TryGet<T>(this FactBag facts, FactKey<T> key, out T? value)
        => facts.TryGet(key.Key, out value);

    /// <summary>Removes a fact using a typed key.</summary>
    public static bool Remove<T>(this FactBag facts, FactKey<T> key)
        => facts.Remove(key.Key);
}
