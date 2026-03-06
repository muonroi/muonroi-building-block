namespace Muonroi.Logging;

public sealed class MLogScopeFactory(ILoggerFactory loggerFactory) : ILogScopeFactory
{
    private readonly ILogger _logger =
        (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger("Muonroi.Scope");

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties)
    {
        return _logger.BeginScope(properties);
    }
}
