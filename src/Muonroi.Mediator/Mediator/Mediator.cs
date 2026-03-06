using Muonroi.Mediator.Mediator.Interfaces;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Muonroi.Mediator.Mediator;

public delegate object? ServiceFactory(Type serviceType);

public class MMediator(ServiceFactory serviceFactory) : IMediator
{
    public async Task<MResponse> Send<MResponse>(IRequest<MResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Type requestType = request.GetType();
        Type handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(MResponse));
        object handler = serviceFactory(handlerType) ??
                      throw new InvalidOperationException($"Handler for {requestType} not found");
        MethodInfo handleMethod = handlerType.GetMethod("Handle")!;
        RequestHandlerDelegate<MResponse> handlerDelegate = () =>
            (Task<MResponse>)handleMethod.Invoke(handler, [request, cancellationToken])!;
        if (serviceFactory(
                typeof(IEnumerable<>).MakeGenericType(
                    typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(MResponse)))) is
            not IEnumerable<object> behaviors)
        {
            return await handlerDelegate();
        }

        {
            foreach (object? behavior in behaviors.Reverse())
            {
                RequestHandlerDelegate<MResponse> next = handlerDelegate;
                MethodInfo behaviorHandle = behavior.GetType().GetMethod("Handle")!;
                handlerDelegate = () =>
                    (Task<MResponse>)behaviorHandle.Invoke(behavior, [request, next, cancellationToken])!;
            }
        }

        return await handlerDelegate();
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        return Send((IRequest<Unit>)request, cancellationToken);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new NullReferenceException(nameof(request));
        }

        return SendDynamic((dynamic)request, cancellationToken);
    }

    private async Task<object?> SendDynamic<MResponse>(IRequest<MResponse> request, CancellationToken cancellationToken)
    {
        MResponse? response = await Send(request, cancellationToken);
        return response;
    }

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : Interfaces.INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        Type notificationType = typeof(INotificationHandler<>).MakeGenericType(notification.GetType());
        if (serviceFactory(
                typeof(IEnumerable<>).MakeGenericType(notificationType)) is not IEnumerable<object> handlers)
        {
            return;
        }

        foreach (object handler in handlers)
        {
            MethodInfo method = handler.GetType().GetMethod("Handle")!;
            await (Task)method.Invoke(handler, [notification, cancellationToken])!;
        }
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Type notificationType = notification.GetType();
        _ = typeof(Interfaces.INotification).IsAssignableFrom(notificationType)
            ? notificationType
            : throw new InvalidOperationException("Notification does not implement INotification");
        MethodInfo method = typeof(MMediator).GetMethod(nameof(Publish), [typeof(Interfaces.INotification), typeof(CancellationToken)])!;
        return (Task)method.Invoke(this, [notification, cancellationToken])!;
    }

    public async IAsyncEnumerable<MResponse> CreateStream<MResponse>(
        IStreamRequest<MResponse> request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Type requestType = request.GetType();
        Type handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, typeof(MResponse));
        object? handler = serviceFactory(handlerType);
        if (handler == null)
        {
            yield break;
        }

        MethodInfo streamHandle = handler.GetType().GetMethod("Handle")!;
        object? result;
        try
        {
            result = streamHandle.Invoke(handler, [request, cancellationToken]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }

        if (result is IAsyncEnumerable<MResponse> asyncEnumerable)
        {
            await foreach (MResponse? item in asyncEnumerable.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Handler {handler.GetType().Name} does not return IAsyncEnumerable<{typeof(MResponse).Name}>");
        }
    }


    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        Type requestType = request.GetType();
        Type responseType = requestType.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>))
            .GetGenericArguments()[0];
        MethodInfo method = typeof(MMediator).GetMethod(nameof(CreateStream),
            [typeof(IStreamRequest<>).MakeGenericType(responseType), typeof(CancellationToken)])!;
        return (IAsyncEnumerable<object?>)method.Invoke(this, [request, cancellationToken])!;
    }
}
