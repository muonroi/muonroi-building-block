namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Represents the IStream Request Handler{TRequest, MResponse}.
/// </summary>
public interface IStreamRequestHandler<in TRequest, out MResponse> where TRequest : IStreamRequest<MResponse>
{
    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    IAsyncEnumerable<MResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
