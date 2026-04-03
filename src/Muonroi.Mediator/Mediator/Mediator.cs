using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Mediator.Mediator.Pipeline;
using System.Runtime.CompilerServices;

namespace Muonroi.Mediator.Mediator;

/// <summary>Delegate used by MMediator to resolve services from the DI container.</summary>
public delegate object? ServiceFactory(Type serviceType);

/// <summary>
/// Default <see cref="IMediator"/> implementation.
/// Uses a compiled-delegate wrapper cache to eliminate per-call reflection overhead.
/// After the first call for a given request type, dispatch is purely interface-based — no reflection.
/// </summary>
public class MMediator(ServiceFactory serviceFactory) : IMediator
{
    // ───────────────────────────── Send ─────────────────────────────

    /// <summary>
    /// Executes the Send{MResponse} operation.
    /// </summary>
    public async Task<MResponse> Send<MResponse>(IRequest<MResponse> request,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(request);

        Type requestType = request.GetType();
        RequestHandlerBase wrapper = RequestHandlerWrapperCache.GetOrCreate<MResponse>(requestType);
        object? result = await wrapper.Handle(request, serviceFactory, cancellationToken);
        return (MResponse)result!;
    }

    /// <summary>
    /// Executes the Send{TRequest} operation.
    /// </summary>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        return Send((IRequest<Unit>)request, cancellationToken);
    }

    /// <summary>
    /// Executes the Send operation.
    /// </summary>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(request);
        
        Type requestType = request.GetType();
        Type? responseType = requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments()[0];

        if (responseType == null)
            throw new MInternalException($"Object of type {requestType.Name} does not implement IRequest<T>.");

        RequestHandlerBase wrapper = RequestHandlerWrapperCache.GetOrCreate(requestType, responseType);
        return wrapper.Handle(request, serviceFactory, cancellationToken);
    }

    // ───────────────────────────── Publish ─────────────────────────────

    /// <summary>
    /// Executes the Publish{TNotification} operation.
    /// </summary>
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        MNotificationStrategy strategy = notification is IMStrategyNotification s
            ? s.Strategy
            : MNotificationStrategy.Sequential;
        return PublishInternal(notification, strategy, cancellationToken);
    }

    /// <summary>
    /// Executes the Publish{TNotification} operation.
    /// </summary>
    public Task Publish<TNotification>(TNotification notification, MNotificationStrategy strategy,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishInternal(notification, strategy, cancellationToken);
    }

    /// <summary>
    /// Executes the Publish operation.
    /// </summary>
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(notification);
        if (notification is not INotification n)
            throw new MInternalException("Notification does not implement INotification.");
        
        MNotificationStrategy strategy = n is IMStrategyNotification s ? s.Strategy : MNotificationStrategy.Sequential;
        NotificationHandlerWrapperBase wrapper = RequestHandlerWrapperCache.GetOrCreateNotification(n.GetType());
        return wrapper.Handle(notification, serviceFactory, cancellationToken, strategy);
    }

    private Task PublishInternal<TNotification>(TNotification notification,
        MNotificationStrategy strategy, CancellationToken cancellationToken)
        where TNotification : INotification
    {
        NotificationHandlerWrapperBase wrapper = RequestHandlerWrapperCache.GetOrCreateNotification(typeof(TNotification));
        return wrapper.Handle(notification, serviceFactory, cancellationToken, strategy);
    }

    // ───────────────────────────── CreateStream ─────────────────────────────

    /// <summary>
    /// Executes the Create Stream{MResponse} operation.
    /// </summary>
    public IAsyncEnumerable<MResponse> CreateStream<MResponse>(
        IStreamRequest<MResponse> request,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(request);
        Type requestType = request.GetType();
        StreamHandlerWrapperBase wrapper = RequestHandlerWrapperCache.GetOrCreateStream<MResponse>(requestType);
        
        return YieldStream<MResponse>(wrapper.CreateStream(request, serviceFactory, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Executes the Create Stream operation.
    /// </summary>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(request);
        Type requestType = request.GetType();
        Type? responseType = requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>))
            ?.GetGenericArguments()[0];

        if (responseType == null)
            throw new MInternalException($"Object of type {requestType.Name} does not implement IStreamRequest<T>.");

        StreamHandlerWrapperBase wrapper = RequestHandlerWrapperCache.GetOrCreateStream(requestType, responseType);
        return wrapper.CreateStream(request, serviceFactory, cancellationToken);
    }

    private static async IAsyncEnumerable<T> YieldStream<T>(IAsyncEnumerable<object?> stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (object? item in stream.WithCancellation(cancellationToken))
        {
            yield return (T)item!;
        }
    }
}
