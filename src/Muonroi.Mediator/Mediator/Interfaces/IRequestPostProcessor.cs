namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Defines a post-processor that runs after the handler returns a response.
/// Post-processors run inside <see cref="Muonroi.Mediator.Behaviours.MPostProcessorBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IRequestPostProcessor<in TRequest, in TResponse>
{
    /// <summary>
    /// Executes the Process Async operation.
    /// </summary>
    Task ProcessAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
}
