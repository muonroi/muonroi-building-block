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
/// A custom logger implementation that provides additional context and helper methods.
/// </summary>
/// <typeparam name="T">The category type for the logger.</typeparam>
public sealed class MLog<T>(
    ILogger<T> inner,
    ISystemExecutionContextAccessor accessor,
    IMLogContext logContext,
    IMTraceContext? traceContext = null,
    IMLogArgumentResolver? resolver = null,
    IExceptionClassifier? exceptionClassifier = null) : IMLog<T>
{
    private readonly ILogger<T> _inner = MGuard.NotNull(inner);
    private readonly ISystemExecutionContextAccessor _accessor = MGuard.NotNull(accessor);
    private readonly IMLogContext _logContext = MGuard.NotNull(logContext);
    private readonly IMLogArgumentResolver _resolver = resolver ?? new MLogArgumentResolver();
    private readonly IExceptionClassifier? _exceptionClassifier = exceptionClassifier;

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

    /// <inheritdoc />
    public void InfoContext(string message, object? request = null, object? result = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        _inner.LogInformation(
            "{Message} | Caller: {CallerMemberName} at {CallerFilePath}:{CallerLineNumber} | Request: {@Request} | Result: {@Result}", 
            message, memberName, filePath, lineNumber, _resolver.Resolve(request), _resolver.Resolve(result));
        RecordTrace("INFO", "{Message} | Caller: {CallerMemberName}", new object?[] { message, memberName });
    }

    /// <inheritdoc />
    public void ErrorContext(Exception ex, string message, object? request = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var classification = _exceptionClassifier?.Classify(ex) ?? ExceptionClassification.Unknown(ex);
        
        _inner.LogError(ex,
            "{Message} | ErrorCode: {ErrorCode} | Retryable: {Retryable} | Caller: {CallerMemberName} at {CallerFilePath}:{CallerLineNumber} | Request: {@Request}", 
            message, classification.ErrorCode, classification.Retryable, memberName, filePath, lineNumber, _resolver.Resolve(request));
            
        RecordTrace("ERROR", "{Message} | ErrorCode: {ErrorCode} | Caller: {CallerMemberName}", new object?[] { message, classification.ErrorCode, memberName }, ex);
    }

    /// <inheritdoc />
    public void Audit(string action, string objectType, string objectId, bool isSuccess = true, string? previousStatus = null, string? newStatus = null, object? extraData = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        _inner.LogInformation(
            "Audit: {AuditAction} on {ObjectType} {ObjectId} | Success: {IsSuccess} | Prev: {PreviousStatus} | New: {NewStatus} | Data: {@ExtraData} | Caller: {CallerMemberName}",
            action, objectType, objectId, isSuccess, previousStatus, newStatus, _resolver.Resolve(extraData), memberName);
        RecordTrace("AUDIT", "Audit: {AuditAction} on {ObjectType} {ObjectId} | Success: {IsSuccess}", new object?[] { action, objectType, objectId, isSuccess });
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
        ISystemExecutionContext context = _accessor.Get();
        if (context == null || context == SystemExecutionContext.Empty)
        {
            return null;
        }

        IMLogContextScope tenantScope = _logContext.PushProperty(LogPropertyConventions.TenantId, context.TenantId);
        IMLogContextScope userScope = _logContext.PushProperty(LogPropertyConventions.UserId, context.UserId);
        IMLogContextScope correlationScope =
            _logContext.PushProperty(LogPropertyConventions.CorrelationId, context.CorrelationId);

        return new CombinedScope(tenantScope, new CombinedScope(userScope, correlationScope));
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
