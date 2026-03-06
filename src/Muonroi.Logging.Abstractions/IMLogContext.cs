namespace Muonroi.Logging.Abstractions;

public interface IMLogContext
{
    IMLogContextScope PushProperty(string key, object? value);
    IMLogContextScope PushProperties(IReadOnlyDictionary<string, object?> properties);
}

public interface IMLogContextScope : IDisposable
{
}
