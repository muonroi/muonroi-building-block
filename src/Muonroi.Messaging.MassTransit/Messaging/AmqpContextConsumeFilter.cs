namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Amqp Context Consume Filter{T}.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "MassTransit ConsumeContext values are guaranteed non-null by the MassTransit pipeline contract.")]
public class AmqpContextConsumeFilter<T>(
    IAmqpContext amqpContext,
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogScopeFactory? logScopeFactory = null) : IFilter<ConsumeContext<T>> where T : class
{
    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        Dictionary<string, object> headers = [];
        global::MassTransit.Headers sourceHeaders = context.Headers;

        if (sourceHeaders == null)
        {
            await next.Send(context);
            return;
        }

        if (sourceHeaders.TryGetHeader(CustomHeader.CorrelationId, out object? correlation) && correlation != null)
            headers[CustomHeader.CorrelationId] = correlation;
        if (sourceHeaders.TryGetHeader(ClaimConstants.UserIdentifier, out object? userId) && userId != null)
            headers[ClaimConstants.UserIdentifier] = userId;
        if (sourceHeaders.TryGetHeader(ClaimConstants.Username, out object? username) && username != null)
            headers[ClaimConstants.Username] = username;
        if (sourceHeaders.TryGetHeader(ClaimConstants.AccessToken, out object? accessToken) && accessToken != null)
            headers[ClaimConstants.AccessToken] = accessToken;
        if (sourceHeaders.TryGetHeader("X-Muonroi-Identity-Sig", out object? identitySig) && identitySig != null)
            headers["X-Muonroi-Identity-Sig"] = identitySig;
        if (sourceHeaders.TryGetHeader(CustomHeader.TenantId, out object? tenantId) && tenantId != null)
            headers[CustomHeader.TenantId] = tenantId;
        if (sourceHeaders.TryGetHeader(CustomHeader.SentAt, out object? sentAt) && sentAt != null)
            headers[CustomHeader.SentAt] = sentAt;
        if (sourceHeaders.TryGetHeader(CustomHeader.SourceType, out object? sourceType) && sourceType != null)
            headers[CustomHeader.SourceType] = sourceType;

        amqpContext.ClearHeaders();
        amqpContext.AddHeaders(headers);
        string correlationId = amqpContext.GetHeaderByKey(CustomHeader.CorrelationId) ?? Guid.NewGuid().ToString("N");
        string? headerSourceType = amqpContext.GetHeaderByKey(CustomHeader.SourceType);
        
        bool hasAccessToken = !string.IsNullOrWhiteSpace(amqpContext.GetHeaderByKey(ClaimConstants.AccessToken));
        bool hasIdentitySig = !string.IsNullOrWhiteSpace(amqpContext.GetHeaderByKey("X-Muonroi-Identity-Sig"));
        
        SystemExecutionContext rawContext = new(
            tenantId: amqpContext.GetHeaderByKey(CustomHeader.TenantId),
            userId: amqpContext.GetHeaderByKey(ClaimConstants.UserIdentifier),
            username: amqpContext.GetHeaderByKey(ClaimConstants.Username),
            correlationId: correlationId,
            accessToken: amqpContext.GetHeaderByKey(ClaimConstants.AccessToken),
            apiKey: null,
            isAuthenticated: hasAccessToken || hasIdentitySig,
            permissions: [],
            sourceType: string.IsNullOrWhiteSpace(headerSourceType) ? "message-bus" : headerSourceType!);
        ISystemExecutionContext resolvedContext = tenantContextPolicy.ResolveAndValidate(rawContext);

        try
        {
            using SystemExecutionContextScope scope = new(executionContextAccessor, resolvedContext);
            using ContextMirrorScope mirror = ContextMirrorScope.Apply(resolvedContext, logScopeFactory);
            await next.Send(context);
        }
        finally
        {
            amqpContext.ClearHeaders();
        }
    }

    /// <summary>
    /// Executes the Probe operation.
    /// </summary>
    public void Probe(ProbeContext context)
    {
    }
}
