namespace Muonroi.Logging;

/// <summary>
/// Implements <see cref="IMLogContext"/> using an <see cref="ILoggerFactory"/> to create logging scopes.
/// </summary>
/// <param name="factory">The <see cref="ILoggerFactory"/> used to create the internal logger.</param>
public sealed class MLogContext(ILoggerFactory factory) : IMLogContext
{
    private readonly ILoggerFactory _factory = MGuard.NotNull(factory);

    /// <inheritdoc />
    public IMLogContextScope PushProperty(string key, object? value)
    {
        return PushProperties(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [key] = value
        });
    }

    /// <inheritdoc />
    public IMLogContextScope PushProperties(IReadOnlyDictionary<string, object?> properties)
    {
        ILogger logger = _factory.CreateLogger("Muonroi.LogContext");
        IDisposable? scope = logger.BeginScope(properties);
        return new MLogContextScope(scope);
    }
}
