namespace Muonroi.Logging;

public sealed class MLogContext(ILoggerFactory factory) : IMLogContext
{
    private readonly ILoggerFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public IMLogContextScope PushProperty(string key, object? value)
    {
        return PushProperties(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        });
    }

    public IMLogContextScope PushProperties(IReadOnlyDictionary<string, object?> properties)
    {
        ILogger logger = _factory.CreateLogger("Muonroi.LogContext");
        IDisposable? scope = logger.BeginScope(properties);
        return new MLogContextScope(scope);
    }
}
