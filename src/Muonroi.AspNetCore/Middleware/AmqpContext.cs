namespace Muonroi.AspNetCore.Middleware;

/// <summary>
/// Provides context for AMQP (Advanced Message Queuing Protocol) operations, including header management.
/// </summary>
public class AmqpContext : IAmqpContext
{
    private readonly Dictionary<string, object> _headers = [];

    /// <summary>
    /// Clears all headers from the context.
    /// </summary>
    public void ClearHeaders()
    {
        _headers.Clear();
    }

    /// <summary>
    /// Adds a collection of headers to the context.
    /// </summary>
    /// <param name="headers">The headers to add.</param>
    public void AddHeaders(IDictionary<string, object> headers)
    {
        foreach (KeyValuePair<string, object> header in headers)
        {
            _headers[header.Key] = header.Value;
        }
    }

    /// <summary>
    /// Gets a header value by its key.
    /// </summary>
    /// <param name="key">The key of the header to retrieve.</param>
    /// <returns>The header value as a string, or null if the key is not found.</returns>
    public string? GetHeaderByKey(string key)
    {
        if (!_headers.TryGetValue(key, out object? value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.Default.GetString(bytes),
            string s => s,
            _ => value.ToString()
        };
    }
}
