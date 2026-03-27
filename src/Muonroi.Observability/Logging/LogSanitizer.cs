namespace Muonroi.Observability.Logging;

/// <summary>
/// Default implementation of <see cref="ILogSanitizer"/> using a deny-list.
/// </summary>
public class LogSanitizer(IEnumerable<string>? denyList = null) : ILogSanitizer
{
    private readonly HashSet<string> _denyList =
        denyList is null ? [] : new HashSet<string>(denyList, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IDictionary<string, object?> Sanitize(IDictionary<string, object?> data)
    {
        foreach (string key in data.Keys)
        {
            if (_denyList.Contains(key))
            {
                data[key] = "***";
            }
        }

        return data;
    }
}
