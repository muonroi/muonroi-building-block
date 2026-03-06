namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Enriches Serilog context with Elastic Common Schema fields for consumed messages.
/// </summary>
/// <typeparam name="T">Message type.</typeparam>
public class EcsConsumeLoggingFilter<T>(
    ILicenseGuard? licenseGuard = null,
    LicenseState? licenseState = null,
    IMLogContext? logContext = null)
    : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly LicenseState _licenseState = licenseState ?? licenseGuard?.Current ?? LicenseState.CreateFree();

    public EcsConsumeLoggingFilter(LicenseState? licenseState)
        : this(null, licenseState)
    {
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        EnsureMessageBusLicensed();

        string messageType = typeof(T).FullName ?? typeof(T).Name;
        Uri? destinationAddress = context.DestinationAddress ?? context.ReceiveContext?.InputAddress;
        string destination = destinationAddress?.ToString() ?? string.Empty;
        string? tenantId = ResolveTenantId(context.Headers);
        string transport = MessageBusRuntimeTelemetry.ResolveTransport(destinationAddress);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string status = "ok";

        using Activity? activity = MessageBusRuntimeTelemetry.ActivitySource.StartActivity(
            "messagebus.consume",
            ActivityKind.Consumer);
        activity?.SetTag("messaging.operation", "consume");
        activity?.SetTag("messaging.message_type", messageType);
        activity?.SetTag("messaging.destination", destination);
        activity?.SetTag("messaging.system", transport);
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);

        using IMLogContextScope? messageId = logContext?.PushProperty("message.id", context.MessageId);
        using IMLogContextScope? correlationId = logContext?.PushProperty("correlation.id", context.CorrelationId);
        using IMLogContextScope? conversationId = logContext?.PushProperty("conversation.id", context.ConversationId);
        using IMLogContextScope? eventAction = logContext?.PushProperty("event.action", "consume");
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            status = "error";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            MessageBusRuntimeTelemetry.TrackOperation(
                "consume",
                messageType,
                destination,
                transport,
                status,
                tenantId,
                stopwatch.Elapsed);
        }
    }

    public void Probe(ProbeContext context)
    {
    }

    private void EnsureMessageBusLicensed()
    {
        if (licenseGuard != null)
        {
            licenseGuard.EnsureFeature(FreeTierFeatures.Premium.MessageBus);
            return;
        }

        if (!_licenseState.HasFeature(FreeTierFeatures.Premium.MessageBus))
        {
            throw new InvalidOperationException(
                "[LICENSE] Feature 'message-bus' is not available under your current license.");
        }
    }

    private static string? ResolveTenantId(global::MassTransit.Headers? headers)
    {
        if (headers != null && headers.TryGetHeader(CustomHeader.TenantId, out object? tenantHeader) && tenantHeader != null)
        {
            string? tenantFromHeader = tenantHeader.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(tenantFromHeader))
            {
                return tenantFromHeader;
            }
        }

        string? tenantFromRuntime = TenantContext.CurrentTenantId?.Trim();
        return string.IsNullOrWhiteSpace(tenantFromRuntime) ? null : tenantFromRuntime;
    }
}
