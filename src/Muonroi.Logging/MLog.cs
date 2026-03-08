namespace Muonroi.Logging;

/// <summary>
/// A custom logger implementation that provides additional context and helper methods.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
/// <param name="inner">The underlying <see cref="ILogger{T}"/> instance.</param>
/// <param name="accessor">The execution context accessor to retrieve context data.</param>
/// <param name="logContext">The logging context used to push properties.</param>
public sealed class MLog<T>(
    ILogger<T> inner,
    ISystemExecutionContextAccessor accessor,
    IMLogContext logContext) : IMLog<T>
{
    private readonly ILogger<T> _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ISystemExecutionContextAccessor _accessor =
        accessor ?? throw new ArgumentNullException(nameof(accessor));
    private readonly IMLogContext _logContext = logContext ?? throw new ArgumentNullException(nameof(logContext));

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        IDisposable? contextScope = BeginExecutionScope();
        IDisposable? innerScope = _inner.BeginScope(state);
        return new CombinedScope(contextScope, innerScope);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        using IDisposable? scope = BeginExecutionScope();
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <inheritdoc />
    public IMLogContextScope BeginProperty(string key, object? value)
    {
        return _logContext.PushProperty(key, value);
    }

    /// <inheritdoc />
    public void Info(string messageTemplate, params object[] args)
    {
        _inner.LogInformation(messageTemplate, args);
    }

    /// <inheritdoc />
    public void Warn(string messageTemplate, params object[] args)
    {
        _inner.LogWarning(messageTemplate, args);
    }

    /// <inheritdoc />
    public void Error(Exception? ex, string messageTemplate, params object[] args)
    {
        _inner.LogError(ex, messageTemplate, args);
    }

    /// <inheritdoc />
    public void Debug(string messageTemplate, params object[] args)
    {
        _inner.LogDebug(messageTemplate, args);
    }

    private IDisposable? BeginExecutionScope()
    {
        ISystemExecutionContext context = _accessor.Get();
        return _inner.BeginScope(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TenantId"] = context.TenantId,
            ["UserId"] = context.UserId,
            ["CorrelationId"] = context.CorrelationId,
            ["SourceType"] = context.SourceType
        });
    }

    private sealed class CombinedScope(IDisposable? left, IDisposable? right) : IDisposable
    {
        private readonly IDisposable? _left = left;
        private readonly IDisposable? _right = right;

        public void Dispose()
        {
            _right?.Dispose();
            _left?.Dispose();
        }
    }
}
