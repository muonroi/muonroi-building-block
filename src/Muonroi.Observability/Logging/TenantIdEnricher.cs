namespace Muonroi.Observability.Logging;

/// <summary>
/// Enriches log events with tenant, user, and correlation identifiers.
/// </summary>
public sealed class TenantIdEnricher(ISystemExecutionContextAccessor accessor) : ILogEventEnricher
{
    private readonly ISystemExecutionContextAccessor _accessor =
        MGuard.NotNull(accessor);

    /// <summary>
    /// Adds context identifiers to the log event.
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        MGuard.NotNull(logEvent);
        MGuard.NotNull(propertyFactory);

        ISystemExecutionContext context = _accessor.Get();
        if (!string.IsNullOrWhiteSpace(context.TenantId))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("TenantId", context.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(context.UserId))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("UserId", context.UserId));
        }

        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("CorrelationId", context.CorrelationId));
        }

        if (!string.IsNullOrWhiteSpace(context.SourceType))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceType", context.SourceType));
        }
    }
}
