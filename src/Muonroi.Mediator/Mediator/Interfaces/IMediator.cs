namespace Muonroi.Mediator.Mediator.Interfaces;

public interface IMediator
{
    Task<MResponse> Send<MResponse>(IRequest<MResponse> request, CancellationToken cancellationToken = default);
    Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest;
    Task<object?> Send(object request, CancellationToken cancellationToken = default);
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
    Task Publish(object notification, CancellationToken cancellationToken = default);
    IAsyncEnumerable<MResponse> CreateStream<MResponse>(IStreamRequest<MResponse> request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
