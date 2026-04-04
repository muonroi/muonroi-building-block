using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Publish Endpoint Auth Extensions.
/// </summary>
public static class PublishEndpointAuthExtensions
{
    /// <summary>
    /// Executes the Publish With Auth Context{T} operation.
    /// </summary>
    public static Task PublishWithAuthContext<T>(
        this IPublishEndpoint endpoint,
        T message,
        ISystemExecutionContextAccessor contextAccessor,
        ITenantContextPolicy tenantContextPolicy,
        CancellationToken cancellationToken = default)
        where T : class
    {
        MGuard.NotNull(contextAccessor);
        ISystemExecutionContext context = contextAccessor.Get();
        return endpoint.PublishWithContext(message, context, tenantContextPolicy, cancellationToken);
    }

    /// <summary>
    /// Executes the Publish With Context{T} operation.
    /// </summary>
    public static Task PublishWithContext<T>(
        this IPublishEndpoint endpoint,
        T message,
        ISystemExecutionContext context,
        ITenantContextPolicy tenantContextPolicy,
        CancellationToken cancellationToken = default)
        where T : class
    {
        MGuard.NotNull(endpoint);
        MGuard.NotNull(context);
        MGuard.NotNull(tenantContextPolicy);

        ISystemExecutionContext resolved = tenantContextPolicy.ResolveAndValidate(context);
        MuonroiMessageEnvelope envelope = new()
        {
            TenantId = resolved.TenantId,
            UserId = resolved.UserId,
            Username = resolved.Username,
            CorrelationId = resolved.CorrelationId,
            AccessToken = resolved.AccessToken,
            SentAt = DateTimeOffset.UtcNow
        };

        return endpoint.Publish(message, context =>
        {
            ApplyEnvelopeHeaders(context, envelope, resolved);
        }, cancellationToken);
    }

    private static void ApplyEnvelopeHeaders<T>(
        PublishContext<T> context,
        MuonroiMessageEnvelope envelope,
        ISystemExecutionContext executionContext)
        where T : class
    {
        context.Headers.Set(CustomHeader.CorrelationId, envelope.CorrelationId);
        context.Headers.Set(CustomHeader.SentAt, envelope.SentAt.ToString("O"));
        context.Headers.Set(CustomHeader.SourceType, executionContext.SourceType);

        if (!string.IsNullOrWhiteSpace(envelope.UserId))
            context.Headers.Set(ClaimConstants.UserIdentifier, envelope.UserId);
        if (!string.IsNullOrWhiteSpace(envelope.Username))
            context.Headers.Set(ClaimConstants.Username, envelope.Username);
        if (!string.IsNullOrWhiteSpace(envelope.AccessToken))
            context.Headers.Set(ClaimConstants.AccessToken, envelope.AccessToken);
        if (!string.IsNullOrWhiteSpace(envelope.TenantId))
            context.Headers.Set(CustomHeader.TenantId, envelope.TenantId);
    }
}
