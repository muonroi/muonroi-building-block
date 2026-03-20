using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.Abstractions.Interfaces;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Tenant Quota Messaging Filter{T}.
/// </summary>
public class TenantQuotaMessagingFilter<T>(
    ITenantQuotaTracker quotaTracker,
    ISystemExecutionContextAccessor contextAccessor,
    IOptions<MessageBusConfigs> configs) : IFilter<PublishContext<T>>, IFilter<SendContext<T>>
    where T : class
{
    private readonly MessageBusConfigs _configs = configs.Value;

    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (_configs.EnableQuotaEnforcement)
        {
            await CheckQuotaAsync(context.Headers);
        }
        await next.Send(context);
    }

    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        if (_configs.EnableQuotaEnforcement)
        {
            await CheckQuotaAsync(context.Headers);
        }
        await next.Send(context);
    }

    private async Task CheckQuotaAsync(global::MassTransit.Headers headers)
    {
        string? tenantId = null;
        if (headers.TryGetHeader(CustomHeader.TenantId, out object? tenantHeader))
        {
            tenantId = tenantHeader?.ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = contextAccessor.Get().TenantId;
        }

        if (string.IsNullOrWhiteSpace(tenantId)) return;

        // Check messages per minute
        bool allowedMinute = await quotaTracker.CheckQuotaAsync(tenantId, QuotaType.MessagesPerMinute, 1);
        if (!allowedMinute)
        {
            throw new QuotaExceededException($"[QUOTA] Message rate limit exceeded for tenant {tenantId} (MessagesPerMinute).");
        }

        // Check messages per day
        bool allowedDay = await quotaTracker.CheckQuotaAsync(tenantId, QuotaType.MessagesPerDay, 1);
        if (!allowedDay)
        {
            throw new QuotaExceededException($"[QUOTA] Daily message limit exceeded for tenant {tenantId} (MessagesPerDay).");
        }

        // Increment usage
        await quotaTracker.IncrementUsageAsync(tenantId, QuotaType.MessagesPerMinute, 1);
        await quotaTracker.IncrementUsageAsync(tenantId, QuotaType.MessagesPerDay, 1);
    }

    /// <summary>
    /// Executes the Probe operation.
    /// </summary>
    public void Probe(ProbeContext context)
    {
    }
}
