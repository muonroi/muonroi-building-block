namespace Quickstart.Mediator.Api.Pipeline;

/// <summary>
/// Custom pipeline behavior that measures and logs the wall-clock time every request spends
/// in the handler. Demonstrates how to plug an <see cref="IPipelineBehavior{TRequest,TResponse}"/>
/// into the Muonroi Mediator pipeline via <c>options.AddBehavior(typeof(TimingBehavior&lt;,&gt;))</c>.
/// </summary>
public sealed class TimingBehavior<TRequest, TResponse>(ILogger<TimingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        Stopwatch sw = Stopwatch.StartNew();

        logger.LogDebug("[TIMING] Starting {RequestName}.", requestName);

        try
        {
            TResponse response = await next();
            sw.Stop();
            logger.LogInformation(
                "[TIMING] {RequestName} completed in {ElapsedMs} ms.",
                requestName,
                sw.ElapsedMilliseconds);
            return response;
        }
        catch
        {
            sw.Stop();
            logger.LogWarning(
                "[TIMING] {RequestName} failed after {ElapsedMs} ms.",
                requestName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
