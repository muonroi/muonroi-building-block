namespace Muonroi.AspNetCore.Middleware;

public class AmqpContext : IAmqpContext
{
    private readonly Dictionary<string, object> _headers = [];

    public void ClearHeaders()
    {
        _headers.Clear();
    }

    public void AddHeaders(IDictionary<string, object> headers)
    {
        foreach (KeyValuePair<string, object> header in headers)
        {
            _headers[header.Key] = header.Value;
        }
    }

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
