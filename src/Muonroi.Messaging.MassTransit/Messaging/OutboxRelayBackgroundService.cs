namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Outbox Relay Background Service.
/// </summary>
public class OutboxRelayBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<MessageBusConfigs> options,
    IMLog<OutboxRelayBackgroundService> log,
    IMDateTimeService? dateTimeService = null)
    : BackgroundService, IOutboxRelayService
{
    private readonly OutboxRelayConfigs _configs = options.Value.OutboxRelay;

    /// <summary>
    /// Executes the Execute Async operation.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configs.Enabled)
        {
            log.Info("Outbox relay is disabled in configuration.");
            return;
        }

        log.Info("Outbox relay background service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error occurred during outbox relay execution.");
            }

            await Task.Delay(_configs.PollingIntervalMs, stoppingToken);
        }
    }

    /// <summary>
    /// Executes the Relay Pending Async operation.
    /// </summary>
    public async Task RelayPendingAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        IEventOutboxStore? store = scope.ServiceProvider.GetService<IEventOutboxStore>();
        
        if (store == null)
        {
            log.Warn("IEventOutboxStore is not registered. Cannot relay outbox messages.");
            return;
        }

        IMJsonSerializeService jsonService = scope.ServiceProvider.GetRequiredService<IMJsonSerializeService>();
        IPublishEndpoint publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        List<EventOutbox> pendingEvents = [.. store.EventOutboxes
            .Where(x => x.Status == EventOutboxStatus.Pending)
            .OrderBy(x => x.CreationTime)
            .Take(_configs.BatchSize)];

        if (pendingEvents.Count == 0)
        {
            return;
        }

        MethodInfo? deserializeMethod = typeof(IMJsonSerializeService).GetMethod(nameof(IMJsonSerializeService.Deserialize));

        foreach (EventOutbox outbox in pendingEvents)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outbox.EventType) || string.IsNullOrWhiteSpace(outbox.EventContent))
                {
                    outbox.Status = EventOutboxStatus.Failed;
                    outbox.ErrorMessage = "EventType or EventContent is missing.";
                    continue;
                }

                Type? messageType = Type.GetType(outbox.EventType);
                if (messageType == null)
                {
                    // Attempt to find in all assemblies if not fully qualified
                    messageType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
                        .FirstOrDefault(t => t.FullName == outbox.EventType || t.Name == outbox.EventType);

                    if (messageType == null)
                    {
                        outbox.Status = EventOutboxStatus.Failed;
                        outbox.ErrorMessage = $"Cannot resolve type: {outbox.EventType}";
                        continue;
                    }
                }

                object? messageInstance = null;
                if (deserializeMethod != null)
                {
                    MethodInfo genericDeserialize = deserializeMethod.MakeGenericMethod(messageType);
                    messageInstance = genericDeserialize.Invoke(jsonService, [outbox.EventContent]);
                }

                if (messageInstance == null)
                {
                    outbox.Status = EventOutboxStatus.Failed;
                    outbox.ErrorMessage = "Deserialization resulted in null.";
                    continue;
                }

                MethodInfo? publishMethod = typeof(IPublishEndpoint)
                    .GetMethods()
                    .FirstOrDefault(m => m.Name == nameof(IPublishEndpoint.Publish) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(CancellationToken));

                if (publishMethod != null)
                {
                    MethodInfo genericPublish = publishMethod.MakeGenericMethod(messageType);
                    Task? publishTask = genericPublish.Invoke(publishEndpoint, [messageInstance, cancellationToken]) as Task;
                    if (publishTask != null)
                    {
                        await publishTask;
                    }
                }
                else
                {
                    // Fallback to object publish
                    await publishEndpoint.Publish(messageInstance, cancellationToken);
                }

                outbox.Status = EventOutboxStatus.Published;
                outbox.LastModificationTime = dateTimeService?.UtcNow() ?? DateTime.UtcNow; // MBB001-exempt: fallback when IMDateTimeService not injected
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to publish outbox event {EventId}", outbox.Id);
                outbox.Status = EventOutboxStatus.Failed;
                outbox.ErrorMessage = ex.Message;
                outbox.LastModificationTime = dateTimeService?.UtcNow() ?? DateTime.UtcNow; // MBB001-exempt: fallback when IMDateTimeService not injected
            }
        }

        await store.SaveChangesAsync(cancellationToken);
    }
}
