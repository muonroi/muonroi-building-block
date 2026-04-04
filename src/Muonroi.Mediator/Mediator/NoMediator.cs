using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Mediator;

/// <summary>
/// Represents the No Mediator.
/// </summary>
public class NoMediator : IMediator
{
    /// <summary>
    /// Executes the Create Stream{TResponse} operation.
    /// </summary>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        return EmptyAsync<TResponse>();
    }

    /// <summary>
    /// Executes the Create Stream operation.
    /// </summary>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        return EmptyAsync<object?>();
    }

    /// <summary>
    /// Executes the Publish operation.
    /// </summary>
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the Publish{TNotification} operation.
    /// </summary>
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : Interfaces.INotification
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the Send{TResponse} operation.
    /// </summary>
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<TResponse>(default!);
    }

    /// <summary>
    /// Executes the Send{TRequest} operation.
    /// </summary>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object?>(null);
    }

    private static async IAsyncEnumerable<T> EmptyAsync<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
