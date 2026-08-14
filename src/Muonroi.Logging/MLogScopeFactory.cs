namespace Muonroi.Logging;

/// <summary>
/// A factory for creating logging scopes based on an <see cref="ILoggerFactory"/>.
/// </summary>
/// <param name="loggerFactory">The logger factory used to create loggers for the scopes.</param>
public sealed class MLogScopeFactory(ILoggerFactory loggerFactory) : ILogScopeFactory
{
    private readonly ILogger _logger = MGuard.NotNull(loggerFactory).CreateLogger("Muonroi.Scope");

    /// <inheritdoc />
    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties)
    {
        return _logger.BeginScope(properties);
    }
}
