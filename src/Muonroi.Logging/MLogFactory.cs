









using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Logging;

/// <summary>
/// Default implementation of <see cref="IMLogFactory"/>.
/// </summary>
public sealed class MLogFactory(
    ILoggerFactory loggerFactory,
    ISystemExecutionContextAccessor accessor,
    IMLogContext logContext,
    IMTraceContext? traceContext = null) : IMLogFactory
{
    private readonly ILoggerFactory _loggerFactory = MGuard.NotNull(loggerFactory);
    private readonly ISystemExecutionContextAccessor _accessor = MGuard.NotNull(accessor);
    private readonly IMLogContext _logContext = MGuard.NotNull(logContext);

    /// <inheritdoc />
    public IMLog<T> CreateLogger<T>()
    {
        return new MLog<T>(_loggerFactory.CreateLogger<T>(), _accessor, _logContext, traceContext);
    }

    /// <inheritdoc />
    public IMLog CreateLogger(string categoryName)
    {
        return new MLogNonGeneric(_loggerFactory.CreateLogger(categoryName), _accessor, _logContext, traceContext);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}

/// <summary>
/// Non-generic implementation of <see cref="IMLog"/>.
/// </summary>
internal sealed class MLogNonGeneric(
    ILogger inner,
    ISystemExecutionContextAccessor accessor,
    IMLogContext logContext,
    IMTraceContext? traceContext = null) : IMLog
{
    private readonly ILogger _inner = MGuard.NotNull(inner);
    private readonly ISystemExecutionContextAccessor _accessor = MGuard.NotNull(accessor);
    private readonly IMLogContext _logContext = MGuard.NotNull(logContext);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
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
    public void Info(string messageTemplate, params object?[] args)
    {
        _inner.LogInformation(messageTemplate, args);
        RecordTrace("INFO", messageTemplate, args);
    }

    /// <inheritdoc />
    public void Warn(string messageTemplate, params object?[] args)
    {
        _inner.LogWarning(messageTemplate, args);
        RecordTrace("WARN", messageTemplate, args);
    }

    /// <inheritdoc />
    public void Error(Exception? ex, string messageTemplate, params object?[] args)
    {
        _inner.LogError(ex, messageTemplate, args);
        RecordTrace("ERROR", messageTemplate, args, ex);
    }

    /// <inheritdoc />
    public void Debug(string messageTemplate, params object?[] args)
    {
        _inner.LogDebug(messageTemplate, args);
        RecordTrace("DEBUG", messageTemplate, args);
    }

    /// <inheritdoc />
    public void InfoTrace(string messageTemplate, params object?[] args)
    {
        _inner.LogInformation(messageTemplate, args);
        RecordTrace("TRACE", messageTemplate, args);
    }

    private void RecordTrace(string level, string template, object?[] args, Exception? ex = null)
    {
        ITraceSession? session = traceContext?.Current;
        if (session is { IsActive: true })
        {
            string message = string.Format(template.Replace("{", "{{").Replace("}", "}}"), args);
            if (ex != null)
            {
                message += $" | Exception: {ex.Message}";
            }

            session.Record($"[{level}] {message}");
        }
    }

    private IDisposable? BeginExecutionScope()
    {
        ISystemExecutionContext context = _accessor.Get();
        if (context == null || context == SystemExecutionContext.Empty)
        {
            return null;
        }

        IMLogContextScope t = _logContext.PushProperty(LogPropertyConventions.TenantId, context.TenantId);
        IMLogContextScope u = _logContext.PushProperty(LogPropertyConventions.UserId, context.UserId);
        IMLogContextScope c = _logContext.PushProperty(LogPropertyConventions.CorrelationId, context.CorrelationId);
        return new CombinedScope(t, new CombinedScope(u, c));
    }

    private sealed class CombinedScope(IDisposable? left, IDisposable? right) : IDisposable
    {
        public void Dispose() { right?.Dispose(); left?.Dispose(); }
    }
}
