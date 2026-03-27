namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Represents the IMediator.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Executes the Send{MResponse} operation.
    /// </summary>
    Task<MResponse> Send<MResponse>(IRequest<MResponse> request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Send{TRequest} operation.
    /// </summary>
    Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest;
    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    Task<object?> Send(object request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Publish{TNotification} operation.
    /// </summary>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
    /// <summary>
    /// Executes the Publish operation.
    /// </summary>
    Task Publish(object notification, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Create Stream{MResponse} operation.
    /// </summary>
    IAsyncEnumerable<MResponse> CreateStream<MResponse>(IStreamRequest<MResponse> request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Create Stream operation.
    /// </summary>
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
