using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Tenancy.Core;

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

    public static ContextMirrorScope Apply(ISystemExecutionContext context, ILogScopeFactory? logScopeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ContextMirrorScope(context, logScopeFactory);
    }

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
