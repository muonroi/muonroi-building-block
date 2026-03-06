namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Enriches Serilog context with Elastic Common Schema fields for sent messages.
/// </summary>
/// <typeparam name="T">Message type.</typeparam>
public class EcsSendLoggingFilter<T>(
    ILicenseGuard? licenseGuard = null,
    LicenseState? licenseState = null,
    IMLogContext? logContext = null)
    : IFilter<SendContext<T>>
    where T : class
{
    private readonly LicenseState _licenseState = licenseState ?? licenseGuard?.Current ?? LicenseState.CreateFree();

    public EcsSendLoggingFilter(LicenseState? licenseState)
        : this(null, licenseState)
    {
    }

    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        EnsureMessageBusLicensed();

        string messageType = typeof(T).FullName ?? typeof(T).Name;
        Uri? destinationAddress = context.DestinationAddress ?? context.SourceAddress;
        string destination = destinationAddress?.ToString() ?? string.Empty;
        string? tenantId = ResolveTenantId(context);
        string transport = MessageBusRuntimeTelemetry.ResolveTransport(destinationAddress);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string status = "ok";

        using Activity? activity = MessageBusRuntimeTelemetry.ActivitySource.StartActivity(
            "messagebus.send",
            ActivityKind.Producer);
        activity?.SetTag("messaging.operation", "send");
        activity?.SetTag("messaging.message_type", messageType);
        activity?.SetTag("messaging.destination", destination);
        activity?.SetTag("messaging.system", transport);
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);

        using IMLogContextScope? messageId = logContext?.PushProperty("message.id", context.MessageId);
        using IMLogContextScope? correlationId = logContext?.PushProperty("correlation.id", context.CorrelationId);
        using IMLogContextScope? conversationId = logContext?.PushProperty("conversation.id", context.ConversationId);
        using IMLogContextScope? eventAction = logContext?.PushProperty("event.action", "send");

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
                "send",
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

    private static string? ResolveTenantId(SendContext<T> context)
    {
        SendHeaders? headers = context.Headers;
        if (headers != null &&
            headers.TryGetHeader(CustomHeader.TenantId, out object? tenantHeader) &&
            tenantHeader != null)
        {
            string? tenantFromHeader = tenantHeader.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(tenantFromHeader))
            {
                return tenantFromHeader;
            }
        }

        string? tenantFromRuntime = TenantContext.CurrentTenantId?.Trim();
        if (!string.IsNullOrWhiteSpace(tenantFromRuntime))
        {
            headers?.Set(CustomHeader.TenantId, tenantFromRuntime);
            return tenantFromRuntime;
        }

        return null;
    }
}
