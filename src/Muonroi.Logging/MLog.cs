namespace Muonroi.Logging;

public sealed class MLog<T>(
    ILogger<T> inner,
    ISystemExecutionContextAccessor accessor,
    IMLogContext logContext) : IMLog<T>
{
    private readonly ILogger<T> _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ISystemExecutionContextAccessor _accessor =
        accessor ?? throw new ArgumentNullException(nameof(accessor));
    private readonly IMLogContext _logContext = logContext ?? throw new ArgumentNullException(nameof(logContext));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        IDisposable? contextScope = BeginExecutionScope();
        IDisposable? innerScope = _inner.BeginScope(state);
        return new CombinedScope(contextScope, innerScope);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

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

    public IMLogContextScope BeginProperty(string key, object? value)
    {
        return _logContext.PushProperty(key, value);
    }

    public void Info(string messageTemplate, params object[] args)
    {
        _inner.LogInformation(messageTemplate, args);
    }

    public void Warn(string messageTemplate, params object[] args)
    {
        _inner.LogWarning(messageTemplate, args);
    }

    public void Error(Exception? ex, string messageTemplate, params object[] args)
    {
        _inner.LogError(ex, messageTemplate, args);
    }

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
