namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Muonroi Context Send Filter T.
/// </summary>
public class MuonroiContextSendFilter<T>(
    ISystemExecutionContextAccessor contextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    IOptions<MessageBusConfigs> configs) : IFilter<SendContext<T>>
    where T : class
{
    private readonly MessageBusConfigs _configs = configs.Value;

    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        ISystemExecutionContext current = contextAccessor.Get();
        if (current != null)
        {
            ISystemExecutionContext resolved = tenantContextPolicy.ResolveAndValidate(current);

            context.Headers.Set(CustomHeader.CorrelationId, resolved.CorrelationId);
            context.Headers.Set(CustomHeader.SentAt, DateTimeOffset.UtcNow.ToString("O"));
            context.Headers.Set(CustomHeader.SourceType, resolved.SourceType);

            if (!string.IsNullOrWhiteSpace(resolved.UserId))
            {
                context.Headers.Set(ClaimConstants.UserIdentifier, resolved.UserId);
            }

            if (!string.IsNullOrWhiteSpace(resolved.Username))
            {
                context.Headers.Set(ClaimConstants.Username, resolved.Username);
            }

            if (!string.IsNullOrWhiteSpace(resolved.TenantId))
            {
                context.Headers.Set(CustomHeader.TenantId, resolved.TenantId);
            }

            if (!string.IsNullOrWhiteSpace(resolved.AccessToken))
            {
                if (_configs.MaskAccessTokenInHeaders)
                {
                    string sig = GenerateIdentitySignature(resolved);
                    context.Headers.Set("X-Muonroi-Identity-Sig", sig);
                }
                else
                {
                    context.Headers.Set(ClaimConstants.AccessToken, resolved.AccessToken);
                }
            }
        }

        await next.Send(context);
    }

    private static string GenerateIdentitySignature(ISystemExecutionContext resolved)
    {
        string payload = $"{resolved.UserId}:{resolved.TenantId}:{resolved.CorrelationId}";
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Executes the Probe operation.
    /// </summary>
    public void Probe(ProbeContext context)
    {
    }
}
