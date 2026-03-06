namespace Muonroi.Core.Abstractions.Interfaces;

public interface IMJsonSerializeService
{
    string Serialize<T>(T obj);

    T? Deserialize<T>(string text);
}
