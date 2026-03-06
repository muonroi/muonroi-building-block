namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Stores facts shared between rules during orchestration.
/// </summary>
public class FactBag
{
    private readonly Dictionary<string, object?> _facts = [];

    public object? this[string key]
    {
        get => _facts.TryGetValue(key, out object? value) ? value : null;
        set => _facts[key] = value!;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_facts.TryGetValue(key, out object? obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Adds or updates a fact.</summary>
    public void Set<T>(string key, T value)
    {
        _facts[key] = value;
    }

    /// <summary>Retrieves a fact by key.</summary>
    public T? Get<T>(string key)
    {
        if (!_facts.TryGetValue(key, out object? value) || value == null) return default;
        if (value is T t) return t;

        // Handle JsonElement from external engines (like Microsoft RulesEngine)
        if (value is System.Text.Json.JsonElement je)
        {
            try
            {
                object? converted = je.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number when typeof(T) == typeof(int) => je.GetInt32(),
                    System.Text.Json.JsonValueKind.Number when typeof(T) == typeof(long) => je.GetInt64(),
                    System.Text.Json.JsonValueKind.Number when typeof(T) == typeof(double) => je.GetDouble(),
                    System.Text.Json.JsonValueKind.Number when typeof(T) == typeof(decimal) => je.GetDecimal(),
                    System.Text.Json.JsonValueKind.String => je.GetString(),
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    _ => null
                };
                if (converted is T typed) return typed;
            }
            catch { /* ignore conversion errors and return default */ }
        }

        return default;
    }

    /// <summary>Returns a read-only view of stored facts.</summary>
    public IReadOnlyDictionary<string, object?> AsReadOnly()
    {
        return _facts;
    }

    /// <summary>Removes a fact from the bag.</summary>
    /// <param name="key">The fact key to remove.</param>
    /// <returns><c>true</c> when the fact existed and was removed.</returns>
    public bool Remove(string key)
    {
        return _facts.Remove(key);
    }

    public IEnumerable<string> Keys => _facts.Keys;
}