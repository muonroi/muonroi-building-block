using Muonroi.Core.Extensions;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Behaviours;

public class LoggingBehavior<TRequest, TResponse>(IMLog<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).GetGenericTypeName();
        Stopwatch sw = Stopwatch.StartNew();

        logger.LogDebug("[MEDIATOR] Handling {RequestName}", requestName);

        try
        {
            TResponse? response = await next();
            sw.Stop();
            logger.LogDebug("[MEDIATOR] Handled {RequestName} in {Elapsed}ms", requestName,
                sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "[MEDIATOR] Failed {RequestName} in {Elapsed}ms", requestName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
