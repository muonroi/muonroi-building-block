namespace Quickstart.Mediator.Api.PostProcessors;

/// <summary>
/// Post-processor that logs the response after the handler has returned.
/// Registered automatically by <c>AddMuonroiEcosystem()</c> via
/// <c>MPostProcessorBehavior&lt;TRequest, TResponse&gt;</c>, which resolves all
/// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> implementations from DI.
/// </summary>
public sealed class LogResponsePostProcessor<TRequest, TResponse>(ILogger<LogResponsePostProcessor<TRequest, TResponse>> logger)
    : IRequestPostProcessor<TRequest, TResponse>
{
    public Task ProcessAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default)
    {
        string requestName = typeof(TRequest).Name;
        string payload = JsonSerializer.Serialize(response);

        logger.LogInformation(
            "[POST-PROCESSOR] {RequestName} responded: {Payload}",
            requestName,
            payload);

        return Task.CompletedTask;
    }
}
