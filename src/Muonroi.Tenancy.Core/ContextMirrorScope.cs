


using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Tenancy.Core;

/// <summary>
/// Mirrors an execution context into ambient tenant and user contexts for the scope lifetime.
/// </summary>
public sealed class ContextMirrorScope : IDisposable
{
    private readonly string? _previousTenantId = TenantContext.CurrentTenantId;
    private readonly string? _previousUserId = UserContext.CurrentUserGuid;
    private readonly string? _previousUsername = UserContext.CurrentUsername;
    private readonly IDisposable? _logScope;
    private bool _disposed;

    private ContextMirrorScope(ISystemExecutionContext context, ILogScopeFactory? logScopeFactory)
    {
        TenantContext.CurrentTenantId = context.TenantId;
        UserContext.CurrentUserGuid = context.UserId;
        UserContext.CurrentUsername = context.Username;

        _logScope = logScopeFactory?.BeginScope(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TenantId"] = context.TenantId,
            ["UserId"] = context.UserId,
            ["CorrelationId"] = context.CorrelationId,
            ["SourceType"] = context.SourceType
        });
    }

    /// <summary>
    /// Applies the supplied execution context and returns a scope that restores previous values on dispose.
    /// </summary>
    /// <param name="context">The execution context to apply.</param>
    /// <param name="logScopeFactory">Optional log scope factory.</param>
    /// <returns>The created scope.</returns>
    public static ContextMirrorScope Apply(ISystemExecutionContext context, ILogScopeFactory? logScopeFactory = null)
    {
        MGuard.NotNull(context);
        return new ContextMirrorScope(context, logScopeFactory);
    }

    /// <summary>
    /// Restores previous ambient context values and disposes the log scope.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logScope?.Dispose();
        TenantContext.CurrentTenantId = _previousTenantId;
        UserContext.CurrentUserGuid = _previousUserId;
        UserContext.CurrentUsername = _previousUsername;
        _disposed = true;
    }
}
