namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Represents the Request Handler Delegate{TResponse}.
/// </summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Represents the IPipeline Behavior{TRequest, TResponse}.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
