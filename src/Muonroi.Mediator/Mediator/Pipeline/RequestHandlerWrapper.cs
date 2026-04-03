using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Mediator.Mediator;
using Muonroi.Mediator.Mediator.Interfaces;
using System.Collections.Concurrent;

namespace Muonroi.Mediator.Mediator.Pipeline;

/// <summary>
/// Non-generic base used as dictionary value so the cache is typed uniformly.
/// </summary>
internal abstract class RequestHandlerBase
{
    public abstract Task<object?> Handle(
        object request,
        ServiceFactory serviceFactory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed request handler wrapper.
/// Created once per (TRequest, TResponse) pair and cached in <see cref="RequestHandlerWrapperCache"/>.
/// On the hot path there is NO reflection — only direct interface dispatch.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerBase
    where TRequest : IRequest<TResponse>
{
    public override async Task<object?> Handle(
        object request,
        ServiceFactory serviceFactory,
        CancellationToken cancellationToken)
    {
        TResponse result = await HandleTyped((TRequest)request, serviceFactory, cancellationToken);
        return result;
    }

    private static async Task<TResponse> HandleTyped(
        TRequest request,
        ServiceFactory serviceFactory,
        CancellationToken cancellationToken)
    {
        IRequestHandler<TRequest, TResponse> handler =
            (IRequestHandler<TRequest, TResponse>?)serviceFactory(typeof(IRequestHandler<TRequest, TResponse>))
            ?? throw new MInternalException(
                $"No handler registered for request type '{typeof(TRequest).FullName}'. " +
                $"Register an IRequestHandler<{typeof(TRequest).Name},{typeof(TResponse).Name}> in DI.");

        RequestHandlerDelegate<TResponse> handlerDelegate = () =>
            handler.Handle(request, cancellationToken);

        IEnumerable<IPipelineBehavior<TRequest, TResponse>>? behaviors =
            serviceFactory(typeof(IEnumerable<IPipelineBehavior<TRequest, TResponse>>))
            as IEnumerable<IPipelineBehavior<TRequest, TResponse>>;

        if (behaviors != null)
        {
            foreach (IPipelineBehavior<TRequest, TResponse> behavior in behaviors.Reverse())
            {
                RequestHandlerDelegate<TResponse> next = handlerDelegate;
                handlerDelegate = () => behavior.Handle(request, next, cancellationToken);
            }
        }

        return await handlerDelegate();
    }
}

/// <summary>
/// Non-generic base for notification wrappers.
/// </summary>
internal abstract class NotificationHandlerWrapperBase
{
    public abstract Task Handle(
        object notification,
        ServiceFactory serviceFactory,
        CancellationToken cancellationToken,
        Interfaces.MNotificationStrategy strategy);
}

/// <summary>
/// Strongly-typed notification handler wrapper with strategy-aware dispatch.
/// </summary>
internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapperBase
    where TNotification : INotification
{
    public override async Task Handle(
        object notification,
        ServiceFactory serviceFactory,
        CancellationToken cancellationToken,
        Interfaces.MNotificationStrategy strategy)
    {
        IEnumerable<INotificationHandler<TNotification>>? handlers =
            serviceFactory(typeof(IEnumerable<INotificationHandler<TNotification>>))
            as IEnumerable<INotificationHandler<TNotification>>;

        if (handlers == null) return;

        INotificationHandler<TNotification>[] handlerArray = [.. handlers];
        TNotification typedNotification = (TNotification)notification;

        switch (strategy)
        {
            case Interfaces.MNotificationStrategy.Sequential:
                foreach (INotificationHandler<TNotification> h in handlerArray)
                    await h.Handle(typedNotification, cancellationToken);
                break;

            case Interfaces.MNotificationStrategy.ParallelWaitAll:
                await Task.WhenAll(handlerArray.Select(h => h.Handle(typedNotification, cancellationToken)));
                break;

            case Interfaces.MNotificationStrategy.ParallelNoWait:
                foreach (INotificationHandler<TNotification> h in handlerArray)
                    _ = Task.Run(() => h.Handle(typedNotification, cancellationToken), cancellationToken);
                break;
        }
    }
}

/// <summary>
/// Non-generic base for stream request wrappers.
/// </summary>
internal abstract class StreamHandlerWrapperBase
{
    public abstract IAsyncEnumerable<object?> CreateStream(
        object request,
        ServiceFactory serviceFactory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed stream handler wrapper.
/// </summary>
internal sealed class StreamHandlerWrapperImpl<TRequest, TResponse> : StreamHandlerWrapperBase
    where TRequest : IStreamRequest<TResponse>
{
    public override async IAsyncEnumerable<object?> CreateStream(
        object request,
        ServiceFactory serviceFactory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IStreamRequestHandler<TRequest, TResponse>? handler =
            (IStreamRequestHandler<TRequest, TResponse>?)serviceFactory(typeof(IStreamRequestHandler<TRequest, TResponse>));
        
        if (handler == null) yield break;

        await foreach (TResponse item in handler.Handle((TRequest)request, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}

/// <summary>
/// Thread-safe cache: RequestType → RequestHandlerBase (compiled wrapper).
/// Populated lazily on first call — after that, no reflection on the hot path.
/// </summary>
internal static class RequestHandlerWrapperCache
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestCache = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapperBase> _notificationCache = new();
    private static readonly ConcurrentDictionary<Type, StreamHandlerWrapperBase> _streamCache = new();

    public static RequestHandlerBase GetOrCreate<TResponse>(Type requestType)
    {
        return GetOrCreate(requestType, typeof(TResponse));
    }

    public static RequestHandlerBase GetOrCreate(Type requestType, Type responseType)
    {
        return _requestCache.GetOrAdd(requestType, static (rt, resType) =>
        {
            Type wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(rt, resType);
            return (RequestHandlerBase)Activator.CreateInstance(wrapperType)!;
        }, responseType);
    }

    public static NotificationHandlerWrapperBase GetOrCreateNotification(Type notificationType)
    {
        return _notificationCache.GetOrAdd(notificationType, static nt =>
        {
            Type wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(nt);
            return (NotificationHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });
    }

    public static StreamHandlerWrapperBase GetOrCreateStream<TResponse>(Type requestType)
    {
        return GetOrCreateStream(requestType, typeof(TResponse));
    }

    public static StreamHandlerWrapperBase GetOrCreateStream(Type requestType, Type responseType)
    {
        return _streamCache.GetOrAdd(requestType, static (rt, resType) =>
        {
            Type wrapperType = typeof(StreamHandlerWrapperImpl<,>).MakeGenericType(rt, resType);
            return (StreamHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        }, responseType);
    }
}
