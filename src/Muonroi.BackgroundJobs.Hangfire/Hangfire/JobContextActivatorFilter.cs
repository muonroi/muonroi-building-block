using Hangfire.Server;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

namespace Muonroi.BackgroundJobs.Hangfire.Hangfire;

/// <summary>
/// Hangfire filter that restores Muonroi execution context for job runs.
/// </summary>
/// <param name="accessor">Optional execution context accessor.</param>
/// <param name="policy">Optional tenant context policy.</param>
/// <param name="logScopeFactory">Optional log scope factory.</param>
public sealed class JobContextActivatorFilter(
    ISystemExecutionContextAccessor? accessor = null,
    ITenantContextPolicy? policy = null,
    ILogScopeFactory? logScopeFactory = null) : IServerFilter
{
    private const string ScopeKey = "__muonroi_context_scope";
    private readonly ISystemExecutionContextAccessor _accessor = accessor ?? new SystemExecutionContextAccessor();
    private readonly ITenantContextPolicy _policy = policy ?? new DefaultTenantContextPolicy(new NullContextResolver());
    private readonly ILogScopeFactory _logScopeFactory = logScopeFactory ?? NullLogScopeFactory.Instance;

    /// <inheritdoc />
    public void OnPerforming(PerformingContext filterContext)
    {
        IMuonroiJobExecutionContext? jobContext =
            filterContext.BackgroundJob?.Job?.Args?.OfType<IMuonroiJobExecutionContext>().FirstOrDefault();

        SystemExecutionContext raw = jobContext is not null
            ? new SystemExecutionContext(
                tenantId: jobContext.TenantId,
                userId: jobContext.UserId,
                username: jobContext.Username,
                correlationId: jobContext.CorrelationId,
                accessToken: jobContext.AccessToken,
                apiKey: jobContext.ApiKey,
                isAuthenticated: jobContext.IsAuthenticated,
                permissions: jobContext.Permissions,
                sourceType: "hangfire")
            : new SystemExecutionContext(
                tenantId: null,
                userId: null,
                username: null,
                correlationId: Guid.NewGuid().ToString("N"),
                accessToken: null,
                apiKey: null,
                isAuthenticated: false,
                permissions: [],
                sourceType: "hangfire");

        ISystemExecutionContext resolved = _policy.ResolveAndValidate(raw);
        SystemExecutionContextScope executionScope = new(_accessor, resolved);
        ContextMirrorScope mirrorScope = ContextMirrorScope.Apply(resolved, _logScopeFactory);
        filterContext.Items[ScopeKey] = new CombinedScope(executionScope, mirrorScope);
    }

    /// <inheritdoc />
    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(ScopeKey, out object? scope) && scope is IDisposable disposableScope)
        {
            disposableScope.Dispose();
        }
    }

    private sealed class CombinedScope(IDisposable left, IDisposable right) : IDisposable
    {
        private readonly IDisposable _left = left;
        private readonly IDisposable _right = right;

        public void Dispose()
        {
            _right.Dispose();
            _left.Dispose();
        }
    }
}
