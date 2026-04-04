using Muonroi.Core.Abstractions.Context;
using Muonroi.Tenancy.Core;

namespace Muonroi.BackgroundJobs.Quartz.Quartz;

/// <summary>
/// Quartz job listener that restores Muonroi execution context for job runs.
/// </summary>
/// <param name="accessor">Optional execution context accessor.</param>
/// <param name="policy">Optional tenant context policy.</param>
/// <param name="logScopeFactory">Optional log scope factory.</param>
public sealed class QuartzContextJobListener(
    ISystemExecutionContextAccessor? accessor = null,
    ITenantContextPolicy? policy = null,
    ILogScopeFactory? logScopeFactory = null) : IJobListener
{
    private const string ContextKey = "muonroi_execution_context";
    private const string ScopeKey = "muonroi_execution_context_scope";

    private readonly ISystemExecutionContextAccessor _accessor = accessor ?? new SystemExecutionContextAccessor();
    private readonly ITenantContextPolicy _policy = policy ?? new DefaultTenantContextPolicy(new NullContextResolver());
    private readonly ILogScopeFactory _logScopeFactory = logScopeFactory ?? NullLogScopeFactory.Instance;

    /// <inheritdoc />
    public string Name => nameof(QuartzContextJobListener);

    /// <inheritdoc />
    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        IMuonroiJobExecutionContext? executionContext = context.MergedJobDataMap[ContextKey] as IMuonroiJobExecutionContext;
        SystemExecutionContext raw = executionContext is not null
            ? new SystemExecutionContext(
                tenantId: executionContext.TenantId,
                userId: executionContext.UserId,
                username: executionContext.Username,
                correlationId: executionContext.CorrelationId,
                accessToken: executionContext.AccessToken,
                apiKey: executionContext.ApiKey,
                isAuthenticated: executionContext.IsAuthenticated,
                permissions: executionContext.Permissions,
                sourceType: "quartz")
            : new SystemExecutionContext(
                tenantId: null,
                userId: null,
                username: null,
                correlationId: Guid.NewGuid().ToString("N"),
                accessToken: null,
                apiKey: null,
                isAuthenticated: false,
                permissions: [],
                sourceType: "quartz");

        ISystemExecutionContext resolved = _policy.ResolveAndValidate(raw);
        SystemExecutionContextScope executionScope = new(_accessor, resolved);
        ContextMirrorScope mirrorScope = ContextMirrorScope.Apply(resolved, _logScopeFactory);
        context.MergedJobDataMap[ScopeKey] = new CombinedScope(executionScope, mirrorScope);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Cleanup(context);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        Cleanup(context);
        return Task.CompletedTask;
    }

    private static void Cleanup(IJobExecutionContext context)
    {
        if (context.MergedJobDataMap[ScopeKey] is IDisposable scope)
        {
            scope.Dispose();
            context.MergedJobDataMap.Remove(ScopeKey);
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
