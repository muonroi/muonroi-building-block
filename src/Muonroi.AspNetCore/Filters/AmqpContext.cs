namespace Muonroi.AspNetCore.Filters;

/// <inheritdoc />
public class AmqpContext : IAmqpContext
{
    private readonly Dictionary<string, object> _headers = [];

/// <inheritdoc />
    public void ClearHeaders()
    {
        _headers.Clear();
    }

/// <inheritdoc />
    public void AddHeaders(IDictionary<string, object> headers)
    {
        foreach (KeyValuePair<string, object> header in headers) _headers.Add(header.Key, header.Value);
    }

/// <inheritdoc />
    public string? GetHeaderByKey(string headerKey)
    {
        _ = ((IDictionary<string, object>)_headers).TryGetValue(headerKey, out object? value);

        if (value == null)
            return null;

        return value switch
        {
            byte[] bytes => Encoding.Default.GetString(bytes),
            string s => s,
            _ => value.ToString()
        };
    }
}
