namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Consume filter that enforces tenant policy for message handlers.
/// </summary>
/// <typeparam name="T">Message type</typeparam>
public class TenantContextConsumeFilter<T>(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogScopeFactory? logScopeFactory = null) : IFilter<ConsumeContext<T>> where T : class
{
    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        string? tenantId = null;
        global::MassTransit.Headers headers = context.Headers;
        if (headers != null && headers.TryGetHeader(CustomHeader.TenantId, out object? header) && header != null)
        {
            tenantId = header.ToString()?.Trim();
        }

        ISystemExecutionContext current = executionContextAccessor.Get();
        ISystemExecutionContext resolved = tenantContextPolicy.ResolveAndValidate(
            new SystemExecutionContext(
                tenantId: tenantId ?? current.TenantId,
                userId: current.UserId,
                username: current.Username,
                correlationId: current.CorrelationId,
                accessToken: current.AccessToken,
                apiKey: current.ApiKey,
                isAuthenticated: current.IsAuthenticated,
                permissions: current.Permissions,
                sourceType: "message-consume"));

        using SystemExecutionContextScope scope = new(executionContextAccessor, resolved);
        using ContextMirrorScope mirror = ContextMirrorScope.Apply(resolved, logScopeFactory);
        await next.Send(context);
    }

    /// <summary>
    /// Executes the Probe operation.
    /// </summary>
    public void Probe(ProbeContext context)
    {
    }
}
