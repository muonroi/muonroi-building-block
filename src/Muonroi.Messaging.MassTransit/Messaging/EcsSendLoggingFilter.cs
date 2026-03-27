namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Enriches Serilog context with Elastic Common Schema fields for sent messages.
/// </summary>
/// <typeparam name="T">Message type.</typeparam>
public class EcsSendLoggingFilter<T>(
    ILicenseGuard? licenseGuard = null,
    LicenseState? licenseState = null,
    IMLogContext? logContext = null,
    ISystemExecutionContextAccessor? contextAccessor = null)
    : IFilter<SendContext<T>>
    where T : class
{
    private readonly LicenseState _licenseState = licenseState ?? licenseGuard?.Current ?? LicenseState.CreateFree();

    /// <summary>
    /// Executes the Ecs Send Logging Filter operation.
    /// </summary>
    public EcsSendLoggingFilter(LicenseState? licenseState)
        : this(null, licenseState, null, null)
    {
    }

    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        EnsureMessageBusLicensed();

        string messageType = typeof(T).FullName ?? typeof(T).Name;
        Uri? destinationAddress = context.DestinationAddress ?? context.SourceAddress;
        string destination = destinationAddress?.ToString() ?? string.Empty;
        string? tenantId = ResolveTenantId(context, contextAccessor);
        string transport = MessageBusRuntimeTelemetry.ResolveTransport(destinationAddress);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string status = "ok";

        using Activity? activity = MessageBusRuntimeTelemetry.ActivitySource.StartActivity(
            "messagebus.send",
            ActivityKind.Producer);

        string? correlationIdValue = contextAccessor?.Get().CorrelationId ?? context.CorrelationId?.ToString();

        activity?.SetTag("messaging.operation", "send");
        activity?.SetTag("messaging.message_type", messageType);
        activity?.SetTag("messaging.destination", destination);
        activity?.SetTag("messaging.system", transport);
        activity?.SetTag("tenant.id", tenantId ?? string.Empty);
        activity?.SetTag("correlation.id", correlationIdValue ?? string.Empty);

        using IMLogContextScope? messageIdLog = logContext?.PushProperty("message.id", context.MessageId);
        using IMLogContextScope? correlationIdLog = logContext?.PushProperty("correlation.id", context.CorrelationId);
        using IMLogContextScope? conversationIdLog = logContext?.PushProperty("conversation.id", context.ConversationId);
        using IMLogContextScope? eventActionLog = logContext?.PushProperty("event.action", "send");

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

    /// <summary>
    /// Executes the Probe operation.
    /// </summary>
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

    private static string? ResolveTenantId(SendContext<T> context, ISystemExecutionContextAccessor? accessor)
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

        string? tenantFromRuntime = accessor?.Get().TenantId?.Trim();
        if (!string.IsNullOrWhiteSpace(tenantFromRuntime))
        {
            headers?.Set(CustomHeader.TenantId, tenantFromRuntime);
            return tenantFromRuntime;
        }

        return null;
    }
}
