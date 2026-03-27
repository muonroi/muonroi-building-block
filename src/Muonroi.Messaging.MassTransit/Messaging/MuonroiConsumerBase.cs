using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Governance.License;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Standard base class for all Muonroi messaging consumers.
/// Handles context injection, license validation, and standardized error logging.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public abstract class MuonroiConsumerBase<TMessage>(
    ISystemExecutionContextAccessor contextAccessor,
    IMLog<TMessage> log,
    ILicenseGuard? licenseGuard = null)
    : IConsumer<TMessage>
    where TMessage : class
{
    /// <summary>
    /// The Log.
    /// </summary>
    protected readonly IMLog<TMessage> Log = log;
    /// <summary>
    /// The Context Accessor.
    /// </summary>
    protected readonly ISystemExecutionContextAccessor ContextAccessor = contextAccessor;
    /// <summary>
    /// The License Guard.
    /// </summary>
    protected readonly ILicenseGuard? LicenseGuard = licenseGuard;

    /// <summary>
    /// Executes the Consume operation.
    /// </summary>
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        LicenseGuard?.EnsureFeature(FreeTierFeatures.Premium.MessageBus);

        ISystemExecutionContext executionContext = ContextAccessor.Get();

        try
        {
            await HandleAsync(context, executionContext, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellations cleanly without treating them as hard faults
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "Error processing message of type {MessageType}. Tenant: {TenantId}, MessageId: {MessageId}",
                typeof(TMessage).Name,
                executionContext.TenantId,
                context.MessageId);
            throw;
        }
    }

    /// <summary>
    /// Override this method to implement the business logic for the consumer.
    /// </summary>
    /// <param name="context">The MassTransit consume context.</param>
    /// <param name="executionContext">The resolved Muonroi system execution context (tenant, user, etc.).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract Task HandleAsync(
        ConsumeContext<TMessage> context, 
        ISystemExecutionContext executionContext, 
        CancellationToken cancellationToken);
}
