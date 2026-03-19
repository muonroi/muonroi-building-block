using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Behaviours;

/// <summary>
/// Executes all registered <see cref="IRequestPreProcessor{TRequest}"/> instances
/// before the handler is called. Pre-processors run in DI registration order.
/// No-op if no pre-processors are registered for <typeparamref name="TRequest"/>.
/// </summary>
public sealed class MPreProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IRequestPreProcessor<TRequest>> processors)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (IRequestPreProcessor<TRequest> processor in processors)
            await processor.ProcessAsync(request, cancellationToken);

        return await next();
    }
}
