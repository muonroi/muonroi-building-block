namespace Muonroi.Core.Abstractions.Interfaces;

public interface IAmqpContext
{
    string? GetHeaderByKey(string key);
    void AddHeaders(IDictionary<string, object> headers);
    void ClearHeaders();
}
