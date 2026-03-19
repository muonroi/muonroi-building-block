using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Outermost pipeline behavior that delegates unhandled exceptions to registered
/// <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}"/> instances.
/// If a handler sets <see cref="RequestExceptionHandlerState{TResponse}.Handled"/> to true,
/// the exception is swallowed and the handler's response is returned.
/// If no handler handles the exception, it is re-thrown.
/// </summary>
public sealed class MExceptionHandlerBehavior<TRequest, TResponse>(
    IEnumerable<IRequestExceptionHandler<TRequest, TResponse, Exception>> handlers,
    IMLog<MExceptionHandlerBehavior<TRequest, TResponse>>? logger = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            RequestExceptionHandlerState<TResponse> state = new();

            foreach (IRequestExceptionHandler<TRequest, TResponse, Exception> handler in handlers)
            {
                await handler.HandleAsync(request, ex, state, cancellationToken);

                if (state.Handled)
                {
                    logger?.Info("[ExceptionHandler] Exception of type '{ExType}' handled by '{Handler}' for request '{Request}'.",
                        ex.GetType().Name,
                        handler.GetType().Name,
                        typeof(TRequest).Name);
                    return state.Response!;
                }
            }

            // No handler handled the exception — rethrow preserving stack trace
            throw;
        }
    }
}
