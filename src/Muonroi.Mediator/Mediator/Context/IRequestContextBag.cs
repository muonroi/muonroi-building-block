namespace Muonroi.Mediator.Mediator.Context;

/// <summary>
/// A scoped, per-request property bag that allows pipeline behaviors to share data
/// without direct coupling. Resolved from DI as a scoped service.
/// </summary>
public interface IRequestContextBag
{
    /// <summary>Sets a value by key. Overwrites any existing value for the same key.</summary>
    void Set<T>(string key, T value);

    /// <summary>Returns the value for the given key, or default(T) if not present.</summary>
    T? Get<T>(string key);

    /// <summary>Returns true if the given key exists in the bag.</summary>
    bool Contains(string key);
}

/// <summary>Default in-memory implementation backed by a Dictionary.</summary>
internal sealed class MRequestContextBag : IRequestContextBag
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    public void Set<T>(string key, T value) => _data[key] = value;

    public T? Get<T>(string key)
        => _data.TryGetValue(key, out object? v) && v is T typed ? typed : default;

    public bool Contains(string key) => _data.ContainsKey(key);
}
