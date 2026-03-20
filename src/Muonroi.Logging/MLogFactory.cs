using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;

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
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly ISystemExecutionContextAccessor _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    private readonly IMLogContext _logContext = logContext ?? throw new ArgumentNullException(nameof(logContext));

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
    private readonly ILogger _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ISystemExecutionContextAccessor _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    private readonly IMLogContext _logContext = logContext ?? throw new ArgumentNullException(nameof(logContext));

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        using var scope = BeginExecutionScope();
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <inheritdoc />
    public IMLogContextScope BeginProperty(string key, object? value) => _logContext.PushProperty(key, value);

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
        var session = traceContext?.Current;
        if (session is { IsActive: true })
        {
            var message = string.Format(template.Replace("{", "{{").Replace("}", "}}"), args);
            if (ex != null) message += $" | Exception: {ex.Message}";
            session.Record($"[{level}] {message}");
        }
    }

    private IDisposable? BeginExecutionScope()
    {
        var context = _accessor.Get();
        if (context == null || context == SystemExecutionContext.Empty) return null;

        var t = _logContext.PushProperty(LogPropertyConventions.TenantId, context.TenantId);
        var u = _logContext.PushProperty(LogPropertyConventions.UserId, context.UserId);
        var c = _logContext.PushProperty(LogPropertyConventions.CorrelationId, context.CorrelationId);
        return new CombinedScope(t, new CombinedScope(u, c));
    }

    private sealed class CombinedScope(IDisposable? left, IDisposable? right) : IDisposable
    {
        public void Dispose() { right?.Dispose(); left?.Dispose(); }
    }
}
