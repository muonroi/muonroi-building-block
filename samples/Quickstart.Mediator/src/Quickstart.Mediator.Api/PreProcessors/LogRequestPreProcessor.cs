using Muonroi.Mediator.Mediator.Interfaces;
using System.Text.Json;

namespace Quickstart.Mediator.Api.PreProcessors;

/// <summary>
/// Pre-processor that logs every incoming request before it enters the handler.
/// Registered automatically by <c>AddMuonroiEcosystem()</c> via
/// <c>MPreProcessorBehavior&lt;TRequest, TResponse&gt;</c>, which resolves all
/// <see cref="IRequestPreProcessor{TRequest}"/> implementations from DI.
/// </summary>
public sealed class LogRequestPreProcessor<TRequest>(ILogger<LogRequestPreProcessor<TRequest>> logger)
    : IRequestPreProcessor<TRequest>
{
    public Task ProcessAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        string requestName = typeof(TRequest).Name;
        string payload = JsonSerializer.Serialize(request);

        logger.LogInformation(
            "[PRE-PROCESSOR] Received {RequestName}: {Payload}",
            requestName,
            payload);

        return Task.CompletedTask;
    }
}
