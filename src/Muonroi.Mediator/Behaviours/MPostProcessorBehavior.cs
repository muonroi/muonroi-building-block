using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Executes all registered <see cref="IRequestPostProcessor{TRequest,TResponse}"/> instances
/// after the handler returns a response. Post-processors run in DI registration order.
/// No-op if no post-processors are registered for this (TRequest, TResponse) pair.
/// </summary>
public sealed class MPostProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IRequestPostProcessor<TRequest, TResponse>> processors)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next();

        foreach (IRequestPostProcessor<TRequest, TResponse> processor in processors)
            await processor.ProcessAsync(request, response, cancellationToken);

        return response;
    }
}
