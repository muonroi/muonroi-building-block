namespace Muonroi.Mediator.Mediator.Interfaces;

public interface IStreamRequestHandler<in TRequest, out MResponse> where TRequest : IStreamRequest<MResponse>
{
    IAsyncEnumerable<MResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
