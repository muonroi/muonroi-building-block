










using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;
using Muonroi.Logging.Abstractions.Exceptions;

namespace Muonroi.Logging;

/// <summary>
/// Default implementation of <see cref="IMLogFactory"/>.
/// </summary>
public sealed class MLogFactory(
    ILoggerFactory loggerFactory,
    ISystemExecutionContextAccessor accessor,
    IMLogContext logContext,
    IMTraceContext? traceContext = null,
    IMLogArgumentResolver? resolver = null,
    IExceptionClassifier? exceptionClassifier = null,
    IInterceptedLogWriter? interceptor = null) : IMLogFactory
{
    private readonly ILoggerFactory _loggerFactory = MGuard.NotNull(loggerFactory);
    private readonly ISystemExecutionContextAccessor _accessor = MGuard.NotNull(accessor);
    private readonly IMLogContext _logContext = MGuard.NotNull(logContext);
    private readonly IMLogArgumentResolver _resolver = resolver ?? new MLogArgumentResolver();
    private readonly IExceptionClassifier? _exceptionClassifier = exceptionClassifier;
    private readonly IInterceptedLogWriter _interceptor = interceptor ?? new InterceptedLogWriter();

    /// <inheritdoc />
    public IMLog<T> CreateLogger<T>()
    {
        return new MLog<T>(_loggerFactory.CreateLogger<T>(), _accessor, _logContext, traceContext, _resolver, _exceptionClassifier, _interceptor);
    }

    /// <inheritdoc />
    public IMLog CreateLogger(string categoryName)
    {
        return new MLogNonGeneric(_loggerFactory.CreateLogger(categoryName), _accessor, _logContext, traceContext, _resolver, _exceptionClassifier, _interceptor);
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
    IMTraceContext? traceContext = null,
    IMLogArgumentResolver? resolver = null,
    IExceptionClassifier? exceptionClassifier = null,
    IInterceptedLogWriter? interceptor = null) : IMLog
{
    private readonly ILogger _inner = MGuard.NotNull(inner);
    private readonly ISystemExecutionContextAccessor _accessor = MGuard.NotNull(accessor);
    private readonly IMLogContext _logContext = MGuard.NotNull(logContext);
    private readonly IMLogArgumentResolver _resolver = resolver ?? new MLogArgumentResolver();
    private readonly IExceptionClassifier? _exceptionClassifier = exceptionClassifier;
    private readonly IInterceptedLogWriter _interceptor = interceptor ?? new InterceptedLogWriter();

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
        if (exception != null)
        {
            _interceptor.Write(logLevel, _inner, exception, formatter(state, exception));
        }
        else
        {
            _interceptor.Write(logLevel, _inner, formatter(state, exception));
        }
    }

    /// <inheritdoc />
    public IMLogContextScope BeginProperty(string key, object? value)
    {
        return _logContext.PushProperty(key, value);
    }

    /// <inheritdoc />
    public void Info(string messageTemplate, params object?[] args)
    {
        _interceptor.Write(LogLevel.Information, _inner, messageTemplate, args);
        RecordTrace("INFO", messageTemplate, args);
    }

    /// <inheritdoc />
    public void Warn(string messageTemplate, params object?[] args)
    {
        _interceptor.Write(LogLevel.Warning, _inner, messageTemplate, args);
        RecordTrace("WARN", messageTemplate, args);
    }

    /// <inheritdoc />
    public void Error(Exception? ex, string messageTemplate, params object?[] args)
    {
        if (ex != null)
        {
            _interceptor.Write(LogLevel.Error, _inner, ex, messageTemplate, args);
        }
        else
        {
            _interceptor.Write(LogLevel.Error, _inner, messageTemplate, args);
        }
        RecordTrace("ERROR", messageTemplate, args, ex);
    }

    /// <inheritdoc />
    public void Debug(string messageTemplate, params object?[] args)
    {
        _interceptor.Write(LogLevel.Debug, _inner, messageTemplate, args);
        RecordTrace("DEBUG", messageTemplate, args);
    }

    /// <inheritdoc />
    public void InfoTrace(string messageTemplate, params object?[] args)
    {
        _interceptor.Write(LogLevel.Trace, _inner, messageTemplate, args);
        RecordTrace("TRACE", messageTemplate, args);
    }

    /// <inheritdoc />
    public void InfoContext(string message, object? request = null, object? result = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        _interceptor.Write(LogLevel.Information, _inner,
            "{Message} | Caller: {CallerMemberName} at {CallerFilePath}:{CallerLineNumber} | Request: {@Request} | Result: {@Result}",
            message, memberName, filePath, lineNumber, _resolver.Resolve(request), _resolver.Resolve(result));
        RecordTrace("INFO", "{Message} | Caller: {CallerMemberName}", new object?[] { message, memberName });
    }

    /// <inheritdoc />
    public void ErrorContext(Exception ex, string message, object? request = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var classification = _exceptionClassifier?.Classify(ex) ?? ExceptionClassification.Unknown(ex);
        
        _interceptor.Write(LogLevel.Error, _inner, ex,
            "{Message} | ErrorCode: {ErrorCode} | Retryable: {Retryable} | Caller: {CallerMemberName} at {CallerFilePath}:{CallerLineNumber} | Request: {@Request}", 
            message, classification.ErrorCode, classification.Retryable, memberName, filePath, lineNumber, _resolver.Resolve(request));
            
            
        RecordTrace("ERROR", "{Message} | ErrorCode: {ErrorCode} | Caller: {CallerMemberName}", new object?[] { message, classification.ErrorCode, memberName }, ex);
    }

    /// <inheritdoc />
    public void Audit(string action, string objectType, string objectId, bool isSuccess = true, string? previousStatus = null, string? newStatus = null, object? extraData = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        using var scope = _inner.BeginScope(new Dictionary<string, object>
        {
            [LogPropertyConventions.EventKind] = "audit",
            [LogPropertyConventions.EventCategory] = "business"
        });

        _interceptor.Write(LogLevel.Information, _inner,
            "Audit: {AuditAction} on {ObjectType} {ObjectId} | Success: {IsSuccess} | Prev: {PreviousStatus} | New: {NewStatus} | Data: {@ExtraData} | Caller: {CallerMemberName}",
            action, objectType, objectId, isSuccess, previousStatus, newStatus, _resolver.Resolve(extraData), memberName);
        RecordTrace("AUDIT", "Audit: {AuditAction} on {ObjectType} {ObjectId} | Success: {IsSuccess}", new object?[] { action, objectType, objectId, isSuccess });
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
