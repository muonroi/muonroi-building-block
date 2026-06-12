namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Defines a pre-processor that runs before the handler for requests of type <typeparamref name="TRequest"/>.
/// Pre-processors run inside <see cref="Behaviours.MPreProcessorBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IRequestPreProcessor<in TRequest>
{
    /// <summary>
    /// Executes the pre-processing step.
    /// </summary>
    Task ProcessAsync(TRequest request, CancellationToken cancellationToken = default);
}
